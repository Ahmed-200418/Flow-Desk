using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FlowDesk.Application.Common.Exceptions.ValidationException;

namespace FlowDesk.Application.Features.Delegations;

public record DelegationDto(
    Guid Id,
    Guid DelegatorUserId,
    string DelegatorName,
    string DelegatorEmail,
    Guid DelegateeUserId,
    string DelegateeName,
    string DelegateeEmail,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    Guid? RequestTypeId,
    string? RequestTypeName,
    bool IsActive,
    string Reason,
    DateTime CreatedAtUtc
);

// Create Delegation Command
public record CreateDelegationCommand(
    Guid DelegateeUserId,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string Reason,
    Guid? RequestTypeId = null,
    Guid? DelegatorUserId = null
) : IRequest<Guid>;

public class CreateDelegationCommandValidator : AbstractValidator<CreateDelegationCommand>
{
    public CreateDelegationCommandValidator()
    {
        RuleFor(x => x.DelegateeUserId).NotEmpty().WithMessage("Delegatee user is required.");
        RuleFor(x => x.StartDateUtc).NotEmpty().WithMessage("Start date is required.");
        RuleFor(x => x.EndDateUtc).NotEmpty().WithMessage("End date is required.");
        RuleFor(x => x.EndDateUtc).GreaterThan(x => x.StartDateUtc).WithMessage("End date must be strictly after start date.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Reason is required and cannot exceed 500 characters.");
    }
}

public class CreateDelegationCommandHandler : IRequestHandler<CreateDelegationCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateDelegationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateDelegationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user required.");
        }

        var currentUserId = _currentUserService.UserId.Value;
        Guid actualDelegatorId = currentUserId;

        // If delegator ID is provided and different, verify admin rights
        if (request.DelegatorUserId.HasValue && request.DelegatorUserId.Value != currentUserId)
        {
            var isUserAdmin = await _context.UserRoles
                .AnyAsync(ur => ur.UserId == currentUserId &&
                               (ur.Role.Name == "Super Admin" || ur.Role.Name == "Organization Admin"), cancellationToken);

            if (!isUserAdmin)
            {
                throw new ForbiddenException("Only administrators can create delegations on behalf of other users.");
            }

            actualDelegatorId = request.DelegatorUserId.Value;
        }

        // 1. Self-delegation check
        if (actualDelegatorId == request.DelegateeUserId)
        {
            throw new ValidationException(new[] { new ValidationFailure("DelegateeUserId", "Self-delegation is not allowed.") });
        }

        // 2. Validate users exist
        var delegatorExists = await _context.Users.AnyAsync(u => u.Id == actualDelegatorId && u.IsActive, cancellationToken);
        if (!delegatorExists) throw new NotFoundException("Delegator user not found or inactive.");

        var delegateeExists = await _context.Users.AnyAsync(u => u.Id == request.DelegateeUserId && u.IsActive, cancellationToken);
        if (!delegateeExists) throw new NotFoundException("Delegatee user not found or inactive.");

        // 3. Date range validation
        if (request.EndDateUtc <= request.StartDateUtc)
        {
            throw new ValidationException(new[] { new ValidationFailure("EndDateUtc", "End date must be after start date.") });
        }

        if (request.EndDateUtc <= DateTime.UtcNow)
        {
            throw new ValidationException(new[] { new ValidationFailure("EndDateUtc", "End date must be in the future.") });
        }

        // 4. Overlapping delegation check
        var overlapping = await _context.Delegations
            .Where(d => d.DelegatorUserId == actualDelegatorId && d.IsActive)
            .Where(d => d.StartDateUtc < request.EndDateUtc && d.EndDateUtc > request.StartDateUtc)
            .Where(d => d.RequestTypeId == null || request.RequestTypeId == null || d.RequestTypeId == request.RequestTypeId)
            .AnyAsync(cancellationToken);

        if (overlapping)
        {
            throw new ValidationException(new[] { new ValidationFailure("StartDateUtc", "An active overlapping delegation already exists for this timeframe and request type.") });
        }

        // 5. Circular delegation check (Detect if Delegatee -> ... -> Delegator chain exists)
        var isCircular = await HasCircularDelegationAsync(request.DelegateeUserId, actualDelegatorId, request.StartDateUtc, request.EndDateUtc, request.RequestTypeId, cancellationToken);
        if (isCircular)
        {
            throw new ValidationException(new[] { new ValidationFailure("DelegateeUserId", "Circular delegation detected. The chosen delegatee already has a delegation chain leading back to you.") });
        }

        var delegation = new Delegation(
            actualDelegatorId,
            request.DelegateeUserId,
            request.StartDateUtc,
            request.EndDateUtc,
            request.Reason,
            request.RequestTypeId);

        _context.Delegations.Add(delegation);

        // Record Audit Trail
        var audit = new AuditLog(
            action: "CreateDelegation",
            entityName: nameof(Delegation),
            entityId: delegation.Id.ToString(),
            userId: currentUserId,
            userEmail: _currentUserService.UserEmail,
            newValuesJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                DelegatorUserId = actualDelegatorId,
                DelegateeUserId = request.DelegateeUserId,
                request.StartDateUtc,
                request.EndDateUtc,
                request.RequestTypeId,
                request.Reason
            }));
        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync(cancellationToken);
        return delegation.Id;
    }

    private async Task<bool> HasCircularDelegationAsync(
        Guid startUserId,
        Guid targetUserId,
        DateTime startDate,
        DateTime endDate,
        Guid? requestTypeId,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { startUserId };
        var queue = new Queue<Guid>();
        queue.Enqueue(startUserId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == targetUserId)
            {
                return true;
            }

            var nextDelegatees = await _context.Delegations
                .Where(d => d.DelegatorUserId == current && d.IsActive)
                .Where(d => d.StartDateUtc < endDate && d.EndDateUtc > startDate)
                .Where(d => d.RequestTypeId == null || requestTypeId == null || d.RequestTypeId == requestTypeId)
                .Select(d => d.DelegateeUserId)
                .ToListAsync(cancellationToken);

            foreach (var delegateeId in nextDelegatees)
            {
                if (delegateeId == targetUserId) return true;

                if (visited.Add(delegateeId))
                {
                    queue.Enqueue(delegateeId);
                }
            }
        }

        return false;
    }
}

// Cancel Delegation Command
public record CancelDelegationCommand(Guid DelegationId) : IRequest;

public class CancelDelegationCommandHandler : IRequestHandler<CancelDelegationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CancelDelegationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(CancelDelegationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();
        var currentUserId = _currentUserService.UserId.Value;

        var delegation = await _context.Delegations.FirstOrDefaultAsync(d => d.Id == request.DelegationId, cancellationToken);
        if (delegation == null) throw new NotFoundException(nameof(Delegation), request.DelegationId);

        var isOwner = delegation.DelegatorUserId == currentUserId;
        var isAdmin = await _context.UserRoles
            .AnyAsync(ur => ur.UserId == currentUserId &&
                           (ur.Role.Name == "Super Admin" || ur.Role.Name == "Organization Admin"), cancellationToken);

        if (!isOwner && !isAdmin)
        {
            throw new ForbiddenException("You can only cancel your own delegations unless you are an administrator.");
        }

        delegation.Cancel();

        // Audit log
        var audit = new AuditLog(
            action: "CancelDelegation",
            entityName: nameof(Delegation),
            entityId: delegation.Id.ToString(),
            userId: currentUserId,
            userEmail: _currentUserService.UserEmail);
        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync(cancellationToken);
    }
}

// Get My Delegations Query
public record GetMyDelegationsQuery(bool IncludeInactive = false) : IRequest<List<DelegationDto>>;

public class GetMyDelegationsQueryHandler : IRequestHandler<GetMyDelegationsQuery, List<DelegationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyDelegationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<DelegationDto>> Handle(GetMyDelegationsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();
        var userId = _currentUserService.UserId.Value;

        var query = _context.Delegations
            .AsNoTracking()
            .Include(d => d.DelegatorUser)
            .Include(d => d.DelegateeUser)
            .Include(d => d.RequestType)
            .Where(d => d.DelegatorUserId == userId || d.DelegateeUserId == userId);

        if (!request.IncludeInactive)
        {
            var now = DateTime.UtcNow;
            query = query.Where(d => d.IsActive && d.EndDateUtc > now);
        }

        var list = await query.OrderByDescending(d => d.CreatedAtUtc).ToListAsync(cancellationToken);

        return list.Select(d => new DelegationDto(
            d.Id,
            d.DelegatorUserId,
            d.DelegatorUser.FullName,
            d.DelegatorUser.Email,
            d.DelegateeUserId,
            d.DelegateeUser.FullName,
            d.DelegateeUser.Email,
            d.StartDateUtc,
            d.EndDateUtc,
            d.RequestTypeId,
            d.RequestType != null ? d.RequestType.Name : null,
            d.IsActive && d.EndDateUtc > DateTime.UtcNow,
            d.Reason,
            d.CreatedAtUtc
        )).ToList();
    }
}

// Get All Delegations Query (Admin)
public record GetAllDelegationsQuery(int PageNumber = 1, int PageSize = 10, bool OnlyActive = false) : IRequest<PaginatedList<DelegationDto>>;

public class GetAllDelegationsQueryHandler : IRequestHandler<GetAllDelegationsQuery, PaginatedList<DelegationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllDelegationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<DelegationDto>> Handle(GetAllDelegationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Delegations
            .AsNoTracking()
            .Include(d => d.DelegatorUser)
            .Include(d => d.DelegateeUser)
            .Include(d => d.RequestType)
            .AsQueryable();

        if (request.OnlyActive)
        {
            var now = DateTime.UtcNow;
            query = query.Where(d => d.IsActive && d.EndDateUtc > now);
        }

        query = query.OrderByDescending(d => d.CreatedAtUtc);

        var dtoQuery = query.Select(d => new DelegationDto(
            d.Id,
            d.DelegatorUserId,
            d.DelegatorUser.FullName,
            d.DelegatorUser.Email,
            d.DelegateeUserId,
            d.DelegateeUser.FullName,
            d.DelegateeUser.Email,
            d.StartDateUtc,
            d.EndDateUtc,
            d.RequestTypeId,
            d.RequestType != null ? d.RequestType.Name : null,
            d.IsActive && d.EndDateUtc > DateTime.UtcNow,
            d.Reason,
            d.CreatedAtUtc
        ));

        return await PaginatedList<DelegationDto>.CreateAsync(dtoQuery, request.PageNumber, request.PageSize, cancellationToken);
    }
}

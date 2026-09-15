using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FlowDesk.Application.Common.Exceptions.ValidationException;

namespace FlowDesk.Application.Features.Approvals;

public record ApprovalActionDto(
    Guid Id,
    Guid ActorUserId,
    string ActorName,
    ApprovalDecision Decision,
    string? Comment,
    DateTime RespondedAtUtc
);

public record ApprovalInstanceDto(
    Guid Id,
    Guid RequestId,
    Guid WorkflowStepId,
    int StepNumber,
    Guid? AssignedUserId,
    string? AssignedUserName,
    Guid? AssignedRoleId,
    string? AssignedRoleName,
    ApprovalStatus Status,
    DateTime AssignedAtUtc,
    DateTime DueAtUtc,
    DateTime? RespondedAtUtc,
    List<ApprovalActionDto> Actions
);

public record PendingApprovalSummaryDto(
    Guid ApprovalInstanceId,
    Guid RequestId,
    string RequestNumber,
    string RequestTitle,
    string RequestTypeName,
    Guid RequesterUserId,
    string RequesterName,
    RequestPriority Priority,
    decimal TotalAmount,
    string Currency,
    int StepNumber,
    DateTime AssignedAtUtc,
    DateTime DueAtUtc,
    bool IsDelegated = false,
    string? DelegatedFromUserName = null,
    string SlaStatus = "OnTime"
);

public record ApprovalHistoryDto(
    Guid RequestId,
    string RequestNumber,
    RequestStatus RequestStatus,
    List<ApprovalInstanceDto> Steps
);

// Approve Command
public record ApproveRequestCommand(
    Guid RequestId,
    Guid ApprovalInstanceId,
    string? Comment = null,
    string? IdempotencyKey = null
) : IRequest;

public class ApproveRequestCommandHandler : IRequestHandler<ApproveRequestCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IWorkflowEvaluator? _workflowEvaluator;
    private readonly IApproverResolver? _approverResolver;

    public ApproveRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdempotencyService idempotencyService,
        IWorkflowEvaluator? workflowEvaluator = null,
        IApproverResolver? approverResolver = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _idempotencyService = idempotencyService;
        _workflowEvaluator = workflowEvaluator;
        _approverResolver = approverResolver;
    }

    public async Task Handle(ApproveRequestCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) && _idempotencyService.IsProcessed(request.IdempotencyKey))
        {
            return; // Idempotent execution
        }

        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException("Authenticated user required.");
        var actorUserId = _currentUserService.UserId.Value;

        var instance = await _context.ApprovalInstances
            .Include(ai => ai.Request)
            .Include(ai => ai.WorkflowStep)
            .FirstOrDefaultAsync(ai => ai.Id == request.ApprovalInstanceId && ai.RequestId == request.RequestId, cancellationToken);

        if (instance == null) throw new NotFoundException(nameof(ApprovalInstance), request.ApprovalInstanceId);

        // Record decision
        var action = instance.RecordDecision(ApprovalDecision.Approved, actorUserId, request.Comment);
        _context.ApprovalActions.Add(action);

        var req = instance.Request;

        // Fetch remaining workflow steps for this version
        var remainingSteps = await _context.WorkflowSteps
            .Include(ws => ws.Conditions)
            .Where(ws => ws.WorkflowVersionId == req.WorkflowVersionId && ws.StepNumber > instance.StepNumber)
            .OrderBy(ws => ws.StepNumber)
            .ToListAsync(cancellationToken);

        WorkflowStep? nextStep = null;
        if (_workflowEvaluator != null)
        {
            nextStep = remainingSteps.FirstOrDefault(s => _workflowEvaluator.EvaluateStepConditions(s.Conditions, req));
        }
        else
        {
            nextStep = remainingSteps.FirstOrDefault();
        }

        if (nextStep != null)
        {
            // Advance to next step
            req.AdvanceToStep(nextStep.StepNumber);

            Guid? assignedUserId = nextStep.ApproverType == ApproverType.User ? nextStep.ApproverTargetId : null;
            Guid? assignedRoleId = nextStep.ApproverType == ApproverType.Role ? nextStep.ApproverTargetId : null;

            if (_approverResolver != null)
            {
                var resolved = await _approverResolver.ResolveApproverAsync(nextStep, req, cancellationToken);
                if (resolved.AssignedUserId.HasValue) assignedUserId = resolved.AssignedUserId;
                if (resolved.AssignedRoleId.HasValue) assignedRoleId = resolved.AssignedRoleId;
            }

            // Create ApprovalInstance for next step
            var nextInstance = new ApprovalInstance(
                req.Id,
                nextStep.Id,
                nextStep.StepNumber,
                assignedUserId,
                assignedRoleId,
                nextStep.TimeoutHours);

            _context.ApprovalInstances.Add(nextInstance);
        }
        else
        {
            // Final step approved -> Mark Request as Approved & Completed
            req.Approve();
            req.Complete();
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                _idempotencyService.MarkAsProcessed(request.IdempotencyKey);
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new DomainException("Concurrency conflict detected. Another user updated this approval step simultaneously.", ex);
        }
    }
}

// Reject Command
public record RejectRequestCommand(
    Guid RequestId,
    Guid ApprovalInstanceId,
    string Comment,
    string? IdempotencyKey = null
) : IRequest;

public class RejectRequestCommandValidator : AbstractValidator<RejectRequestCommand>
{
    public RejectRequestCommandValidator()
    {
        RuleFor(x => x.Comment).NotEmpty().WithMessage("Rejection reason comment is mandatory.");
    }
}

public class RejectRequestCommandHandler : IRequestHandler<RejectRequestCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdempotencyService _idempotencyService;

    public RejectRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdempotencyService idempotencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _idempotencyService = idempotencyService;
    }

    public async Task Handle(RejectRequestCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) && _idempotencyService.IsProcessed(request.IdempotencyKey))
        {
            return;
        }

        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();
        var actorUserId = _currentUserService.UserId.Value;

        var instance = await _context.ApprovalInstances
            .Include(ai => ai.Request)
            .FirstOrDefaultAsync(ai => ai.Id == request.ApprovalInstanceId && ai.RequestId == request.RequestId, cancellationToken);

        if (instance == null) throw new NotFoundException(nameof(ApprovalInstance), request.ApprovalInstanceId);

        var action = instance.RecordDecision(ApprovalDecision.Rejected, actorUserId, request.Comment);
        _context.ApprovalActions.Add(action);

        var req = instance.Request;
        req.Reject();

        // Cancel all other active approval instances for this request
        var pendingInstances = await _context.ApprovalInstances
            .Where(ai => ai.RequestId == req.Id && ai.Id != instance.Id && ai.Status == ApprovalStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var p in pendingInstances)
        {
            var cancelAction = p.RecordDecision(ApprovalDecision.Rejected, actorUserId, "Cancelled due to request rejection.");
            _context.ApprovalActions.Add(cancelAction);
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            _idempotencyService.MarkAsProcessed(request.IdempotencyKey);
        }
    }
}

// Return Command
public record ReturnRequestCommand(
    Guid RequestId,
    Guid ApprovalInstanceId,
    string Comment,
    string? IdempotencyKey = null
) : IRequest;

public class ReturnRequestCommandHandler : IRequestHandler<ReturnRequestCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdempotencyService _idempotencyService;

    public ReturnRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdempotencyService idempotencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _idempotencyService = idempotencyService;
    }

    public async Task Handle(ReturnRequestCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) && _idempotencyService.IsProcessed(request.IdempotencyKey))
        {
            return;
        }

        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();
        var actorUserId = _currentUserService.UserId.Value;

        var instance = await _context.ApprovalInstances
            .Include(ai => ai.Request)
            .FirstOrDefaultAsync(ai => ai.Id == request.ApprovalInstanceId && ai.RequestId == request.RequestId, cancellationToken);

        if (instance == null) throw new NotFoundException(nameof(ApprovalInstance), request.ApprovalInstanceId);

        instance.RecordDecision(ApprovalDecision.Returned, actorUserId, request.Comment);

        var req = instance.Request;
        req.ReturnToDraft();

        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            _idempotencyService.MarkAsProcessed(request.IdempotencyKey);
        }
    }
}

// Delegate Command
public record DelegateApprovalCommand(
    Guid RequestId,
    Guid ApprovalInstanceId,
    Guid TargetUserId,
    string Reason
) : IRequest;

public class DelegateApprovalCommandHandler : IRequestHandler<DelegateApprovalCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DelegateApprovalCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DelegateApprovalCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();
        var actorUserId = _currentUserService.UserId.Value;

        var instance = await _context.ApprovalInstances.FirstOrDefaultAsync(ai => ai.Id == request.ApprovalInstanceId && ai.RequestId == request.RequestId, cancellationToken);
        if (instance == null) throw new NotFoundException(nameof(ApprovalInstance), request.ApprovalInstanceId);

        var targetUserExists = await _context.Users.AnyAsync(u => u.Id == request.TargetUserId, cancellationToken);
        if (!targetUserExists) throw new NotFoundException("Target user for delegation was not found.");

        var action = instance.Delegate(request.TargetUserId, actorUserId, request.Reason);
        _context.ApprovalActions.Add(action);

        // Create new approval instance assigned to delegatee
        var delegatedInstance = new ApprovalInstance(
            instance.RequestId,
            instance.WorkflowStepId,
            instance.StepNumber,
            request.TargetUserId,
            null);

        _context.ApprovalInstances.Add(delegatedInstance);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

// Approver Inbox Query
public record GetPendingApprovalsQuery(int PageNumber = 1, int PageSize = 10) : IRequest<PaginatedList<PendingApprovalSummaryDto>>;

public class GetPendingApprovalsQueryHandler : IRequestHandler<GetPendingApprovalsQuery, PaginatedList<PendingApprovalSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPendingApprovalsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<PendingApprovalSummaryDto>> Handle(GetPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();
        var userId = _currentUserService.UserId.Value;
        var now = DateTime.UtcNow;

        var userRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        // Fetch active delegations where current user is the delegatee
        var activeDelegations = await _context.Delegations
            .AsNoTracking()
            .Include(d => d.DelegatorUser)
            .Where(d => d.DelegateeUserId == userId && d.IsActive && d.StartDateUtc <= now && d.EndDateUtc >= now)
            .ToListAsync(cancellationToken);

        var delegatorUserIds = activeDelegations.Select(d => d.DelegatorUserId).ToList();

        var query = _context.ApprovalInstances
            .AsNoTracking()
            .Include(ai => ai.AssignedUser)
            .Include(ai => ai.Request).ThenInclude(r => r.RequestType)
            .Include(ai => ai.Request).ThenInclude(r => r.RequesterUser)
            .Include(ai => ai.Actions)
            .Where(ai => ai.Status == ApprovalStatus.Pending &&
                        (ai.AssignedUserId == userId ||
                         (ai.AssignedRoleId.HasValue && userRoleIds.Contains(ai.AssignedRoleId.Value)) ||
                         (ai.AssignedUserId.HasValue && delegatorUserIds.Contains(ai.AssignedUserId.Value))))
            .OrderBy(ai => ai.DueAtUtc);

        var instances = await query.ToListAsync(cancellationToken);

        var dtos = instances.Select(ai =>
        {
            bool isDelegated = ai.AssignedUserId.HasValue && ai.AssignedUserId.Value != userId && delegatorUserIds.Contains(ai.AssignedUserId.Value);
            string? delegatedFrom = isDelegated ? activeDelegations.FirstOrDefault(d => d.DelegatorUserId == ai.AssignedUserId.Value)?.DelegatorUser?.FullName : null;

            string slaStatus = "OnTime";
            var isEscalated = ai.Actions.Any(a => a.Decision == ApprovalDecision.Delegated && a.Comment != null && a.Comment.Contains("Escalated"));
            if (isEscalated)
            {
                slaStatus = "Escalated";
            }
            else if (now > ai.DueAtUtc)
            {
                slaStatus = "Overdue";
            }
            else
            {
                var totalDuration = (ai.DueAtUtc - ai.AssignedAtUtc).TotalHours;
                var elapsedHours = (now - ai.AssignedAtUtc).TotalHours;
                if (elapsedHours >= (totalDuration * 0.5))
                {
                    slaStatus = "Reminder";
                }
            }

            return new PendingApprovalSummaryDto(
                ai.Id,
                ai.RequestId,
                ai.Request.RequestNumber,
                ai.Request.Title,
                ai.Request.RequestType.Name,
                ai.Request.RequesterUserId,
                ai.Request.RequesterUser.FullName,
                ai.Request.Priority,
                ai.Request.TotalAmount,
                ai.Request.Currency,
                ai.StepNumber,
                ai.AssignedAtUtc,
                ai.DueAtUtc,
                isDelegated,
                delegatedFrom,
                slaStatus
            );
        }).ToList();

        int count = dtos.Count;
        var items = dtos.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();

        return new PaginatedList<PendingApprovalSummaryDto>(items, count, request.PageNumber, request.PageSize);
    }
}

// Approval History Query
public record GetApprovalHistoryQuery(Guid RequestId) : IRequest<ApprovalHistoryDto>;

public class GetApprovalHistoryQueryHandler : IRequestHandler<GetApprovalHistoryQuery, ApprovalHistoryDto>
{
    private readonly IApplicationDbContext _context;

    public GetApprovalHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApprovalHistoryDto> Handle(GetApprovalHistoryQuery request, CancellationToken cancellationToken)
    {
        var req = await _context.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);
        if (req == null) throw new NotFoundException(nameof(Request), request.RequestId);

        var instances = await _context.ApprovalInstances
            .AsNoTracking()
            .Include(ai => ai.AssignedUser)
            .Include(ai => ai.AssignedRole)
            .Include(ai => ai.Actions).ThenInclude(a => a.ActorUser)
            .Where(ai => ai.RequestId == request.RequestId)
            .OrderBy(ai => ai.StepNumber)
            .ThenBy(ai => ai.AssignedAtUtc)
            .ToListAsync(cancellationToken);

        var stepDtos = instances.Select(ai => new ApprovalInstanceDto(
            ai.Id,
            ai.RequestId,
            ai.WorkflowStepId,
            ai.StepNumber,
            ai.AssignedUserId,
            ai.AssignedUser?.FullName,
            ai.AssignedRoleId,
            ai.AssignedRole?.Name,
            ai.Status,
            ai.AssignedAtUtc,
            ai.DueAtUtc,
            ai.RespondedAtUtc,
            ai.Actions.OrderBy(a => a.RespondedAtUtc).Select(a => new ApprovalActionDto(
                a.Id,
                a.ActorUserId,
                a.ActorUser.FullName,
                a.Decision,
                a.Comment,
                a.RespondedAtUtc)).ToList()
        )).ToList();

        return new ApprovalHistoryDto(req.Id, req.RequestNumber, req.Status, stepDtos);
    }
}

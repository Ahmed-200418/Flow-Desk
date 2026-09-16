using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FlowDesk.Application.Common.Exceptions.ValidationException;

namespace FlowDesk.Application.Features.Users;

public record UserSummaryDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? JobTitle,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? PositionId,
    string? PositionTitle,
    bool IsActive,
    List<string> Roles
);

public record UserDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? JobTitle,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? PositionId,
    string? PositionTitle,
    bool IsActive,
    bool IsLockedOut,
    DateTime? LockoutEndUtc,
    List<string> Roles,
    List<string> Permissions,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// Create User Command
public record CreateUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? JobTitle = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    List<Guid>? RoleIds = null
) : IRequest<UserDetailDto>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public CreateUserCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDetailDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            throw new ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Email", "Email is already registered.") });
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new User(normalizedEmail, passwordHash, request.FirstName, request.LastName, request.JobTitle, request.DepartmentId, request.PositionId);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.RoleIds != null && request.RoleIds.Count != 0)
        {
            var validRoleIds = await _context.Roles.Where(r => request.RoleIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(cancellationToken);
            foreach (var roleId in validRoleIds)
            {
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetUserDetailAsync(user.Id, _context, cancellationToken);
    }

    public static async Task<UserDetailDto> GetUserDetailAsync(Guid userId, IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.Position)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) throw new NotFoundException(nameof(User), userId);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        return new UserDetailDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.JobTitle,
            user.DepartmentId,
            user.Department?.Name,
            user.PositionId,
            user.Position?.Title,
            user.IsActive,
            user.IsLockedOut,
            user.LockoutEndUtc,
            roles,
            permissions,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }
}

// Assign Roles Command
public record AssignUserRolesCommand(Guid UserId, List<Guid> RoleIds) : IRequest<UserDetailDto>;

public class AssignUserRolesCommandHandler : IRequestHandler<AssignUserRolesCommand, UserDetailDto>
{
    private readonly IApplicationDbContext _context;

    public AssignUserRolesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserDetailDto> Handle(AssignUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null) throw new NotFoundException(nameof(User), request.UserId);

        _context.UserRoles.RemoveRange(user.UserRoles);

        var validRoles = await _context.Roles.Where(r => request.RoleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        foreach (var role in validRoles)
        {
            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await CreateUserCommandHandler.GetUserDetailAsync(user.Id, _context, cancellationToken);
    }
}

// Get Users Query
public record GetUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? DepartmentId = null,
    bool? IsActive = null
) : IRequest<PaginatedList<UserSummaryDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PaginatedList<UserSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<UserSummaryDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(u => u.DepartmentId == request.DepartmentId.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term));
        }

        var dtoQuery = query.OrderBy(u => u.FirstName)
            .Select(u => new UserSummaryDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.FullName,
                u.JobTitle,
                u.DepartmentId,
                u.Department != null ? u.Department.Name : null,
                u.PositionId,
                u.Position != null ? u.Position.Title : null,
                u.IsActive,
                u.UserRoles.Select(ur => ur.Role.Name).ToList()));

        return await PaginatedList<UserSummaryDto>.CreateAsync(dtoQuery, request.PageNumber, request.PageSize, cancellationToken);
    }
}

// Get User By Id Query
public record GetUserByIdQuery(Guid Id) : IRequest<UserDetailDto>;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetUserByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserDetailDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        return await CreateUserCommandHandler.GetUserDetailAsync(request.Id, _context, cancellationToken);
    }
}

// Update User Command
public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? JobTitle,
    Guid? DepartmentId,
    Guid? PositionId,
    List<Guid>? RoleIds = null
) : IRequest<UserDetailDto>;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserDetailDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserDetailDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);
        if (user == null) throw new NotFoundException(nameof(User), request.Id);

        var fnProp = typeof(User).GetProperty(nameof(User.FirstName));
        fnProp?.SetValue(user, request.FirstName.Trim());

        var lnProp = typeof(User).GetProperty(nameof(User.LastName));
        lnProp?.SetValue(user, request.LastName.Trim());

        var jtProp = typeof(User).GetProperty(nameof(User.JobTitle));
        jtProp?.SetValue(user, request.JobTitle?.Trim());

        var deptProp = typeof(User).GetProperty(nameof(User.DepartmentId));
        deptProp?.SetValue(user, request.DepartmentId);

        var posProp = typeof(User).GetProperty(nameof(User.PositionId));
        posProp?.SetValue(user, request.PositionId);

        if (request.RoleIds != null)
        {
            _context.UserRoles.RemoveRange(user.UserRoles);
            var validRoleIds = await _context.Roles.Where(r => request.RoleIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(cancellationToken);
            foreach (var roleId in validRoleIds)
            {
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await CreateUserCommandHandler.GetUserDetailAsync(user.Id, _context, cancellationToken);
    }
}

// Toggle User Status / Delete Command
public record ToggleUserStatusCommand(Guid Id) : IRequest;

public class ToggleUserStatusCommandHandler : IRequestHandler<ToggleUserStatusCommand>
{
    private readonly IApplicationDbContext _context;

    public ToggleUserStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ToggleUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);
        if (user == null) throw new NotFoundException(nameof(User), request.Id);

        var activeProp = typeof(User).GetProperty(nameof(User.IsActive));
        activeProp?.SetValue(user, !user.IsActive);

        await _context.SaveChangesAsync(cancellationToken);
    }
}

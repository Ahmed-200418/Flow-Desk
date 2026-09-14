using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Auth.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IRequest<UserDto>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentUserQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedException("User is not authenticated.");
        }

        var userId = _currentUserService.UserId.Value;
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User account not found or inactive.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        return new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.JobTitle,
            user.DepartmentId,
            roles,
            permissions);
    }
}

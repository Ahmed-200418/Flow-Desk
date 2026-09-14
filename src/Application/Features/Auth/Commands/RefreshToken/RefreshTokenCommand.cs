using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Auth.DTOs;
using FlowDesk.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RefreshTokenEntity = FlowDesk.Domain.Entities.RefreshToken;

namespace FlowDesk.Application.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponseDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUserService _currentUserService;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUserService = currentUserService;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hashedToken = _jwtTokenGenerator.HashToken(request.RefreshToken);
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hashedToken, cancellationToken);

        var ipAddress = _currentUserService.IpAddress ?? "0.0.0.0";

        if (storedToken == null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        // Security check: Reuse of revoked refresh token triggers revocation of ALL active tokens for the user!
        if (storedToken.IsRevoked)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == storedToken.UserId && rt.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.Revoke(ipAddress, "COMPROMISED_REUSE_DETECTED");
            }
            await _context.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedException("Security violation detected: Token reuse attempt. All active sessions have been revoked.");
        }

        if (storedToken.IsExpired)
        {
            throw new UnauthorizedException("Refresh token has expired. Please log in again.");
        }

        var user = storedToken.User;
        if (!user.IsActive)
        {
            throw new UnauthorizedException("User account is inactive.");
        }

        // Rotate token
        var newRawToken = _jwtTokenGenerator.GenerateRefreshToken();
        var newHashedToken = _jwtTokenGenerator.HashToken(newRawToken);

        storedToken.Revoke(ipAddress, newHashedToken);

        var newRefreshTokenEntity = new RefreshTokenEntity(
            newHashedToken,
            user.Id,
            DateTime.UtcNow.AddDays(7),
            ipAddress);

        _context.RefreshTokens.Add(newRefreshTokenEntity);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var (accessToken, expiresAtUtc) = _jwtTokenGenerator.GenerateAccessToken(user, roles, permissions);
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.JobTitle,
            user.DepartmentId,
            roles,
            permissions);

        return new AuthResponseDto(accessToken, newRawToken, expiresAtUtc, userDto);
    }
}

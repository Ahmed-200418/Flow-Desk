using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Auth.DTOs;
using FlowDesk.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RefreshTokenEntity = FlowDesk.Domain.Entities.RefreshToken;

namespace FlowDesk.Application.Features.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUserService _currentUserService;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUserService = currentUserService;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("Invalid credentials or account is inactive.");
        }

        if (user.IsLockedOut)
        {
            throw new UnauthorizedException($"Account is locked due to multiple failed login attempts. Try again after {user.LockoutEndUtc?.ToUniversalTime():u}.");
        }

        bool isPasswordValid = _passwordHasher.VerifyPassword(user.PasswordHash, request.Password);
        if (!isPasswordValid)
        {
            user.RecordFailedLogin();
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Invalid credentials.");
        }

        user.ResetFailedLogin();

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var (accessToken, expiresAtUtc) = _jwtTokenGenerator.GenerateAccessToken(user, roles, permissions);
        var rawRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var hashedRefreshToken = _jwtTokenGenerator.HashToken(rawRefreshToken);

        var ipAddress = _currentUserService.IpAddress ?? "0.0.0.0";
        var refreshTokenEntity = new RefreshTokenEntity(
            hashedRefreshToken,
            user.Id,
            DateTime.UtcNow.AddDays(7),
            ipAddress);

        _context.RefreshTokens.Add(refreshTokenEntity);
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

        return new AuthResponseDto(accessToken, rawRefreshToken, expiresAtUtc, userDto);
    }
}

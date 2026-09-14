using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Auth.Commands.RevokeToken;

public record RevokeTokenCommand(string RefreshToken) : IRequest;

public class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUserService _currentUserService;

    public RevokeTokenCommandHandler(
        IApplicationDbContext context,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUserService = currentUserService;
    }

    public async Task Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        var hashedToken = _jwtTokenGenerator.HashToken(request.RefreshToken);
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hashedToken, cancellationToken);

        if (storedToken == null)
        {
            throw new NotFoundException("Token not found.");
        }

        if (storedToken.IsActive)
        {
            var ipAddress = _currentUserService.IpAddress ?? "0.0.0.0";
            storedToken.Revoke(ipAddress, "MANUAL_REVOCATION");
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

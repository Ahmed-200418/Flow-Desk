using System.Security.Claims;
using FlowDesk.Application.Common.Interfaces;

namespace FlowDesk.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public string? UserEmail => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public string? IpAddress
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                return forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}

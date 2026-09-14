using FlowDesk.Domain.Entities;

namespace FlowDesk.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    (string AccessToken, DateTime ExpiresAtUtc) GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    string HashToken(string token);
}

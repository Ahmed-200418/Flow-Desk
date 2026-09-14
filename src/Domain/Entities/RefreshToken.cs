using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Entities;

public class RefreshToken : Entity
{
    public string TokenHash { get; private set; } = default!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = default!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string CreatedByIp { get; private set; } = default!;
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive => RevokedAtUtc == null && DateTime.UtcNow < ExpiresAtUtc;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc != null;

    private RefreshToken() { }

    public RefreshToken(string tokenHash, Guid userId, DateTime expiresAtUtc, string createdByIp)
    {
        TokenHash = tokenHash;
        UserId = userId;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Revoke(string revokedByIp, string? replacedByTokenHash = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}

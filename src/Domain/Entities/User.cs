using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Entities;

public class User : AuditableEntity
{
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string? JobTitle { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LockoutEndUtc { get; private set; }
    public int AccessFailedCount { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    private User() { }

    public User(string email, string passwordHash, string firstName, string lastName, string? jobTitle = null, Guid? departmentId = null)
    {
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        JobTitle = jobTitle?.Trim();
        DepartmentId = departmentId;
        IsActive = true;
        AccessFailedCount = 0;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        AccessFailedCount = 0;
        LockoutEndUtc = null;
    }

    public void UpdateProfile(string firstName, string lastName, string? jobTitle, Guid? departmentId)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        JobTitle = jobTitle?.Trim();
        DepartmentId = departmentId;
    }

    public void RecordFailedLogin(int maxFailedAccessAttempts = 5, int lockoutMinutes = 15)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= maxFailedAccessAttempts)
        {
            LockoutEndUtc = DateTime.UtcNow.AddMinutes(lockoutMinutes);
        }
    }

    public void ResetFailedLogin()
    {
        AccessFailedCount = 0;
        LockoutEndUtc = null;
    }

    public bool IsLockedOut => LockoutEndUtc.HasValue && LockoutEndUtc.Value > DateTime.UtcNow;

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}

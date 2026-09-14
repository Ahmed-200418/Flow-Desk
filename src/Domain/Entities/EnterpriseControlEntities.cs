using FlowDesk.Domain.Common;
using FlowDesk.Domain.Enums;

namespace FlowDesk.Domain.Entities;

public class Delegation : AuditableEntity
{
    public Guid DelegatorUserId { get; private set; }
    public User DelegatorUser { get; private set; } = default!;

    public Guid DelegateeUserId { get; private set; }
    public User DelegateeUser { get; private set; } = default!;

    public DateTime StartDateUtc { get; private set; }
    public DateTime EndDateUtc { get; private set; }

    public Guid? RequestTypeId { get; private set; }
    public RequestType? RequestType { get; private set; }

    public bool IsActive { get; private set; } = true;
    public string Reason { get; private set; } = default!;

    private Delegation() { }

    public Delegation(
        Guid delegatorUserId,
        Guid delegateeUserId,
        DateTime startDateUtc,
        DateTime endDateUtc,
        string reason,
        Guid? requestTypeId = null)
    {
        if (delegatorUserId == delegateeUserId)
        {
            throw new DomainException("Self-delegation is not allowed.");
        }

        if (endDateUtc <= startDateUtc)
        {
            throw new DomainException("Delegation EndDate must be strictly after StartDate.");
        }

        DelegatorUserId = delegatorUserId;
        DelegateeUserId = delegateeUserId;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        Reason = reason.Trim();
        RequestTypeId = requestTypeId;
        IsActive = true;
    }

    public void Cancel()
    {
        IsActive = false;
    }
}

public class Notification : Entity
{
    public Guid RecipientUserId { get; private set; }
    public User RecipientUser { get; private set; } = default!;

    public string Title { get; private set; } = default!;
    public string Message { get; private set; } = default!;
    public NotificationType Type { get; private set; } = NotificationType.InApp;

    public bool IsRead { get; private set; } = false;
    public DateTime? ReadAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private Notification() { }

    public Notification(Guid recipientUserId, string title, string message, NotificationType type = NotificationType.InApp)
    {
        RecipientUserId = recipientUserId;
        Title = title.Trim();
        Message = message.Trim();
        Type = type;
        IsRead = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsRead()
    {
        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
    }
}

public class AuditLog : Entity
{
    public Guid? UserId { get; private set; }
    public string? UserEmail { get; private set; }

    public string Action { get; private set; } = default!;
    public string EntityName { get; private set; } = default!;
    public string EntityId { get; private set; } = default!;

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public string? OldValuesJson { get; private set; }
    public string? NewValuesJson { get; private set; }

    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    private AuditLog() { }

    public AuditLog(
        string action,
        string entityName,
        string entityId,
        Guid? userId = null,
        string? userEmail = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? oldValuesJson = null,
        string? newValuesJson = null)
    {
        Action = action.Trim();
        EntityName = entityName.Trim();
        EntityId = entityId.Trim();
        UserId = userId;
        UserEmail = userEmail?.Trim();
        IpAddress = ipAddress?.Trim();
        UserAgent = userAgent?.Trim();
        OldValuesJson = oldValuesJson;
        NewValuesJson = newValuesJson;
        TimestampUtc = DateTime.UtcNow;
    }
}

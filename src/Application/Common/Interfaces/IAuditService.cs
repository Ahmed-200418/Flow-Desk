namespace FlowDesk.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string entityId,
        Guid? userId = null,
        string? userEmail = null,
        string? ipAddress = null,
        string? userAgent = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
}

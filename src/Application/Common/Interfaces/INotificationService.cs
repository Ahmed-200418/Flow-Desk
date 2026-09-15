using FlowDesk.Domain.Enums;

namespace FlowDesk.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(
        Guid recipientUserId,
        string title,
        string message,
        NotificationType type = NotificationType.InApp,
        string? emailBody = null,
        CancellationToken cancellationToken = default);

    Task SendNotificationToGroupAsync(
        IEnumerable<Guid> recipientUserIds,
        string title,
        string message,
        NotificationType type = NotificationType.InApp,
        string? emailBody = null,
        CancellationToken cancellationToken = default);
}

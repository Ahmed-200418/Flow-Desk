using FlowDesk.Domain.Enums;

namespace FlowDesk.Application.Common.Interfaces;

public interface IBackgroundJobService
{
    void EnqueueNotification(Guid recipientUserId, string title, string message, NotificationType type, string? emailBody = null);

    Task ExecuteApprovalRemindersAsync(CancellationToken cancellationToken = default);

    Task ExecuteCheckExpiredApprovalsAsync(CancellationToken cancellationToken = default);

    Task ExecuteProcessEscalationsAsync(CancellationToken cancellationToken = default);

    Task ExecuteGenerateScheduledReportsAsync(CancellationToken cancellationToken = default);

    Task ExecuteCleanupTemporaryFilesAsync(CancellationToken cancellationToken = default);
}

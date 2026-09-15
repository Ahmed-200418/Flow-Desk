using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowDesk.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IApplicationDbContext context,
        IEmailSender emailSender,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task SendNotificationAsync(
        Guid recipientUserId,
        string title,
        string message,
        NotificationType type = NotificationType.InApp,
        string? emailBody = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == recipientUserId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Cannot send notification: Recipient User {UserId} not found.", recipientUserId);
            return;
        }

        // Save In-App Notification entity
        var notification = new Notification(recipientUserId, title, message, type);
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("In-app notification saved for user {UserId} ({Email}) - Title: {Title}", recipientUserId, user.Email, title);

        // Send Email if Email address is present and NotificationType is Email or Approval notification
        if (!string.IsNullOrWhiteSpace(user.Email) && (type == NotificationType.Email || type == NotificationType.ApprovalRequired || type == NotificationType.ApprovalResult || type == NotificationType.Escalation))
        {
            var body = emailBody ?? $"<p>{message}</p>";
            var html = $$"""
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body { font-family: Arial, sans-serif; color: #333; line-height: 1.6; }
                        .container { max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; }
                        .header { background-color: #0f172a; color: #ffffff; padding: 15px; border-top-left-radius: 8px; border-top-right-radius: 8px; text-align: center; }
                        .content { padding: 20px; background-color: #ffffff; }
                        .footer { text-align: center; padding: 10px; font-size: 12px; color: #777; }
                    </style>
                </head>
                <body>
                    <div class="container">
                        <div class="header">
                            <h2>FlowDesk Enterprise Notification</h2>
                        </div>
                        <div class="content">
                            <h3>{{title}}</h3>
                            {{body}}
                        </div>
                        <div class="footer">
                            <p>This is an automated notification from FlowDesk Enterprise Platform.</p>
                        </div>
                    </div>
                </body>
                </html>
                """;

            await _emailSender.SendEmailAsync(user.Email, $"[FlowDesk] {title}", html, cancellationToken);
        }
    }

    public async Task SendNotificationToGroupAsync(
        IEnumerable<Guid> recipientUserIds,
        string title,
        string message,
        NotificationType type = NotificationType.InApp,
        string? emailBody = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in recipientUserIds)
        {
            await SendNotificationAsync(userId, title, message, type, emailBody, cancellationToken);
        }
    }
}

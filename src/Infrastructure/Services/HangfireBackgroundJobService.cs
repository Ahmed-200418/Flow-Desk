using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Sla;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowDesk.Infrastructure.Services;

public class HangfireBackgroundJobService : IBackgroundJobService
{
    private readonly IBackgroundJobClient _jobClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HangfireBackgroundJobService> _logger;

    public HangfireBackgroundJobService(
        IBackgroundJobClient jobClient,
        IServiceProvider serviceProvider,
        ILogger<HangfireBackgroundJobService> logger)
    {
        _jobClient = jobClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void EnqueueNotification(Guid recipientUserId, string title, string message, NotificationType type, string? emailBody = null)
    {
        _jobClient.Enqueue<INotificationService>(s => s.SendNotificationAsync(recipientUserId, title, message, type, emailBody, CancellationToken.None));
    }

    public async Task ExecuteApprovalRemindersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing background job: SendApprovalReminder");
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new ProcessSlaAndEscalationsCommand(), cancellationToken);
        _logger.LogInformation("SendApprovalReminder job completed. Reminders sent: {RemindersSent}", result.RemindersSent);
    }

    public async Task ExecuteCheckExpiredApprovalsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing background job: CheckExpiredApprovals");
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;
        var expiredInstances = await dbContext.ApprovalInstances
            .Include(ai => ai.Request)
            .Where(ai => ai.Status == ApprovalStatus.Pending && ai.DueAtUtc < now)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("CheckExpiredApprovals completed. Found {Count} overdue/expired pending approval instances.", expiredInstances.Count);
    }

    public async Task ExecuteProcessEscalationsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing background job: ProcessEscalations");
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new ProcessSlaAndEscalationsCommand(), cancellationToken);
        _logger.LogInformation("ProcessEscalations job completed. Escalations processed: {EscalationsProcessed}", result.EscalationsProcessed);
    }

    public async Task ExecuteGenerateScheduledReportsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing background job: GenerateScheduledReports");
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var superAdmins = await dbContext.UserRoles
            .Where(ur => ur.Role.Name == "Super Admin" || ur.Role.Name == "Organization Admin")
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        var totalRequests = await dbContext.Requests.CountAsync(cancellationToken);
        var pendingApprovals = await dbContext.ApprovalInstances.CountAsync(ai => ai.Status == ApprovalStatus.Pending, cancellationToken);

        var reportTitle = "Scheduled Daily Workflow Summary Report";
        var reportMessage = $"Daily System Report: Total Requests: {totalRequests}, Pending Approvals: {pendingApprovals}. System status is healthy.";

        await notificationService.SendNotificationToGroupAsync(superAdmins, reportTitle, reportMessage, NotificationType.Email, cancellationToken: cancellationToken);
        _logger.LogInformation("GenerateScheduledReports job completed successfully.");
    }

    public async Task ExecuteCleanupTemporaryFilesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing background job: CleanupTemporaryFiles");
        var tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");

        if (Directory.Exists(tempFolder))
        {
            var files = Directory.GetFiles(tempFolder);
            int deletedCount = 0;
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1))
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file {FilePath}", file);
                    }
                }
            }
            _logger.LogInformation("CleanupTemporaryFiles completed. Cleaned {Count} temporary files.", deletedCount);
        }
        else
        {
            _logger.LogInformation("CleanupTemporaryFiles completed. Temp folder does not exist.");
        }

        await Task.CompletedTask;
    }
}

using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Sla;

public record OverdueApprovalDto(
    Guid ApprovalInstanceId,
    Guid RequestId,
    string RequestNumber,
    string RequestTitle,
    string RequestTypeName,
    string RequesterName,
    Guid? AssignedUserId,
    string? AssignedApproverName,
    int StepNumber,
    DateTime AssignedAtUtc,
    DateTime DueAtUtc,
    double HoursOverdue,
    string SlaStatus,
    bool IsEscalated
);

public record SlaMetricsDto(
    int TotalPending,
    int TotalOnTime,
    int TotalReminder,
    int TotalOverdue,
    int TotalEscalated
);

public record ProcessSlaAndEscalationsResultDto(
    int RemindersSent,
    int OverdueCount,
    int EscalationsProcessed
);

// Process SLA & Escalations Command
public record ProcessSlaAndEscalationsCommand() : IRequest<ProcessSlaAndEscalationsResultDto>;

public class ProcessSlaAndEscalationsCommandHandler : IRequestHandler<ProcessSlaAndEscalationsCommand, ProcessSlaAndEscalationsResultDto>
{
    private readonly IApplicationDbContext _context;

    public ProcessSlaAndEscalationsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProcessSlaAndEscalationsResultDto> Handle(ProcessSlaAndEscalationsCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var pendingApprovals = await _context.ApprovalInstances
            .Include(ai => ai.Request).ThenInclude(r => r.RequesterUser)
            .Include(ai => ai.Request).ThenInclude(r => r.RequestType)
            .Include(ai => ai.AssignedUser).ThenInclude(u => u.Department)
            .Include(ai => ai.Actions)
            .Where(ai => ai.Status == ApprovalStatus.Pending)
            .ToListAsync(cancellationToken);

        int remindersSent = 0;
        int overdueCount = 0;
        int escalationsProcessed = 0;

        foreach (var instance in pendingApprovals)
        {
            var totalDuration = (instance.DueAtUtc - instance.AssignedAtUtc).TotalHours;
            var elapsedHours = (now - instance.AssignedAtUtc).TotalHours;

            // 1. Reminder Check: If 50% or more of SLA has passed and no reminder sent yet
            if (now < instance.DueAtUtc && elapsedHours >= (totalDuration * 0.5))
            {
                if (instance.AssignedUserId.HasValue)
                {
                    var existingReminder = await _context.Notifications
                        .AnyAsync(n => n.RecipientUserId == instance.AssignedUserId.Value &&
                                       n.Type == NotificationType.Reminder &&
                                       n.Message.Contains(instance.Request.RequestNumber), cancellationToken);

                    if (!existingReminder)
                    {
                        var notification = new Notification(
                            instance.AssignedUserId.Value,
                            "SLA Reminder: Approval Pending",
                            $"Reminder: Request {instance.Request.RequestNumber} ({instance.Request.Title}) is pending your approval and due on {instance.DueAtUtc:yyyy-MM-dd HH:mm UTC}.",
                            NotificationType.Reminder);

                        _context.Notifications.Add(notification);
                        remindersSent++;
                    }
                }
            }

            // 2. Overdue & Escalation Check
            if (now > instance.DueAtUtc)
            {
                overdueCount++;

                // Escalation threshold: overdue by more than 24 hours
                var isOverduePastEscalationWindow = (now - instance.DueAtUtc).TotalHours >= 24;

                // Check if already escalated
                var alreadyEscalated = instance.Actions.Any(a => a.Decision == ApprovalDecision.Delegated && a.Comment != null && a.Comment.Contains("Escalated"));

                if (isOverduePastEscalationWindow && !alreadyEscalated)
                {
                    Guid? escalationTargetUserId = await ResolveEscalationTargetAsync(instance, cancellationToken);

                    if (escalationTargetUserId.HasValue && escalationTargetUserId.Value != instance.AssignedUserId)
                    {
                        // Delegate current instance with Escalation reason
                        var escAction = instance.Delegate(escalationTargetUserId.Value, instance.AssignedUserId ?? instance.Request.RequesterUserId, $"Escalated due to SLA timeout exceeding 24 hours.");
                        _context.ApprovalActions.Add(escAction);

                        // Send Escalation Notification to target user
                        var escNotification = new Notification(
                            escalationTargetUserId.Value,
                            "SLA Escalation Required",
                            $"URGENT: Request {instance.Request.RequestNumber} ({instance.Request.Title}) has been escalated to you due to an overdue SLA deadline.",
                            NotificationType.Escalation);

                        _context.Notifications.Add(escNotification);

                        // Audit Log
                        var audit = new AuditLog(
                            action: "EscalateApprovalSla",
                            entityName: nameof(ApprovalInstance),
                            entityId: instance.Id.ToString(),
                            userId: instance.AssignedUserId,
                            newValuesJson: System.Text.Json.JsonSerializer.Serialize(new
                            {
                                instance.RequestId,
                                OriginalApproverId = instance.AssignedUserId,
                                EscalatedToUserId = escalationTargetUserId.Value,
                                instance.DueAtUtc,
                                EscalatedAtUtc = now
                            }));
                        _context.AuditLogs.Add(audit);

                        escalationsProcessed++;
                    }
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ProcessSlaAndEscalationsResultDto(remindersSent, overdueCount, escalationsProcessed);
    }

    private async Task<Guid?> ResolveEscalationTargetAsync(ApprovalInstance instance, CancellationToken cancellationToken)
    {
        // 1. If assigned to a user, check that user's department manager or manager's manager
        if (instance.AssignedUserId.HasValue)
        {
            var assignedUser = await _context.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == instance.AssignedUserId.Value, cancellationToken);

            if (assignedUser?.Department?.ManagerUserId != null && assignedUser.Department.ManagerUserId != assignedUser.Id)
            {
                return assignedUser.Department.ManagerUserId;
            }
        }

        // 2. Fallback to Requester's Department Manager
        var requesterDeptId = instance.Request.DepartmentId ?? instance.Request.RequesterUser?.DepartmentId;
        if (requesterDeptId.HasValue)
        {
            var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Id == requesterDeptId.Value, cancellationToken);
            if (dept?.ManagerUserId != null && dept.ManagerUserId != instance.AssignedUserId)
            {
                return dept.ManagerUserId;
            }
        }

        // 3. Fallback to Super Admin / Organization Admin
        var adminUser = await _context.UserRoles
            .Where(ur => ur.Role.Name == "Super Admin" || ur.Role.Name == "Organization Admin")
            .Select(ur => ur.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return adminUser != Guid.Empty ? adminUser : null;
    }
}

// Get Overdue Approvals Query
public record GetOverdueApprovalsQuery(int PageNumber = 1, int PageSize = 10) : IRequest<PaginatedList<OverdueApprovalDto>>;

public class GetOverdueApprovalsQueryHandler : IRequestHandler<GetOverdueApprovalsQuery, PaginatedList<OverdueApprovalDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOverdueApprovalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<OverdueApprovalDto>> Handle(GetOverdueApprovalsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var query = _context.ApprovalInstances
            .AsNoTracking()
            .Include(ai => ai.Request).ThenInclude(r => r.RequestType)
            .Include(ai => ai.Request).ThenInclude(r => r.RequesterUser)
            .Include(ai => ai.AssignedUser)
            .Include(ai => ai.Actions)
            .Where(ai => ai.Status == ApprovalStatus.Pending && ai.DueAtUtc < now)
            .OrderBy(ai => ai.DueAtUtc);

        var dtoQuery = query.Select(ai => new OverdueApprovalDto(
            ai.Id,
            ai.RequestId,
            ai.Request.RequestNumber,
            ai.Request.Title,
            ai.Request.RequestType.Name,
            ai.Request.RequesterUser.FullName,
            ai.AssignedUserId,
            ai.AssignedUser != null ? ai.AssignedUser.FullName : "Role/Unassigned",
            ai.StepNumber,
            ai.AssignedAtUtc,
            ai.DueAtUtc,
            Math.Round((now - ai.DueAtUtc).TotalHours, 1),
            (now - ai.DueAtUtc).TotalHours >= 24 ? "Escalated / Critical" : "Overdue",
            ai.Actions.Any(a => a.Decision == ApprovalDecision.Delegated && a.Comment != null && a.Comment.Contains("Escalated"))
        ));

        return await PaginatedList<OverdueApprovalDto>.CreateAsync(dtoQuery, request.PageNumber, request.PageSize, cancellationToken);
    }
}

// Get SLA Metrics Query
public record GetSlaMetricsQuery() : IRequest<SlaMetricsDto>;

public class GetSlaMetricsQueryHandler : IRequestHandler<GetSlaMetricsQuery, SlaMetricsDto>
{
    private readonly IApplicationDbContext _context;

    public GetSlaMetricsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SlaMetricsDto> Handle(GetSlaMetricsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var pendingApprovals = await _context.ApprovalInstances
            .AsNoTracking()
            .Include(ai => ai.Actions)
            .Where(ai => ai.Status == ApprovalStatus.Pending)
            .ToListAsync(cancellationToken);

        int totalPending = pendingApprovals.Count;
        int totalOnTime = 0;
        int totalReminder = 0;
        int totalOverdue = 0;
        int totalEscalated = 0;

        foreach (var ai in pendingApprovals)
        {
            var isEscalated = ai.Actions.Any(a => a.Decision == ApprovalDecision.Delegated && a.Comment != null && a.Comment.Contains("Escalated"));

            if (isEscalated)
            {
                totalEscalated++;
            }
            else if (now > ai.DueAtUtc)
            {
                totalOverdue++;
            }
            else
            {
                var totalDuration = (ai.DueAtUtc - ai.AssignedAtUtc).TotalHours;
                var elapsedHours = (now - ai.AssignedAtUtc).TotalHours;

                if (elapsedHours >= (totalDuration * 0.5))
                {
                    totalReminder++;
                }
                else
                {
                    totalOnTime++;
                }
            }
        }

        return new SlaMetricsDto(totalPending, totalOnTime, totalReminder, totalOverdue, totalEscalated);
    }
}

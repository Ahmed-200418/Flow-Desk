using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Reports;

public record EmployeeDashboardReportDto(
    int TotalMyRequests,
    int PendingCount,
    int ApprovedCount,
    int RejectedCount,
    int ReturnedCount,
    List<Domain.Entities.Request> RecentRequests
);

public record ManagerDashboardReportDto(
    int PendingApprovalsCount,
    int ApprovedTodayCount,
    int RejectedTodayCount,
    int OverdueApprovalsCount,
    List<Domain.Entities.ApprovalInstance> RecentPendingApprovals
);

public record DepartmentAnalyticsDto(
    Guid DepartmentId,
    string DepartmentName,
    int TotalRequests,
    int ApprovedRequests,
    int RejectedRequests,
    double AverageApprovalTimeHours
);

public record WorkflowBottleneckDto(
    Guid WorkflowStepId,
    string StepName,
    string WorkflowName,
    int TotalPendingInStep,
    double AverageWaitTimeHours
);

public record SlaComplianceReportDto(
    int TotalCompletedRequests,
    int MetSlaCount,
    int ExceededSlaCount,
    double SlaCompliancePercentage
);

public record AdminDashboardReportDto(
    int TotalRequests,
    int PendingRequests,
    int ApprovedRequests,
    int RejectedRequests,
    double AverageApprovalTimeHours,
    double SlaCompliancePercentage,
    List<DepartmentAnalyticsDto> DepartmentAnalytics,
    List<WorkflowBottleneckDto> WorkflowBottlenecks,
    DateTime GeneratedAtUtc
);

public record GetEmployeeDashboardReportQuery(Guid UserId) : IRequest<EmployeeDashboardReportDto>;

public class GetEmployeeDashboardReportQueryHandler : IRequestHandler<GetEmployeeDashboardReportQuery, EmployeeDashboardReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetEmployeeDashboardReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeDashboardReportDto> Handle(GetEmployeeDashboardReportQuery request, CancellationToken cancellationToken)
    {
        var requests = _context.Requests
            .AsNoTracking()
            .Where(r => r.RequesterUserId == request.UserId);

        var total = await requests.CountAsync(cancellationToken);
        var pending = await requests.CountAsync(r => r.Status == RequestStatus.PendingApproval, cancellationToken);
        var approved = await requests.CountAsync(r => r.Status == RequestStatus.Approved, cancellationToken);
        var rejected = await requests.CountAsync(r => r.Status == RequestStatus.Rejected, cancellationToken);
        var returned = await requests.CountAsync(r => r.Status == RequestStatus.Returned, cancellationToken);

        var recent = await requests
            .Include(r => r.RequestType)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new EmployeeDashboardReportDto(
            total,
            pending,
            approved,
            rejected,
            returned,
            recent
        );
    }
}

public record GetManagerDashboardReportQuery(Guid ManagerUserId) : IRequest<ManagerDashboardReportDto>;

public class GetManagerDashboardReportQueryHandler : IRequestHandler<GetManagerDashboardReportQuery, ManagerDashboardReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetManagerDashboardReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ManagerDashboardReportDto> Handle(GetManagerDashboardReportQuery request, CancellationToken cancellationToken)
    {
        var approvals = _context.ApprovalInstances
            .AsNoTracking()
            .Where(a => a.AssignedUserId == request.ManagerUserId);

        var todayUtc = DateTime.UtcNow.Date;
        var nowUtc = DateTime.UtcNow;

        var pending = await approvals.CountAsync(a => a.Status == ApprovalStatus.Pending, cancellationToken);
        var approvedToday = await approvals.CountAsync(a => a.Status == ApprovalStatus.Approved && a.RespondedAtUtc >= todayUtc, cancellationToken);
        var rejectedToday = await approvals.CountAsync(a => a.Status == ApprovalStatus.Rejected && a.RespondedAtUtc >= todayUtc, cancellationToken);
        var overdue = await approvals.CountAsync(a => a.Status == ApprovalStatus.Pending && a.DueAtUtc < nowUtc, cancellationToken);

        var recentPending = await approvals
            .Include(a => a.Request)
            .ThenInclude(r => r.RequestType)
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderBy(a => a.DueAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new ManagerDashboardReportDto(
            pending,
            approvedToday,
            rejectedToday,
            overdue,
            recentPending
        );
    }
}

public record GetAdminDashboardReportQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? DepartmentId = null
) : IRequest<AdminDashboardReportDto>;

public class GetAdminDashboardReportQueryHandler : IRequestHandler<GetAdminDashboardReportQuery, AdminDashboardReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminDashboardReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardReportDto> Handle(GetAdminDashboardReportQuery request, CancellationToken cancellationToken)
    {
        var requestsQuery = _context.Requests.AsNoTracking();

        if (request.FromDate.HasValue)
        {
            requestsQuery = requestsQuery.Where(r => r.CreatedAtUtc >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            requestsQuery = requestsQuery.Where(r => r.CreatedAtUtc <= request.ToDate.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            requestsQuery = requestsQuery.Where(r => r.DepartmentId == request.DepartmentId.Value);
        }

        var total = await requestsQuery.CountAsync(cancellationToken);
        var pending = await requestsQuery.CountAsync(r => r.Status == RequestStatus.PendingApproval, cancellationToken);
        var approved = await requestsQuery.CountAsync(r => r.Status == RequestStatus.Approved, cancellationToken);
        var rejected = await requestsQuery.CountAsync(r => r.Status == RequestStatus.Rejected, cancellationToken);

        // Average approval time calculation
        var completedRequests = await requestsQuery
            .Where(r => r.Status == RequestStatus.Approved || r.Status == RequestStatus.Rejected)
            .Select(r => new { r.CreatedAtUtc, r.UpdatedAtUtc, r.CompletedAtUtc, r.SubmittedAtUtc })
            .ToListAsync(cancellationToken);

        double avgHours = 0.0;
        int metSla = 0;

        if (completedRequests.Count > 0)
        {
            double totalHours = 0.0;
            foreach (var req in completedRequests)
            {
                var completionTime = req.CompletedAtUtc ?? req.UpdatedAtUtc ?? DateTime.UtcNow;
                var durationHours = Math.Max(0.0, (completionTime - req.CreatedAtUtc).TotalHours);
                totalHours += durationHours;

                if (durationHours <= 48.0)
                {
                    metSla++;
                }
            }

            avgHours = Math.Round(totalHours / completedRequests.Count, 2);
        }

        double slaPercentage = completedRequests.Count > 0 ? Math.Round((double)metSla / completedRequests.Count * 100.0, 1) : 100.0;

        // Department breakdown
        var deptList = await _context.Departments.AsNoTracking().ToListAsync(cancellationToken);
        var departmentAnalytics = new List<DepartmentAnalyticsDto>();

        foreach (var dept in deptList)
        {
            var deptReqs = await _context.Requests
                .AsNoTracking()
                .Where(r => r.DepartmentId == dept.Id)
                .ToListAsync(cancellationToken);

            var deptTotal = deptReqs.Count;
            var deptApproved = deptReqs.Count(r => r.Status == RequestStatus.Approved);
            var deptRejected = deptReqs.Count(r => r.Status == RequestStatus.Rejected);

            double deptAvgHours = 0.0;
            var deptCompleted = deptReqs.Where(r => r.Status == RequestStatus.Approved || r.Status == RequestStatus.Rejected).ToList();
            if (deptCompleted.Count > 0)
            {
                deptAvgHours = Math.Round(deptCompleted.Sum(r => ( (r.CompletedAtUtc ?? r.UpdatedAtUtc ?? DateTime.UtcNow) - r.CreatedAtUtc).TotalHours) / deptCompleted.Count, 2);
            }

            departmentAnalytics.Add(new DepartmentAnalyticsDto(
                dept.Id,
                dept.Name,
                deptTotal,
                deptApproved,
                deptRejected,
                deptAvgHours
            ));
        }

        // Workflow Bottlenecks
        var pendingApprovals = await _context.ApprovalInstances
            .AsNoTracking()
            .Include(a => a.WorkflowStep)
            .Include(a => a.Request)
            .ThenInclude(r => r.RequestType)
            .Where(a => a.Status == ApprovalStatus.Pending)
            .ToListAsync(cancellationToken);

        var bottlenecks = pendingApprovals
            .GroupBy(a => a.WorkflowStepId)
            .Select(g =>
            {
                var first = g.First();
                var stepName = first.WorkflowStep?.StepName ?? "Approval Step";
                var avgWait = Math.Round(g.Average(a => (DateTime.UtcNow - a.CreatedAtUtc).TotalHours), 2);

                return new WorkflowBottleneckDto(
                    g.Key,
                    stepName,
                    first.Request?.RequestType?.Name ?? "Standard Workflow",
                    g.Count(),
                    avgWait
                );
            })
            .OrderByDescending(b => b.AverageWaitTimeHours)
            .Take(5)
            .ToList();

        return new AdminDashboardReportDto(
            total,
            pending,
            approved,
            rejected,
            avgHours,
            slaPercentage,
            departmentAnalytics,
            bottlenecks,
            DateTime.UtcNow
        );
    }
}

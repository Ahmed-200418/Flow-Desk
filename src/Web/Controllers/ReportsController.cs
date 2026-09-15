using System.Text;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public ReportsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> Dashboard(DateTime? fromDate = null, DateTime? toDate = null, Guid? departmentId = null)
    {
        ViewData["Title"] = "Executive Reporting & Analytics";
        ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-ddTHH:mm");
        ViewData["ToDate"] = toDate?.ToString("yyyy-MM-ddTHH:mm");
        ViewData["DepartmentId"] = departmentId;

        var userId = _currentUserService.UserId ?? Guid.Empty;

        var employeeReport = await _mediator.Send(new GetEmployeeDashboardReportQuery(userId));
        var managerReport = await _mediator.Send(new GetManagerDashboardReportQuery(userId));
        var adminReport = await _mediator.Send(new GetAdminDashboardReportQuery(fromDate, toDate, departmentId));

        ViewData["EmployeeReport"] = employeeReport;
        ViewData["ManagerReport"] = managerReport;

        return View(adminReport);
    }

    [HttpGet]
    public async Task<IActionResult> Export(DateTime? fromDate = null, DateTime? toDate = null, Guid? departmentId = null)
    {
        var adminReport = await _mediator.Send(new GetAdminDashboardReportQuery(fromDate, toDate, departmentId));

        var csv = new StringBuilder();
        csv.AppendLine("DepartmentId,DepartmentName,TotalRequests,ApprovedRequests,RejectedRequests,AverageApprovalTimeHours");

        foreach (var dept in adminReport.DepartmentAnalytics)
        {
            csv.AppendLine($"{dept.DepartmentId},\"{dept.DepartmentName.Replace("\"", "\"\"")}\",{dept.TotalRequests},{dept.ApprovedRequests},{dept.RejectedRequests},{dept.AverageApprovalTimeHours}");
        }

        var fileName = $"Executive_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }
}

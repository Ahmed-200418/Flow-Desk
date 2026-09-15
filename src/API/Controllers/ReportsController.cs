using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Reports;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class ReportsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public ReportsController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet("employee")]
    public async Task<IActionResult> GetEmployeeReport()
    {
        if (!_currentUserService.UserId.HasValue) return Unauthorized();
        var result = await Sender.Send(new GetEmployeeDashboardReportQuery(_currentUserService.UserId.Value));
        return Ok(result);
    }

    [HttpGet("manager")]
    public async Task<IActionResult> GetManagerReport()
    {
        if (!_currentUserService.UserId.HasValue) return Unauthorized();
        var result = await Sender.Send(new GetManagerDashboardReportQuery(_currentUserService.UserId.Value));
        return Ok(result);
    }

    [HttpGet("admin")]
    [HasPermission(Permissions.Reports.Read)]
    public async Task<IActionResult> GetAdminReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? departmentId = null)
    {
        var result = await Sender.Send(new GetAdminDashboardReportQuery(fromDate, toDate, departmentId));
        return Ok(result);
    }
}

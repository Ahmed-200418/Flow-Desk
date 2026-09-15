using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Sla;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class SlaController : ApiControllerBase
{
    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdueApprovals(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetOverdueApprovalsQuery(pageNumber, pageSize));
        return Ok(result);
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetSlaMetrics()
    {
        var result = await Sender.Send(new GetSlaMetricsQuery());
        return Ok(result);
    }

    [HttpPost("process")]
    [HasPermission(Permissions.Sla.Manage)]
    public async Task<IActionResult> ProcessSlaAndEscalations()
    {
        var result = await Sender.Send(new ProcessSlaAndEscalationsCommand());
        return Ok(result);
    }
}

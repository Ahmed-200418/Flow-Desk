using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Audit;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class AuditLogsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.AuditLogs.Read)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var result = await Sender.Send(new GetAuditLogsQuery(
            PageNumber: pageNumber,
            PageSize: pageSize,
            SearchTerm: searchTerm,
            EntityName: entityName,
            EntityId: entityId,
            UserId: userId,
            Action: action,
            FromDate: fromDate,
            ToDate: toDate));

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.AuditLogs.Read)]
    public async Task<IActionResult> GetAuditLogById(Guid id)
    {
        var result = await Sender.Send(new GetAuditLogByIdQuery(id));
        if (result == null) return NotFound(new { Message = $"Audit log with ID '{id}' not found." });

        return Ok(result);
    }

    [HttpGet("entity/{entityName}/{entityId}")]
    [HasPermission(Permissions.AuditLogs.Read)]
    public async Task<IActionResult> GetEntityAuditTrail(string entityName, string entityId)
    {
        var result = await Sender.Send(new GetEntityAuditTrailQuery(entityName, entityId));
        return Ok(result);
    }

    [HttpGet("stats")]
    [HasPermission(Permissions.AuditLogs.Read)]
    public async Task<IActionResult> GetAuditStats()
    {
        var result = await Sender.Send(new GetAuditStatsQuery());
        return Ok(result);
    }
}

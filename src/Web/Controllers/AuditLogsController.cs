using System.Text;
using FlowDesk.Application.Features.Audit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class AuditLogsController : Controller
{
    private readonly IMediator _mediator;

    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index(
        int page = 1,
        string? search = null,
        string? entityName = null,
        string? entityId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        ViewData["Title"] = "Audit Trail & Compliance";
        ViewData["Search"] = search;
        ViewData["EntityName"] = entityName;
        ViewData["EntityId"] = entityId;
        ViewData["Action"] = action;
        ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-ddTHH:mm");
        ViewData["ToDate"] = toDate?.ToString("yyyy-MM-ddTHH:mm");

        var query = new GetAuditLogsQuery(
            PageNumber: page,
            PageSize: 15,
            SearchTerm: search,
            EntityName: entityName,
            EntityId: entityId,
            Action: action,
            FromDate: fromDate,
            ToDate: toDate);

        var logs = await _mediator.Send(query);
        var stats = await _mediator.Send(new GetAuditStatsQuery());

        ViewData["Stats"] = stats;

        return View(logs);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Audit Log Details";
        var log = await _mediator.Send(new GetAuditLogByIdQuery(id));

        if (log == null)
        {
            TempData["Error"] = $"Audit log record with ID '{id}' was not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(log);
    }

    public async Task<IActionResult> EntityHistory(string entityName, string entityId)
    {
        ViewData["Title"] = $"Audit History - {entityName} #{entityId}";
        ViewData["EntityName"] = entityName;
        ViewData["EntityId"] = entityId;

        var history = await _mediator.Send(new GetEntityAuditTrailQuery(entityName, entityId));
        return View(history);
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        string? search = null,
        string? entityName = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var logs = await _mediator.Send(new GetAuditLogsQuery(
            PageNumber: 1,
            PageSize: 5000,
            SearchTerm: search,
            EntityName: entityName,
            Action: action,
            FromDate: fromDate,
            ToDate: toDate));

        var csv = new StringBuilder();
        csv.AppendLine("TimestampUtc,Id,UserEmail,UserId,Action,EntityName,EntityId,IpAddress,UserAgent");

        foreach (var item in logs.Items)
        {
            var userEmail = EscapeCsv(item.UserEmail);
            var actionStr = EscapeCsv(item.Action);
            var entityNameStr = EscapeCsv(item.EntityName);
            var entityIdStr = EscapeCsv(item.EntityId);
            var ip = EscapeCsv(item.IpAddress);
            var ua = EscapeCsv(item.UserAgent);

            csv.AppendLine($"{item.TimestampUtc:O},{item.Id},{userEmail},{item.UserId},{actionStr},{entityNameStr},{entityIdStr},{ip},{ua}");
        }

        var fileName = $"AuditLogs_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

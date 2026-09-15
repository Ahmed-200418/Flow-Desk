using FlowDesk.Application.Features.Sla;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class SlaController : Controller
{
    private readonly IMediator _mediator;

    public SlaController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Dashboard(int page = 1)
    {
        ViewData["Title"] = "SLA & Escalations Control Center";

        var metrics = await _mediator.Send(new GetSlaMetricsQuery());
        var overdueList = await _mediator.Send(new GetOverdueApprovalsQuery(PageNumber: page, PageSize: 10));

        ViewBag.Metrics = metrics;
        return View(overdueList);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process()
    {
        try
        {
            var result = await _mediator.Send(new ProcessSlaAndEscalationsCommand());
            TempData["Success"] = $"SLA check completed: {result.RemindersSent} reminders sent, {result.OverdueCount} overdue tracked, {result.EscalationsProcessed} escalations executed.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Dashboard));
    }
}

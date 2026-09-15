using FlowDesk.Application.Features.Performance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class PerformanceController : Controller
{
    private readonly IMediator _mediator;

    public PerformanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Redis & Performance Optimization";
        var metrics = await _mediator.Send(new GetPerformanceMetricsQuery());
        return View(metrics);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(string? key, string? prefix, bool clearAll = false)
    {
        await _mediator.Send(new ClearCacheCommand(key, prefix, clearAll));
        TempData["Success"] = clearAll ? "Entire cache flushed successfully." : "Target cache entries invalidated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Benchmark(int iterations = 50)
    {
        var result = await _mediator.Send(new RunPerformanceBenchmarkQuery(iterations));
        TempData["Benchmark"] = $"Benchmark Complete ({result.SampleSize} runs): DB Query = {result.DirectDbQueryDurationMs:F3}ms vs Cache Query = {result.CachedQueryDurationMs:F3}ms ({result.SpeedupFactor}x Faster).";
        return RedirectToAction(nameof(Index));
    }
}

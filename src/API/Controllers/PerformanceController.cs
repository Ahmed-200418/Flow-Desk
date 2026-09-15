using Asp.Versioning;
using FlowDesk.Application.Features.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class PerformanceController : ApiControllerBase
{
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var result = await Sender.Send(new GetPerformanceMetricsQuery());
        return Ok(result);
    }

    [HttpGet("cache/stats")]
    public async Task<IActionResult> GetCacheStats()
    {
        var result = await Sender.Send(new GetCacheStatsQuery());
        return Ok(result);
    }

    [HttpPost("cache/clear")]
    public async Task<IActionResult> ClearCache([FromBody] ClearCacheCommand command)
    {
        var success = await Sender.Send(command);
        return Ok(new { Success = success, ClearedAtUtc = DateTime.UtcNow });
    }

    [HttpPost("benchmark")]
    public async Task<IActionResult> RunBenchmark([FromQuery] int iterations = 50)
    {
        var result = await Sender.Send(new RunPerformanceBenchmarkQuery(iterations));
        return Ok(result);
    }
}

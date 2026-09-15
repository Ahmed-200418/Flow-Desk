using System.Diagnostics;
using FlowDesk.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Performance;

public record PerformanceMetricsDto(
    CacheStatsDto CacheStats,
    int TotalDatabaseRequests,
    int TotalDatabaseWorkflows,
    int TotalAuditRecords,
    double EstimatedMemoryUsageMb,
    string DatabaseProvider,
    DateTime MeasuredAtUtc
);

public record BenchmarkResultDto(
    int SampleSize,
    double DirectDbQueryDurationMs,
    double CachedQueryDurationMs,
    double SpeedupFactor,
    double TimeSavedMs,
    DateTime MeasuredAtUtc
);

public record GetPerformanceMetricsQuery : IRequest<PerformanceMetricsDto>;

public class GetPerformanceMetricsQueryHandler : IRequestHandler<GetPerformanceMetricsQuery, PerformanceMetricsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public GetPerformanceMetricsQueryHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<PerformanceMetricsDto> Handle(GetPerformanceMetricsQuery request, CancellationToken cancellationToken)
    {
        var cacheStats = await _cacheService.GetStatsAsync(cancellationToken);
        var reqCount = await _context.Requests.CountAsync(cancellationToken);
        var wfCount = await _context.Workflows.CountAsync(cancellationToken);
        var auditCount = await _context.AuditLogs.CountAsync(cancellationToken);

        var currentProcess = Process.GetCurrentProcess();
        var memoryMb = Math.Round(currentProcess.WorkingSet64 / (1024.0 * 1024.0), 2);

        return new PerformanceMetricsDto(
            CacheStats: cacheStats,
            TotalDatabaseRequests: reqCount,
            TotalDatabaseWorkflows: wfCount,
            TotalAuditRecords: auditCount,
            EstimatedMemoryUsageMb: memoryMb,
            DatabaseProvider: "Microsoft SQL Server",
            MeasuredAtUtc: DateTime.UtcNow
        );
    }
}

public record GetCacheStatsQuery : IRequest<CacheStatsDto>;

public class GetCacheStatsQueryHandler : IRequestHandler<GetCacheStatsQuery, CacheStatsDto>
{
    private readonly ICacheService _cacheService;

    public GetCacheStatsQueryHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<CacheStatsDto> Handle(GetCacheStatsQuery request, CancellationToken cancellationToken)
    {
        return await _cacheService.GetStatsAsync(cancellationToken);
    }
}

public record ClearCacheCommand(string? Key = null, string? Prefix = null, bool ClearAll = false) : IRequest<bool>;

public class ClearCacheCommandHandler : IRequestHandler<ClearCacheCommand, bool>
{
    private readonly ICacheService _cacheService;

    public ClearCacheCommandHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<bool> Handle(ClearCacheCommand request, CancellationToken cancellationToken)
    {
        if (request.ClearAll)
        {
            await _cacheService.ClearAllAsync(cancellationToken);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.Prefix))
        {
            await _cacheService.RemoveByPrefixAsync(request.Prefix.Trim(), cancellationToken);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            await _cacheService.RemoveAsync(request.Key.Trim(), cancellationToken);
            return true;
        }

        return false;
    }
}

public record RunPerformanceBenchmarkQuery(int Iterations = 50) : IRequest<BenchmarkResultDto>;

public class RunPerformanceBenchmarkQueryHandler : IRequestHandler<RunPerformanceBenchmarkQuery, BenchmarkResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public RunPerformanceBenchmarkQueryHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<BenchmarkResultDto> Handle(RunPerformanceBenchmarkQuery request, CancellationToken cancellationToken)
    {
        var iterations = Math.Max(5, Math.Min(request.Iterations, 500));
        var benchmarkKey = "Benchmark_Test_Dataset";

        // Warm up cache
        var sampleData = await _context.RequestTypes.AsNoTracking().ToListAsync(cancellationToken);
        await _cacheService.SetAsync(benchmarkKey, sampleData, TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);

        // 1. Direct DB Query Timing
        var swDb = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var _ = await _context.RequestTypes.AsNoTracking().ToListAsync(cancellationToken);
        }
        swDb.Stop();
        var dbAvgMs = Math.Round(swDb.Elapsed.TotalMilliseconds / iterations, 4);

        // 2. Cached Query Timing
        var swCache = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var _ = await _cacheService.GetAsync<List<Domain.Entities.RequestType>>(benchmarkKey, cancellationToken);
        }
        swCache.Stop();
        var cacheAvgMs = Math.Round(swCache.Elapsed.TotalMilliseconds / iterations, 4);

        if (cacheAvgMs <= 0.0001) cacheAvgMs = 0.001; // Avoid divide by zero

        var speedup = Math.Round(dbAvgMs / cacheAvgMs, 2);
        var timeSaved = Math.Round(Math.Max(0, dbAvgMs - cacheAvgMs), 4);

        return new BenchmarkResultDto(
            SampleSize: iterations,
            DirectDbQueryDurationMs: dbAvgMs,
            CachedQueryDurationMs: cacheAvgMs,
            SpeedupFactor: speedup,
            TimeSavedMs: timeSaved,
            MeasuredAtUtc: DateTime.UtcNow
        );
    }
}

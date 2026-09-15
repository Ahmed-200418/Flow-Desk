using System.Collections.Concurrent;
using System.Text.Json;
using FlowDesk.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace FlowDesk.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private static readonly ConcurrentDictionary<string, byte> KeyRegistry = new();
    private static long _hitCount = 0;
    private static long _missCount = 0;
    private static long _invalidationCount = 0;

    private readonly IDistributedCache _distributedCache;

    public RedisCacheService(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return default;

        try
        {
            var data = await _distributedCache.GetAsync(key, cancellationToken);
            if (data == null || data.Length == 0)
            {
                Interlocked.Increment(ref _missCount);
                return default;
            }

            Interlocked.Increment(ref _hitCount);
            return JsonSerializer.Deserialize<T>(data);
        }
        catch
        {
            Interlocked.Increment(ref _missCount);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null, TimeSpan? unusedExpiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || value == null) return;

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow ?? TimeSpan.FromHours(1),
                SlidingExpiration = unusedExpiration
            };

            await _distributedCache.SetAsync(key, bytes, options, cancellationToken);
            KeyRegistry.TryAdd(key, 0);
        }
        catch
        {
            // Graceful degradation: cache write failures do not block application execution
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
            KeyRegistry.TryRemove(key, out _);
            Interlocked.Increment(ref _invalidationCount);
        }
        catch
        {
            // Fallback gracefully
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return;

        var keysToRemove = KeyRegistry.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            await RemoveAsync(key, cancellationToken);
        }
    }

    public Task<CacheStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var hits = Interlocked.Read(ref _hitCount);
        var misses = Interlocked.Read(ref _missCount);
        var totalReqs = hits + misses;
        var hitRate = totalReqs > 0 ? Math.Round((double)hits / totalReqs * 100.0, 2) : 0.0;
        var keyCount = KeyRegistry.Count;
        var invalidations = Interlocked.Read(ref _invalidationCount);

        var stats = new CacheStatsDto(
            TotalKeys: keyCount,
            HitCount: hits,
            MissCount: misses,
            HitRatePercentage: hitRate,
            InvalidationCount: invalidations,
            EngineStatus: "Active (Distributed Cache Engine)",
            ServerTimeUtc: DateTime.UtcNow
        );

        return Task.FromResult(stats);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var keys = KeyRegistry.Keys.ToList();
        foreach (var key in keys)
        {
            await RemoveAsync(key, cancellationToken);
        }
        KeyRegistry.Clear();
    }
}

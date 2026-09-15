namespace FlowDesk.Application.Common.Interfaces;

public record CacheStatsDto(
    int TotalKeys,
    long HitCount,
    long MissCount,
    double HitRatePercentage,
    long InvalidationCount,
    string EngineStatus,
    DateTime ServerTimeUtc
);

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null, TimeSpan? unusedExpiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    Task<CacheStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
}

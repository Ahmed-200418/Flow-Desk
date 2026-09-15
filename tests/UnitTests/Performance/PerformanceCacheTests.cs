using FlowDesk.Application.Features.Performance;
using FlowDesk.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlowDesk.UnitTests.Performance;

public class PerformanceCacheTests
{
    private static RedisCacheService CreateCacheService()
    {
        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        var distributedCache = new MemoryDistributedCache(memoryOptions);
        return new RedisCacheService(distributedCache);
    }

    [Fact]
    public async Task CacheService_SetAndGet_ReturnsCachedValue()
    {
        // Arrange
        var cache = CreateCacheService();
        var key = "TestKey_123";
        var value = new { Name = "Workflow Engine", Version = 1.0 };

        // Act
        await cache.SetAsync(key, value);
        var result = await cache.GetAsync<dynamic>(key);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CacheService_RemoveByPrefix_EvictsMatchingKeys()
    {
        // Arrange
        var cache = CreateCacheService();
        await cache.SetAsync("Workflow_1", "WF 1");
        await cache.SetAsync("Workflow_2", "WF 2");
        await cache.SetAsync("User_1", "User 1");

        // Act
        await cache.RemoveByPrefixAsync("Workflow_");
        var wf1 = await cache.GetAsync<string>("Workflow_1");
        var user1 = await cache.GetAsync<string>("User_1");

        // Assert
        Assert.Null(wf1);
        Assert.NotNull(user1);
        Assert.Equal("User 1", user1);
    }

    [Fact]
    public async Task CacheService_GetStats_TracksHitsAndMissesCorrectly()
    {
        // Arrange
        var cache = CreateCacheService();
        var key = "SampleStatsKey_" + Guid.NewGuid();

        // Act - 1 Miss
        await cache.GetAsync<string>(key);

        // Act - Set & 2 Hits
        await cache.SetAsync(key, "Data");
        await cache.GetAsync<string>(key);
        await cache.GetAsync<string>(key);

        var stats = await cache.GetStatsAsync();

        // Assert
        Assert.True(stats.HitCount >= 2);
        Assert.True(stats.MissCount >= 1);
        Assert.True(stats.HitRatePercentage > 0);
    }
}

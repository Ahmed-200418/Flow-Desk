using System.Collections.Concurrent;
using FlowDesk.Application.Common.Interfaces;

namespace FlowDesk.Infrastructure.Services;

public class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, DateTime> _cache = new();

    public bool IsProcessed(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return false;

        if (_cache.TryGetValue(idempotencyKey, out var expiry))
        {
            if (DateTime.UtcNow < expiry)
            {
                return true;
            }

            _cache.TryRemove(idempotencyKey, out _);
        }

        return false;
    }

    public void MarkAsProcessed(string idempotencyKey, TimeSpan? expiry = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return;

        var expirationTime = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromHours(24));
        _cache[idempotencyKey] = expirationTime;
    }
}

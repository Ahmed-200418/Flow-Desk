namespace FlowDesk.Application.Common.Interfaces;

public interface IIdempotencyService
{
    bool IsProcessed(string idempotencyKey);
    void MarkAsProcessed(string idempotencyKey, TimeSpan? expiry = null);
}

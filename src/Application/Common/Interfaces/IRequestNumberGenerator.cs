namespace FlowDesk.Application.Common.Interfaces;

public interface IRequestNumberGenerator
{
    Task<string> GenerateRequestNumberAsync(CancellationToken cancellationToken = default);
}

using FlowDesk.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Infrastructure.Services;

public class RequestNumberGenerator : IRequestNumberGenerator
{
    private readonly IApplicationDbContext _context;

    public RequestNumberGenerator(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateRequestNumberAsync(CancellationToken cancellationToken = default)
    {
        var currentYear = DateTime.UtcNow.Year;
        var prefix = $"REQ-{currentYear}-";

        var lastRequestNumber = await _context.Requests
            .AsNoTracking()
            .Where(r => r.RequestNumber.StartsWith(prefix))
            .OrderByDescending(r => r.RequestNumber)
            .Select(r => r.RequestNumber)
            .FirstOrDefaultAsync(cancellationToken);

        int nextSequence = 1;
        if (!string.IsNullOrEmpty(lastRequestNumber))
        {
            var parts = lastRequestNumber.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var currentSeq))
            {
                nextSequence = currentSeq + 1;
            }
        }

        return $"{prefix}{nextSequence:D5}";
    }
}

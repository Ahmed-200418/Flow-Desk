using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Audit;

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string EntityName,
    string EntityId,
    string? IpAddress,
    string? UserAgent,
    DateTime TimestampUtc
);

public record AuditLogDetailDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string EntityName,
    string EntityId,
    string? IpAddress,
    string? UserAgent,
    string? OldValuesJson,
    string? NewValuesJson,
    DateTime TimestampUtc
);

public record AuditStatsDto(
    int TotalLogs,
    int TodayLogs,
    int SecurityEventsCount,
    int RequestEventsCount,
    int WorkflowEventsCount,
    int UniqueUsersCount
);

public record GetAuditLogsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    string? EntityName = null,
    string? EntityId = null,
    Guid? UserId = null,
    string? Action = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
) : IRequest<PaginatedList<AuditLogDto>>;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PaginatedList<AuditLogDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(term) ||
                a.EntityName.ToLower().Contains(term) ||
                a.EntityId.ToLower().Contains(term) ||
                (a.UserEmail != null && a.UserEmail.ToLower().Contains(term)) ||
                (a.IpAddress != null && a.IpAddress.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            query = query.Where(a => a.EntityName.ToLower() == request.EntityName.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(request.EntityId))
        {
            query = query.Where(a => a.EntityId == request.EntityId.Trim());
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(a => a.Action.ToLower().Contains(request.Action.Trim().ToLower()));
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.TimestampUtc <= request.ToDate.Value);
        }

        var orderedQuery = query.OrderByDescending(a => a.TimestampUtc);

        var count = await orderedQuery.CountAsync(cancellationToken);
        var items = await orderedQuery
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                a.UserEmail,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.IpAddress,
                a.UserAgent,
                a.TimestampUtc
            ))
            .ToListAsync(cancellationToken);

        return new PaginatedList<AuditLogDto>(items, count, request.PageNumber, request.PageSize);
    }
}

public record GetAuditLogByIdQuery(Guid Id) : IRequest<AuditLogDetailDto?>;

public class GetAuditLogByIdQueryHandler : IRequestHandler<GetAuditLogByIdQuery, AuditLogDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLogDetailDto?> Handle(GetAuditLogByIdQuery request, CancellationToken cancellationToken)
    {
        var log = await _context.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (log == null) return null;

        return new AuditLogDetailDto(
            log.Id,
            log.UserId,
            log.UserEmail,
            log.Action,
            log.EntityName,
            log.EntityId,
            log.IpAddress,
            log.UserAgent,
            log.OldValuesJson,
            log.NewValuesJson,
            log.TimestampUtc
        );
    }
}

public record GetEntityAuditTrailQuery(string EntityName, string EntityId) : IRequest<List<AuditLogDetailDto>>;

public class GetEntityAuditTrailQueryHandler : IRequestHandler<GetEntityAuditTrailQuery, List<AuditLogDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEntityAuditTrailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AuditLogDetailDto>> Handle(GetEntityAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var entityName = request.EntityName.Trim().ToLower();
        var entityId = request.EntityId.Trim();

        return await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName.ToLower() == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.TimestampUtc)
            .Select(a => new AuditLogDetailDto(
                a.Id,
                a.UserId,
                a.UserEmail,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.IpAddress,
                a.UserAgent,
                a.OldValuesJson,
                a.NewValuesJson,
                a.TimestampUtc
            ))
            .ToListAsync(cancellationToken);
    }
}

public record GetAuditStatsQuery : IRequest<AuditStatsDto>;

public class GetAuditStatsQueryHandler : IRequestHandler<GetAuditStatsQuery, AuditStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetAuditStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuditStatsDto> Handle(GetAuditStatsQuery request, CancellationToken cancellationToken)
    {
        var logs = _context.AuditLogs.AsNoTracking();

        var todayUtc = DateTime.UtcNow.Date;

        var totalLogs = await logs.CountAsync(cancellationToken);
        var todayLogs = await logs.CountAsync(a => a.TimestampUtc >= todayUtc, cancellationToken);

        var securityEvents = await logs.CountAsync(a =>
            a.Action.StartsWith("Auth") ||
            a.Action.StartsWith("User") ||
            a.Action.StartsWith("Role") ||
            a.Action.StartsWith("Permission"), cancellationToken);

        var requestEvents = await logs.CountAsync(a =>
            a.EntityName == "Request" ||
            a.Action.StartsWith("Request"), cancellationToken);

        var workflowEvents = await logs.CountAsync(a =>
            a.EntityName == "Workflow" ||
            a.Action.StartsWith("Workflow"), cancellationToken);

        var uniqueUsers = await logs
            .Where(a => a.UserId.HasValue)
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new AuditStatsDto(
            totalLogs,
            todayLogs,
            securityEvents,
            requestEvents,
            workflowEvents,
            uniqueUsers
        );
    }
}

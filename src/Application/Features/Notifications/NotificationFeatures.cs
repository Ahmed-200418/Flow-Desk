using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Notifications;

public record NotificationDto(
    Guid Id,
    Guid RecipientUserId,
    string Title,
    string Message,
    NotificationType Type,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc
);

// Get My Notifications Query
public record GetMyNotificationsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    bool? OnlyUnread = false
) : IRequest<PaginatedList<NotificationDto>>;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, PaginatedList<NotificationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user required to view notifications.");
        }

        var userId = _currentUserService.UserId.Value;

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId);

        if (request.OnlyUnread.HasValue && request.OnlyUnread.Value)
        {
            query = query.Where(n => !n.IsRead);
        }

        var dtoQuery = query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationDto(
                n.Id,
                n.RecipientUserId,
                n.Title,
                n.Message,
                n.Type,
                n.IsRead,
                n.ReadAtUtc,
                n.CreatedAtUtc));

        return await PaginatedList<NotificationDto>.CreateAsync(dtoQuery, request.PageNumber, request.PageSize, cancellationToken);
    }
}

// Get Unread Count Query
public record GetUnreadCountQuery() : IRequest<int>;

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUnreadCountQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) return 0;
        var userId = _currentUserService.UserId.Value;

        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead, cancellationToken);
    }
}

// Mark Notification as Read Command
public record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();
        var userId = _currentUserService.UserId.Value;

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.RecipientUserId == userId, cancellationToken);

        if (notification == null)
        {
            throw new NotFoundException(nameof(Notification), request.NotificationId);
        }

        if (!notification.IsRead)
        {
            notification.MarkAsRead();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

// Mark All Notifications as Read Command
public record MarkAllNotificationsAsReadCommand() : IRequest;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkAllNotificationsAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();
        var userId = _currentUserService.UserId.Value;

        var unreadNotifications = await _context.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count != 0)
        {
            foreach (var n in unreadNotifications)
            {
                n.MarkAsRead();
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

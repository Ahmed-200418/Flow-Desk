using FlowDesk.Application.Common.Models;
using FlowDesk.Application.Features.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[Authorize]
public class NotificationsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<NotificationDto>>> GetNotifications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] bool? onlyUnread = false)
    {
        var result = await Sender.Send(new GetMyNotificationsQuery(pageNumber, pageSize, onlyUnread));
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var result = await Sender.Send(new GetUnreadCountQuery());
        return Ok(result);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await Sender.Send(new MarkNotificationAsReadCommand(id));
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await Sender.Send(new MarkAllNotificationsAsReadCommand());
        return NoContent();
    }
}

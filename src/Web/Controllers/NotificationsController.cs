using FlowDesk.Application.Features.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int pageNumber = 1, bool? onlyUnread = false)
    {
        var notifications = await _sender.Send(new GetMyNotificationsQuery(pageNumber, 15, onlyUnread));
        ViewBag.OnlyUnread = onlyUnread ?? false;
        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var count = await _sender.Send(new GetUnreadCountQuery());
        return Json(new { count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _sender.Send(new MarkNotificationAsReadCommand(id));
        TempData["Success"] = "Notification marked as read.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _sender.Send(new MarkAllNotificationsAsReadCommand());
        TempData["Success"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index));
    }
}

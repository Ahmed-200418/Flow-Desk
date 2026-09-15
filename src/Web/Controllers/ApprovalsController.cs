using FlowDesk.Application.Features.Approvals;
using FlowDesk.Application.Features.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class ApprovalsController : Controller
{
    private readonly IMediator _mediator;

    public ApprovalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Inbox(int page = 1)
    {
        ViewData["Title"] = "Pending My Approval";
        var pendingList = await _mediator.Send(new GetPendingApprovalsQuery(PageNumber: page, PageSize: 10));
        return View(pendingList);
    }

    [HttpGet]
    public async Task<IActionResult> Action(Guid id)
    {
        ViewData["Title"] = "Review & Approve Request";

        var inbox = await _mediator.Send(new GetPendingApprovalsQuery(1, 100));
        var approvalItem = inbox.Items.FirstOrDefault(x => x.ApprovalInstanceId == id);

        if (approvalItem == null)
        {
            TempData["Error"] = "Approval task not found or already processed.";
            return RedirectToAction(nameof(Inbox));
        }

        var requestDetail = await _mediator.Send(new GetRequestByIdQuery(approvalItem.RequestId));

        ViewBag.ApprovalItem = approvalItem;
        ViewBag.IdempotencyKey = Guid.NewGuid().ToString();

        return View(requestDetail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid requestId, Guid approvalInstanceId, string? comment, string? idempotencyKey)
    {
        try
        {
            await _mediator.Send(new ApproveRequestCommand(requestId, approvalInstanceId, comment, idempotencyKey));
            TempData["Success"] = "Request approved successfully!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Inbox));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid requestId, Guid approvalInstanceId, string comment)
    {
        try
        {
            await _mediator.Send(new RejectRequestCommand(requestId, approvalInstanceId, comment));
            TempData["Success"] = "Request rejected.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Inbox));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(Guid requestId, Guid approvalInstanceId, string comment)
    {
        try
        {
            await _mediator.Send(new ReturnRequestCommand(requestId, approvalInstanceId, comment));
            TempData["Success"] = "Request returned to requester for modifications.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Inbox));
    }
}

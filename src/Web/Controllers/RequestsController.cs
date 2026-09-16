using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Organizations;
using FlowDesk.Application.Features.Requests;
using FlowDesk.Application.Features.RequestTypes;
using FlowDesk.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class RequestsController : Controller
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public RequestsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> Index(RequestStatus? status = null, int page = 1)
    {
        ViewData["Title"] = "My Requests";
        ViewData["CurrentStatus"] = status;

        var requesterUserId = _currentUserService.UserId;
        var result = await _mediator.Send(new GetRequestsQuery(
            PageNumber: page,
            PageSize: 10,
            Status: status,
            RequesterUserId: requesterUserId));

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Create Business Request";

        var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
        var org = orgs.Items.FirstOrDefault();

        List<RequestTypeDto> requestTypes = new();
        if (org != null)
        {
            requestTypes = await _mediator.Send(new GetRequestTypesQuery(org.Id));
            ViewBag.OrganizationId = org.Id;
        }

        ViewBag.RequestTypes = requestTypes;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Guid organizationId,
        Guid requestTypeId,
        string title,
        string description,
        RequestPriority priority = RequestPriority.Medium,
        decimal totalAmount = 0m,
        string currency = "EGP",
        List<string>? itemNames = null,
        List<int>? itemQuantities = null,
        List<decimal>? itemPrices = null)
    {
        try
        {
            List<CreateRequestItemInput>? items = null;
            if (itemNames != null && itemNames.Count > 0)
            {
                items = new List<CreateRequestItemInput>();
                for (int i = 0; i < itemNames.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(itemNames[i]))
                    {
                        var qty = itemQuantities != null && itemQuantities.Count > i ? itemQuantities[i] : 1;
                        var price = itemPrices != null && itemPrices.Count > i ? itemPrices[i] : 0m;
                        items.Add(new CreateRequestItemInput(itemNames[i], qty, price));
                    }
                }
            }

            var request = await _mediator.Send(new CreateRequestCommand(
                organizationId, requestTypeId, title, description, priority, totalAmount, currency, null, items));

            TempData["Success"] = $"Request #{request.RequestNumber} created as Draft.";
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
            var org = orgs.Items.FirstOrDefault();
            ViewBag.OrganizationId = org?.Id ?? Guid.Empty;
            ViewBag.RequestTypes = org != null ? await _mediator.Send(new GetRequestTypesQuery(org.Id)) : new List<RequestTypeDto>();
            return View();
        }
    }

    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Request Details";
        try
        {
            var req = await _mediator.Send(new GetRequestByIdQuery(id));
            return View(req);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id)
    {
        try
        {
            await _mediator.Send(new SubmitRequestCommand(id));
            TempData["Success"] = "Request submitted for approval successfully!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            await _mediator.Send(new CancelRequestCommand(id));
            TempData["Success"] = "Request cancelled.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(Guid id, string content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                await _mediator.Send(new AddCommentCommand(id, content));
                TempData["Success"] = "Comment added.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _mediator.Send(new DeleteRequestCommand(id));
            TempData["Success"] = "Request deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

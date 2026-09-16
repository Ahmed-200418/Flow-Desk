using FlowDesk.Application.Features.Delegations;
using FlowDesk.Application.Features.RequestTypes;
using FlowDesk.Application.Features.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class DelegationsController : Controller
{
    private readonly IMediator _mediator;

    public DelegationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index(bool includeInactive = false)
    {
        ViewData["Title"] = "Approval Delegations";
        ViewData["IncludeInactive"] = includeInactive;

        var myDelegations = await _mediator.Send(new GetMyDelegationsQuery(includeInactive));

        var isUserAdmin = User.IsInRole("Super Admin") || User.IsInRole("Organization Admin");
        if (isUserAdmin)
        {
            ViewBag.AllDelegations = await _mediator.Send(new GetAllDelegationsQuery(PageNumber: 1, PageSize: 50, OnlyActive: !includeInactive));
        }

        return View(myDelegations);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New Approval Delegation";

        var usersList = await _mediator.Send(new GetUsersQuery(PageNumber: 1, PageSize: 100));
        var requestTypes = await _mediator.Send(new GetRequestTypesQuery());

        ViewBag.Users = usersList.Items as IEnumerable<UserSummaryDto>;
        ViewBag.RequestTypes = requestTypes as IEnumerable<RequestTypeDto>;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid delegateeUserId, DateTime startDateUtc, DateTime endDateUtc, string reason, Guid? requestTypeId)
    {
        try
        {
            await _mediator.Send(new CreateDelegationCommand(delegateeUserId, startDateUtc, endDateUtc, reason, requestTypeId));
            TempData["Success"] = "Approval delegation created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;

            ViewData["Title"] = "New Approval Delegation";
            var usersList = await _mediator.Send(new GetUsersQuery(PageNumber: 1, PageSize: 100));
            var requestTypes = await _mediator.Send(new GetRequestTypesQuery());
            ViewBag.Users = usersList.Items as IEnumerable<UserSummaryDto>;
            ViewBag.RequestTypes = requestTypes as IEnumerable<RequestTypeDto>;

            return View();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            await _mediator.Send(new CancelDelegationCommand(id));
            TempData["Success"] = "Delegation cancelled successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        ViewData["Title"] = "Edit Delegation";
        try
        {
            var delegation = await _mediator.Send(new GetDelegationByIdQuery(id));
            return View(delegation);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, DateTime startDateUtc, DateTime endDateUtc, string reason)
    {
        try
        {
            await _mediator.Send(new UpdateDelegationCommand(id, startDateUtc, endDateUtc, reason));
            TempData["Success"] = "Delegation updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var delegation = await _mediator.Send(new GetDelegationByIdQuery(id));
            return View(delegation);
        }
    }
}

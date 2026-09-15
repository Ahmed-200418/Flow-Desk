using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Application.Features.Approvals;
using FlowDesk.Application.Features.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public HomeController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";

        PaginatedList<RequestSummaryDto>? myRequests = null;
        PaginatedList<PendingApprovalSummaryDto>? pendingApprovals = null;

        try
        {
            if (_currentUserService.UserId.HasValue)
            {
                myRequests = await _mediator.Send(new GetRequestsQuery(PageNumber: 1, PageSize: 5, RequesterUserId: _currentUserService.UserId.Value));
            }
        }
        catch { }

        try
        {
            pendingApprovals = await _mediator.Send(new GetPendingApprovalsQuery(PageNumber: 1, PageSize: 5));
        }
        catch { }

        ViewBag.MyRequestsCount = myRequests?.TotalCount ?? 0;
        ViewBag.PendingApprovalsCount = pendingApprovals?.TotalCount ?? 0;
        ViewBag.PendingApprovals = pendingApprovals?.Items ?? new List<PendingApprovalSummaryDto>();
        ViewBag.RecentRequests = myRequests?.Items ?? new List<RequestSummaryDto>();

        return View();
    }
}

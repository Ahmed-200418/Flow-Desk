using FlowDesk.Application.Features.Organizations;
using FlowDesk.Application.Features.RequestTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class RequestTypesController : Controller
{
    private readonly IMediator _mediator;

    public RequestTypesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Request Types";

        var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
        var org = orgs.Items.FirstOrDefault();

        List<RequestTypeDto> requestTypes = new();
        if (org != null)
        {
            requestTypes = await _mediator.Send(new GetRequestTypesQuery(org.Id));
            ViewBag.OrganizationId = org.Id;
        }

        return View(requestTypes);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Create Request Type";

        var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
        ViewBag.Organizations = orgs.Items;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid organizationId, string name, string code, string description, string? icon)
    {
        try
        {
            await _mediator.Send(new CreateRequestTypeCommand(organizationId, name, code, description, icon));
            TempData["Success"] = $"Request Type '{name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
            ViewBag.Organizations = orgs.Items;
            return View();
        }
    }
}

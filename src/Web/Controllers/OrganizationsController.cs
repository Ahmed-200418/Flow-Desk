using FlowDesk.Application.Features.Organizations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class OrganizationsController : Controller
{
    private readonly IMediator _mediator;

    public OrganizationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index(int page = 1, string? search = null)
    {
        ViewData["Title"] = "Organizations";
        ViewData["Search"] = search;

        var result = await _mediator.Send(new GetOrganizationsQuery(PageNumber: page, PageSize: 10, SearchTerm: search));
        return View(result);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create Organization";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string code, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            ModelState.AddModelError(string.Empty, "Name and Code are required.");
            return View();
        }

        try
        {
            await _mediator.Send(new CreateOrganizationCommand(name, code, description));
            TempData["Success"] = $"Organization '{name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Organization Details";
        try
        {
            var org = await _mediator.Send(new GetOrganizationByIdQuery(id));
            return View(org);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        ViewData["Title"] = "Edit Organization";
        try
        {
            var org = await _mediator.Send(new GetOrganizationByIdQuery(id));
            return View(org);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, string name, string? description, bool isActive)
    {
        try
        {
            await _mediator.Send(new UpdateOrganizationCommand(id, name, description, isActive));
            TempData["Success"] = "Organization updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var org = await _mediator.Send(new GetOrganizationByIdQuery(id));
            return View(org);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _mediator.Send(new DeleteOrganizationCommand(id));
            TempData["Success"] = "Organization deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

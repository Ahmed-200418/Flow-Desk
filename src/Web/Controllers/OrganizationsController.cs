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
}

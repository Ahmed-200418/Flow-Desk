using FlowDesk.Application.Features.Departments;
using FlowDesk.Application.Features.Organizations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class DepartmentsController : Controller
{
    private readonly IMediator _mediator;

    public DepartmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index(Guid? organizationId = null)
    {
        ViewData["Title"] = "Departments";

        var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
        ViewBag.Organizations = orgs.Items;

        var selectedOrgId = organizationId ?? orgs.Items.FirstOrDefault()?.Id ?? Guid.Empty;
        ViewBag.SelectedOrgId = selectedOrgId;

        List<DepartmentDto> departments = new();
        if (selectedOrgId != Guid.Empty)
        {
            departments = await _mediator.Send(new GetDepartmentsQuery(selectedOrgId));
        }

        return View(departments);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid organizationId)
    {
        ViewData["Title"] = "Create Department";
        ViewBag.OrganizationId = organizationId;

        var depts = await _mediator.Send(new GetDepartmentsQuery(organizationId));
        ViewBag.Departments = depts;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid organizationId, string name, string code, Guid? parentDepartmentId = null, Guid? managerUserId = null)
    {
        try
        {
            await _mediator.Send(new CreateDepartmentCommand(organizationId, name, code, parentDepartmentId, managerUserId));
            TempData["Success"] = $"Department '{name}' created successfully.";
            return RedirectToAction(nameof(Index), new { organizationId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.OrganizationId = organizationId;
            ViewBag.Departments = await _mediator.Send(new GetDepartmentsQuery(organizationId));
            return View();
        }
    }
}

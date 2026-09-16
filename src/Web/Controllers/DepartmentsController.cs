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

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Department Details";
        try
        {
            var dept = await _mediator.Send(new GetDepartmentByIdQuery(id));
            return View(dept);
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
        ViewData["Title"] = "Edit Department";
        try
        {
            var dept = await _mediator.Send(new GetDepartmentByIdQuery(id));
            ViewBag.Departments = await _mediator.Send(new GetDepartmentsQuery(dept.OrganizationId));
            return View(dept);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, string name, Guid? parentDepartmentId = null, Guid? managerUserId = null)
    {
        try
        {
            var updated = await _mediator.Send(new UpdateDepartmentCommand(id, name, parentDepartmentId, managerUserId));
            TempData["Success"] = "Department updated successfully.";
            return RedirectToAction(nameof(Index), new { organizationId = updated.OrganizationId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var dept = await _mediator.Send(new GetDepartmentByIdQuery(id));
            ViewBag.Departments = await _mediator.Send(new GetDepartmentsQuery(dept.OrganizationId));
            return View(dept);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid organizationId)
    {
        try
        {
            await _mediator.Send(new DeleteDepartmentCommand(id));
            TempData["Success"] = "Department deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { organizationId });
    }
}

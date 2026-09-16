using FlowDesk.Application.Features.Departments;
using FlowDesk.Application.Features.Organizations;
using FlowDesk.Application.Features.Positions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class PositionsController : Controller
{
    private readonly IMediator _mediator;

    public PositionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index(Guid? departmentId = null)
    {
        ViewData["Title"] = "Positions";

        var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
        var org = orgs.Items.FirstOrDefault();

        List<DepartmentDto> departments = new();
        if (org != null)
        {
            departments = await _mediator.Send(new GetDepartmentsQuery(org.Id));
        }

        ViewBag.Departments = departments;
        var selectedDeptId = departmentId ?? departments.FirstOrDefault()?.Id ?? Guid.Empty;
        ViewBag.SelectedDeptId = selectedDeptId;

        List<PositionDto> positions = new();
        if (selectedDeptId != Guid.Empty)
        {
            positions = await _mediator.Send(new GetPositionsByDepartmentQuery(selectedDeptId));
        }

        return View(positions);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid departmentId)
    {
        ViewData["Title"] = "Create Position";
        ViewBag.DepartmentId = departmentId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid departmentId, string title, string code, int level = 1)
    {
        try
        {
            await _mediator.Send(new CreatePositionCommand(departmentId, title, code, level));
            TempData["Success"] = $"Position '{title}' created successfully.";
            return RedirectToAction(nameof(Index), new { departmentId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.DepartmentId = departmentId;
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Position Details";
        try
        {
            var pos = await _mediator.Send(new GetPositionByIdQuery(id));
            return View(pos);
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
        ViewData["Title"] = "Edit Position";
        try
        {
            var pos = await _mediator.Send(new GetPositionByIdQuery(id));
            return View(pos);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, string title, int level = 1)
    {
        try
        {
            var pos = await _mediator.Send(new UpdatePositionCommand(id, title, level));
            TempData["Success"] = "Position updated successfully.";
            return RedirectToAction(nameof(Index), new { departmentId = pos.DepartmentId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var pos = await _mediator.Send(new GetPositionByIdQuery(id));
            return View(pos);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid departmentId)
    {
        try
        {
            await _mediator.Send(new DeletePositionCommand(id));
            TempData["Success"] = "Position deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { departmentId });
    }
}

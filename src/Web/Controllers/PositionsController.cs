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
}

using FlowDesk.Application.Features.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index(int page = 1, string? search = null)
    {
        ViewData["Title"] = "User Directory & Roles";
        ViewData["Search"] = search;

        var result = await _mediator.Send(new GetUsersQuery(PageNumber: page, PageSize: 10, SearchTerm: search));
        return View(result);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create User Account";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, string password, string firstName, string lastName, string? jobTitle)
    {
        try
        {
            await _mediator.Send(new CreateUserCommand(email, password, firstName, lastName, jobTitle));
            TempData["Success"] = $"User '{firstName} {lastName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View();
        }
    }

    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "User Details";
        try
        {
            var user = await _mediator.Send(new GetUserByIdQuery(id));
            return View(user);
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
        ViewData["Title"] = "Edit User";
        try
        {
            var user = await _mediator.Send(new GetUserByIdQuery(id));
            return View(user);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, string firstName, string lastName, string? jobTitle, Guid? departmentId, Guid? positionId)
    {
        try
        {
            await _mediator.Send(new UpdateUserCommand(id, firstName, lastName, jobTitle, departmentId, positionId));
            TempData["Success"] = "User profile updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var user = await _mediator.Send(new GetUserByIdQuery(id));
            return View(user);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        try
        {
            await _mediator.Send(new ToggleUserStatusCommand(id));
            TempData["Success"] = "User status updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

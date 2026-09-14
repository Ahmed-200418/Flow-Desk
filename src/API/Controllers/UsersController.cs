using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Users;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class UsersController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] bool? isActive = null)
    {
        var result = await Sender.Send(new GetUsersQuery(pageNumber, pageSize, searchTerm, departmentId, isActive));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var result = await Sender.Send(new GetUserByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command)
    {
        var result = await Sender.Send(command);
        return CreatedAtAction(nameof(GetUserById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/roles")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> AssignUserRoles(Guid id, [FromBody] List<Guid> roleIds)
    {
        var result = await Sender.Send(new AssignUserRolesCommand(id, roleIds));
        return Ok(result);
    }
}

using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Departments;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class DepartmentsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDepartments([FromQuery] Guid organizationId)
    {
        var result = await Sender.Send(new GetDepartmentsQuery(organizationId));
        return Ok(result);
    }

    [HttpGet("hierarchy")]
    public async Task<IActionResult> GetDepartmentHierarchy([FromQuery] Guid organizationId)
    {
        var result = await Sender.Send(new GetDepartmentHierarchyQuery(organizationId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPost("{id:guid}/manager")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> AssignDepartmentManager(Guid id, [FromBody] Guid managerUserId)
    {
        await Sender.Send(new AssignDepartmentManagerCommand(id, managerUserId));
        return NoContent();
    }
}

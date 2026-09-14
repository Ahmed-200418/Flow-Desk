using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Positions;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class PositionsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPositionsByDepartment([FromQuery] Guid departmentId)
    {
        var result = await Sender.Send(new GetPositionsByDepartmentQuery(departmentId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> CreatePosition([FromBody] CreatePositionCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }
}

using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Delegations;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class DelegationsController : ApiControllerBase
{
    [HttpGet("my")]
    public async Task<IActionResult> GetMyDelegations([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetMyDelegationsQuery(includeInactive));
        return Ok(result);
    }

    [HttpGet]
    [HasPermission(Permissions.Delegation.Manage)]
    public async Task<IActionResult> GetAllDelegations(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool onlyActive = false)
    {
        var result = await Sender.Send(new GetAllDelegationsQuery(pageNumber, pageSize, onlyActive));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDelegation([FromBody] CreateDelegationCommand command)
    {
        var id = await Sender.Send(command);
        return CreatedAtAction(nameof(GetMyDelegations), new { }, new { id });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelDelegation(Guid id)
    {
        await Sender.Send(new CancelDelegationCommand(id));
        return NoContent();
    }
}

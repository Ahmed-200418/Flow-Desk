using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Organizations;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class OrganizationsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOrganizations([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? searchTerm = null)
    {
        var result = await Sender.Send(new GetOrganizationsQuery(pageNumber, pageSize, searchTerm));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrganizationById(Guid id)
    {
        var result = await Sender.Send(new GetOrganizationByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationCommand command)
    {
        var result = await Sender.Send(command);
        return CreatedAtAction(nameof(GetOrganizationById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> UpdateOrganization(Guid id, [FromBody] UpdateOrganizationCommand command)
    {
        if (id != command.Id) return BadRequest("Mismatched Organization ID in route and body.");

        var result = await Sender.Send(command);
        return Ok(result);
    }
}

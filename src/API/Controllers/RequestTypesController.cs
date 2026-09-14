using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.RequestTypes;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class RequestTypesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRequestTypes([FromQuery] Guid organizationId)
    {
        var result = await Sender.Send(new GetRequestTypesQuery(organizationId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> CreateRequestType([FromBody] CreateRequestTypeCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }
}

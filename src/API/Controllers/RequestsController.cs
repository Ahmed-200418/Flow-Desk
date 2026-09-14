using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Requests;
using FlowDesk.Domain.Constants;
using FlowDesk.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class RequestsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRequests(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? requestTypeId = null,
        [FromQuery] RequestStatus? status = null,
        [FromQuery] Guid? requesterUserId = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] RequestPriority? priority = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] DateTime? fromDateUtc = null,
        [FromQuery] DateTime? toDateUtc = null)
    {
        var result = await Sender.Send(new GetRequestsQuery(
            pageNumber, pageSize, searchTerm, requestTypeId, status, requesterUserId, departmentId, priority, minAmount, maxAmount, fromDateUtc, toDateUtc));

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRequestById(Guid id)
    {
        var result = await Sender.Send(new GetRequestByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Purchase.Create)]
    public async Task<IActionResult> CreateRequest([FromBody] CreateRequestCommand command)
    {
        var result = await Sender.Send(command);
        return CreatedAtAction(nameof(GetRequestById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Purchase.Update)]
    public async Task<IActionResult> UpdateRequest(Guid id, [FromBody] UpdateRequestCommand command)
    {
        if (id != command.Id) return BadRequest("Mismatched request ID in route and body.");

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitRequest(Guid id)
    {
        var result = await Sender.Send(new SubmitRequestCommand(id));
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        await Sender.Send(new CancelRequestCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] string content)
    {
        var result = await Sender.Send(new AddCommentCommand(id, content));
        return Ok(result);
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file provided.");

        using var stream = file.OpenReadStream();
        var command = new UploadAttachmentCommand(id, stream, file.FileName, file.ContentType, file.Length);

        var result = await Sender.Send(command);
        return Ok(result);
    }
}

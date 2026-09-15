using Asp.Versioning;
using FlowDesk.API.Controllers;
using FlowDesk.Application.Features.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class ApprovalsController : ApiControllerBase
{
    [HttpGet("inbox")]
    public async Task<IActionResult> GetPendingApprovals([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetPendingApprovalsQuery(pageNumber, pageSize));
        return Ok(result);
    }

    [HttpGet("requests/{requestId:guid}/history")]
    public async Task<IActionResult> GetApprovalHistory(Guid requestId)
    {
        var result = await Sender.Send(new GetApprovalHistoryQuery(requestId));
        return Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveRequest(
        Guid id,
        [FromQuery] Guid requestId,
        [FromBody] string? comment = null,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey = null)
    {
        await Sender.Send(new ApproveRequestCommand(requestId, id, comment, idempotencyKey));
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectRequest(
        Guid id,
        [FromQuery] Guid requestId,
        [FromBody] string comment,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey = null)
    {
        await Sender.Send(new RejectRequestCommand(requestId, id, comment, idempotencyKey));
        return NoContent();
    }

    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> ReturnRequest(
        Guid id,
        [FromQuery] Guid requestId,
        [FromBody] string comment,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey = null)
    {
        await Sender.Send(new ReturnRequestCommand(requestId, id, comment, idempotencyKey));
        return NoContent();
    }

    [HttpPost("{id:guid}/delegate")]
    public async Task<IActionResult> DelegateApproval(
        Guid id,
        [FromQuery] Guid requestId,
        [FromBody] DelegateApprovalInput input)
    {
        await Sender.Send(new DelegateApprovalCommand(requestId, id, input.TargetUserId, input.Reason));
        return NoContent();
    }
}

public record DelegateApprovalInput(Guid TargetUserId, string Reason);

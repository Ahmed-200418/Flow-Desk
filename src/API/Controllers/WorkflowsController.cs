using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.Application.Features.Workflows;
using FlowDesk.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class WorkflowsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetWorkflows(
        [FromQuery] Guid organizationId,
        [FromQuery] Guid? requestTypeId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetWorkflowsQuery(organizationId, requestTypeId, pageNumber, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWorkflowById(Guid id)
    {
        var result = await Sender.Send(new GetWorkflowByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Workflow.Manage)]
    public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowCommand command)
    {
        var result = await Sender.Send(command);
        return CreatedAtAction(nameof(GetWorkflowById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/versions")]
    [HasPermission(Permissions.Workflow.Manage)]
    public async Task<IActionResult> CreateWorkflowVersion(Guid id, [FromQuery] bool copyFromPrevious = true)
    {
        var result = await Sender.Send(new CreateWorkflowVersionCommand(id, copyFromPrevious));
        return Ok(result);
    }

    [HttpPost("versions/{versionId:guid}/publish")]
    [HasPermission(Permissions.Workflow.Publish)]
    public async Task<IActionResult> PublishWorkflowVersion(Guid versionId)
    {
        var result = await Sender.Send(new PublishWorkflowVersionCommand(versionId));
        return Ok(result);
    }

    [HttpPost("versions/{versionId:guid}/archive")]
    [HasPermission(Permissions.Workflow.Manage)]
    public async Task<IActionResult> ArchiveWorkflowVersion(Guid versionId)
    {
        await Sender.Send(new ArchiveWorkflowVersionCommand(versionId));
        return NoContent();
    }

    [HttpPost("versions/{versionId:guid}/steps")]
    [HasPermission(Permissions.Workflow.Manage)]
    public async Task<IActionResult> AddWorkflowStep(Guid versionId, [FromBody] AddWorkflowStepCommand command)
    {
        if (versionId != command.WorkflowVersionId)
        {
            return BadRequest("Mismatched WorkflowVersionId in URL and payload.");
        }

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPut("steps/{stepId:guid}")]
    [HasPermission(Permissions.Workflow.Manage)]
    public async Task<IActionResult> UpdateWorkflowStep(Guid stepId, [FromBody] UpdateWorkflowStepCommand command)
    {
        if (stepId != command.StepId)
        {
            return BadRequest("Mismatched StepId in URL and payload.");
        }

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpDelete("steps/{stepId:guid}")]
    [HasPermission(Permissions.Workflow.Manage)]
    public async Task<IActionResult> DeleteWorkflowStep(Guid stepId)
    {
        await Sender.Send(new DeleteWorkflowStepCommand(stepId));
        return NoContent();
    }

    [HttpPost("steps/{stepId:guid}/conditions")]
    [HasPermission(Permissions.Workflow.Manage)]
    public async Task<IActionResult> AddStepCondition(Guid stepId, [FromBody] AddStepConditionCommand command)
    {
        if (stepId != command.WorkflowStepId)
        {
            return BadRequest("Mismatched StepId in URL and payload.");
        }

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpDelete("conditions/{conditionId:guid}")]
    [HasPermission(Permissions.Workflow.Manage)]
    public async Task<IActionResult> RemoveStepCondition(Guid conditionId)
    {
        await Sender.Send(new RemoveStepConditionCommand(conditionId));
        return NoContent();
    }
}

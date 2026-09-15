using FlowDesk.Application.Common.Models;
using FlowDesk.Application.Features.Organizations;
using FlowDesk.Application.Features.RequestTypes;
using FlowDesk.Application.Features.Workflows;
using FlowDesk.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class WorkflowsController : Controller
{
    private readonly IMediator _mediator;

    public WorkflowsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        ViewData["Title"] = "Workflow Engine Definitions";

        var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
        var org = orgs.Items.FirstOrDefault();

        PaginatedList<WorkflowDto>? result = null;
        if (org != null)
        {
            result = await _mediator.Send(new GetWorkflowsQuery(org.Id, PageNumber: page, PageSize: 10));
            ViewBag.OrganizationId = org.Id;
        }

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Create Workflow Definition";

        var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
        var org = orgs.Items.FirstOrDefault();

        List<RequestTypeDto> requestTypes = new();
        if (org != null)
        {
            requestTypes = await _mediator.Send(new GetRequestTypesQuery(org.Id));
            ViewBag.OrganizationId = org.Id;
        }

        ViewBag.RequestTypes = requestTypes;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid organizationId, Guid requestTypeId, string name, string code, string description)
    {
        try
        {
            var workflow = await _mediator.Send(new CreateWorkflowCommand(organizationId, requestTypeId, name, code, description));
            TempData["Success"] = $"Workflow '{name}' created with draft version 1.";
            return RedirectToAction(nameof(Builder), new { id = workflow.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var orgs = await _mediator.Send(new GetOrganizationsQuery(1, 100));
            var org = orgs.Items.FirstOrDefault();
            ViewBag.OrganizationId = org?.Id ?? Guid.Empty;
            ViewBag.RequestTypes = org != null ? await _mediator.Send(new GetRequestTypesQuery(org.Id)) : new List<RequestTypeDto>();
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Builder(Guid id, Guid? versionId = null)
    {
        ViewData["Title"] = "Workflow Builder";

        try
        {
            var workflow = await _mediator.Send(new GetWorkflowByIdQuery(id));
            ViewBag.Workflow = workflow;

            var selectedVersion = versionId.HasValue
                ? workflow.Versions.FirstOrDefault(v => v.Id == versionId.Value)
                : (workflow.ActiveVersion ?? workflow.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault());

            ViewBag.SelectedVersion = selectedVersion;
            return View(workflow);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVersion(Guid workflowId)
    {
        try
        {
            var newVersion = await _mediator.Send(new CreateWorkflowVersionCommand(workflowId, CopyFromPrevious: true));
            TempData["Success"] = $"Created new draft version #{newVersion.VersionNumber}.";
            return RedirectToAction(nameof(Builder), new { id = workflowId, versionId = newVersion.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Builder), new { id = workflowId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishVersion(Guid workflowId, Guid versionId)
    {
        try
        {
            await _mediator.Send(new PublishWorkflowVersionCommand(versionId));
            TempData["Success"] = "Workflow version published and activated!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Builder), new { id = workflowId, versionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStep(
        Guid workflowId,
        Guid workflowVersionId,
        string stepName,
        ApproverType approverType,
        Guid? approverTargetId,
        StepType stepType = StepType.Sequential,
        bool requireAllApprovers = false,
        int timeoutHours = 48)
    {
        try
        {
            await _mediator.Send(new AddWorkflowStepCommand(
                workflowVersionId, stepName, approverType, approverTargetId, stepType, requireAllApprovers, timeoutHours));
            TempData["Success"] = $"Step '{stepName}' added successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Builder), new { id = workflowId, versionId = workflowVersionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStep(Guid workflowId, Guid workflowVersionId, Guid stepId)
    {
        try
        {
            await _mediator.Send(new DeleteWorkflowStepCommand(stepId));
            TempData["Success"] = "Step removed.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Builder), new { id = workflowId, versionId = workflowVersionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCondition(
        Guid workflowId,
        Guid workflowVersionId,
        Guid workflowStepId,
        string fieldName,
        ConditionOperator operatorType,
        string value,
        string logicGroup = "AND")
    {
        try
        {
            await _mediator.Send(new AddStepConditionCommand(workflowStepId, fieldName, operatorType, value, logicGroup));
            TempData["Success"] = "Rule condition added to step.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Builder), new { id = workflowId, versionId = workflowVersionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCondition(Guid workflowId, Guid workflowVersionId, Guid conditionId)
    {
        try
        {
            await _mediator.Send(new RemoveStepConditionCommand(conditionId));
            TempData["Success"] = "Condition removed.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Builder), new { id = workflowId, versionId = workflowVersionId });
    }
}

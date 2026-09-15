using FluentValidation;
using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Workflows;

// -----------------------------------------------------------------------------
// 1. CREATE WORKFLOW
// -----------------------------------------------------------------------------
public record CreateWorkflowCommand(
    Guid OrganizationId,
    Guid RequestTypeId,
    string Name,
    string Code,
    string Description
) : IRequest<WorkflowDto>;

public class CreateWorkflowCommandValidator : AbstractValidator<CreateWorkflowCommand>
{
    public CreateWorkflowCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.RequestTypeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}

public class CreateWorkflowCommandHandler : IRequestHandler<CreateWorkflowCommand, WorkflowDto>
{
    private readonly IApplicationDbContext _context;

    public CreateWorkflowCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowDto> Handle(CreateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var existing = await _context.Workflows
            .AnyAsync(w => w.OrganizationId == request.OrganizationId && w.Code.ToUpper() == request.Code.Trim().ToUpper(), cancellationToken);

        if (existing)
        {
            throw new DomainException($"Workflow with code '{request.Code}' already exists in this organization.");
        }

        var workflow = new Workflow(
            request.OrganizationId,
            request.RequestTypeId,
            request.Name,
            request.Code,
            request.Description);

        _context.Workflows.Add(workflow);

        // Auto-create initial Draft Version 1
        var initialVersion = new WorkflowVersion(workflow.Id, 1);
        _context.WorkflowVersions.Add(initialVersion);

        await _context.SaveChangesAsync(cancellationToken);

        return new WorkflowDto(
            workflow.Id,
            workflow.OrganizationId,
            workflow.RequestTypeId,
            workflow.Name,
            workflow.Code,
            workflow.Description,
            workflow.IsActive,
            workflow.CreatedAtUtc,
            new WorkflowVersionDto(initialVersion.Id, initialVersion.WorkflowId, initialVersion.VersionNumber, initialVersion.Status, initialVersion.EffectiveFromUtc, initialVersion.EffectiveToUtc, initialVersion.CreatedAtUtc, new List<WorkflowStepDto>()),
            new List<WorkflowVersionDto> {
                new WorkflowVersionDto(initialVersion.Id, initialVersion.WorkflowId, initialVersion.VersionNumber, initialVersion.Status, initialVersion.EffectiveFromUtc, initialVersion.EffectiveToUtc, initialVersion.CreatedAtUtc, new List<WorkflowStepDto>())
            });
    }
}

// -----------------------------------------------------------------------------
// 2. GET WORKFLOWS (QUERY)
// -----------------------------------------------------------------------------
public record GetWorkflowsQuery(
    Guid OrganizationId,
    Guid? RequestTypeId = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PaginatedList<WorkflowDto>>;

public class GetWorkflowsQueryHandler : IRequestHandler<GetWorkflowsQuery, PaginatedList<WorkflowDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWorkflowsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<WorkflowDto>> Handle(GetWorkflowsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Workflows
            .AsNoTracking()
            .Include(w => w.Versions)
                .ThenInclude(v => v.Steps)
                    .ThenInclude(s => s.Conditions)
            .Where(w => w.OrganizationId == request.OrganizationId);

        if (request.RequestTypeId.HasValue)
        {
            query = query.Where(w => w.RequestTypeId == request.RequestTypeId.Value);
        }

        query = query.OrderByDescending(w => w.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(w => MapWorkflowToDto(w)).ToList();

        return new PaginatedList<WorkflowDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }

    public static WorkflowDto MapWorkflowToDto(Workflow w)
    {
        var versionDtos = w.Versions.Select(v => new WorkflowVersionDto(
            v.Id,
            v.WorkflowId,
            v.VersionNumber,
            v.Status,
            v.EffectiveFromUtc,
            v.EffectiveToUtc,
            v.CreatedAtUtc,
            v.Steps.OrderBy(s => s.StepNumber).Select(s => new WorkflowStepDto(
                s.Id,
                s.WorkflowVersionId,
                s.StepNumber,
                s.StepName,
                s.StepType,
                s.ApproverType,
                s.ApproverTargetId,
                s.RequireAllApprovers,
                s.TimeoutHours,
                s.Conditions.Select(c => new WorkflowConditionDto(c.Id, c.WorkflowStepId, c.FieldName, c.Operator, c.Value, c.LogicGroup)).ToList()
            )).ToList()
        )).OrderByDescending(v => v.VersionNumber).ToList();

        var activeVersion = versionDtos.FirstOrDefault(v => v.Status == WorkflowVersionStatus.Published);

        return new WorkflowDto(
            w.Id,
            w.OrganizationId,
            w.RequestTypeId,
            w.Name,
            w.Code,
            w.Description,
            w.IsActive,
            w.CreatedAtUtc,
            activeVersion,
            versionDtos
        );
    }
}

// -----------------------------------------------------------------------------
// 3. GET WORKFLOW BY ID
// -----------------------------------------------------------------------------
public record GetWorkflowByIdQuery(Guid Id) : IRequest<WorkflowDto>;

public class GetWorkflowByIdQueryHandler : IRequestHandler<GetWorkflowByIdQuery, WorkflowDto>
{
    private readonly IApplicationDbContext _context;

    public GetWorkflowByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowDto> Handle(GetWorkflowByIdQuery request, CancellationToken cancellationToken)
    {
        var w = await _context.Workflows
            .AsNoTracking()
            .Include(w => w.Versions)
                .ThenInclude(v => v.Steps)
                    .ThenInclude(s => s.Conditions)
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

        if (w == null) throw new NotFoundException(nameof(Workflow), request.Id);

        return GetWorkflowsQueryHandler.MapWorkflowToDto(w);
    }
}

// -----------------------------------------------------------------------------
// 4. CREATE WORKFLOW VERSION
// -----------------------------------------------------------------------------
public record CreateWorkflowVersionCommand(
    Guid WorkflowId,
    bool CopyFromPrevious = true
) : IRequest<WorkflowVersionDto>;

public class CreateWorkflowVersionCommandHandler : IRequestHandler<CreateWorkflowVersionCommand, WorkflowVersionDto>
{
    private readonly IApplicationDbContext _context;

    public CreateWorkflowVersionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowVersionDto> Handle(CreateWorkflowVersionCommand request, CancellationToken cancellationToken)
    {
        var workflow = await _context.Workflows
            .Include(w => w.Versions)
                .ThenInclude(v => v.Steps)
                    .ThenInclude(s => s.Conditions)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);

        if (workflow == null) throw new NotFoundException(nameof(Workflow), request.WorkflowId);

        var latestVersion = workflow.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        int nextVersionNumber = (latestVersion?.VersionNumber ?? 0) + 1;

        var newVersion = new WorkflowVersion(workflow.Id, nextVersionNumber);
        _context.WorkflowVersions.Add(newVersion);

        if (request.CopyFromPrevious && latestVersion != null)
        {
            foreach (var oldStep in latestVersion.Steps.OrderBy(s => s.StepNumber))
            {
                var newStep = new WorkflowStep(
                    newVersion.Id,
                    oldStep.StepNumber,
                    oldStep.StepName,
                    oldStep.ApproverType,
                    oldStep.ApproverTargetId,
                    oldStep.StepType,
                    oldStep.RequireAllApprovers,
                    oldStep.TimeoutHours);

                _context.WorkflowSteps.Add(newStep);

                foreach (var oldCond in oldStep.Conditions)
                {
                    var newCond = new WorkflowCondition(
                        newStep.Id,
                        oldCond.FieldName,
                        oldCond.Operator,
                        oldCond.Value,
                        oldCond.LogicGroup);

                    _context.WorkflowConditions.Add(newCond);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Fetch reloaded version with steps
        var savedVersion = await _context.WorkflowVersions
            .AsNoTracking()
            .Include(v => v.Steps)
                .ThenInclude(s => s.Conditions)
            .FirstAsync(v => v.Id == newVersion.Id, cancellationToken);

        return new WorkflowVersionDto(
            savedVersion.Id,
            savedVersion.WorkflowId,
            savedVersion.VersionNumber,
            savedVersion.Status,
            savedVersion.EffectiveFromUtc,
            savedVersion.EffectiveToUtc,
            savedVersion.CreatedAtUtc,
            savedVersion.Steps.OrderBy(s => s.StepNumber).Select(s => new WorkflowStepDto(
                s.Id, s.WorkflowVersionId, s.StepNumber, s.StepName, s.StepType, s.ApproverType, s.ApproverTargetId, s.RequireAllApprovers, s.TimeoutHours,
                s.Conditions.Select(c => new WorkflowConditionDto(c.Id, c.WorkflowStepId, c.FieldName, c.Operator, c.Value, c.LogicGroup)).ToList()
            )).ToList());
    }
}

// -----------------------------------------------------------------------------
// 5. PUBLISH WORKFLOW VERSION
// -----------------------------------------------------------------------------
public record PublishWorkflowVersionCommand(Guid VersionId) : IRequest<WorkflowVersionDto>;

public class PublishWorkflowVersionCommandHandler : IRequestHandler<PublishWorkflowVersionCommand, WorkflowVersionDto>
{
    private readonly IApplicationDbContext _context;

    public PublishWorkflowVersionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowVersionDto> Handle(PublishWorkflowVersionCommand request, CancellationToken cancellationToken)
    {
        var targetVersion = await _context.WorkflowVersions
            .Include(v => v.Steps)
                .ThenInclude(s => s.Conditions)
            .FirstOrDefaultAsync(v => v.Id == request.VersionId, cancellationToken);

        if (targetVersion == null) throw new NotFoundException(nameof(WorkflowVersion), request.VersionId);

        if (targetVersion.Steps.Count == 0)
        {
            throw new DomainException("Cannot publish a workflow version with 0 steps. Add at least 1 approval step.");
        }

        // Archive currently published version for the same workflow
        var activePublished = await _context.WorkflowVersions
            .Where(v => v.WorkflowId == targetVersion.WorkflowId && v.Status == WorkflowVersionStatus.Published && v.Id != targetVersion.Id)
            .ToListAsync(cancellationToken);

        foreach (var oldPublished in activePublished)
        {
            oldPublished.Archive();
        }

        targetVersion.Publish();
        await _context.SaveChangesAsync(cancellationToken);

        return new WorkflowVersionDto(
            targetVersion.Id,
            targetVersion.WorkflowId,
            targetVersion.VersionNumber,
            targetVersion.Status,
            targetVersion.EffectiveFromUtc,
            targetVersion.EffectiveToUtc,
            targetVersion.CreatedAtUtc,
            targetVersion.Steps.OrderBy(s => s.StepNumber).Select(s => new WorkflowStepDto(
                s.Id, s.WorkflowVersionId, s.StepNumber, s.StepName, s.StepType, s.ApproverType, s.ApproverTargetId, s.RequireAllApprovers, s.TimeoutHours,
                s.Conditions.Select(c => new WorkflowConditionDto(c.Id, c.WorkflowStepId, c.FieldName, c.Operator, c.Value, c.LogicGroup)).ToList()
            )).ToList());
    }
}

// -----------------------------------------------------------------------------
// 6. ARCHIVE WORKFLOW VERSION
// -----------------------------------------------------------------------------
public record ArchiveWorkflowVersionCommand(Guid VersionId) : IRequest;

public class ArchiveWorkflowVersionCommandHandler : IRequestHandler<ArchiveWorkflowVersionCommand>
{
    private readonly IApplicationDbContext _context;

    public ArchiveWorkflowVersionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ArchiveWorkflowVersionCommand request, CancellationToken cancellationToken)
    {
        var version = await _context.WorkflowVersions.FirstOrDefaultAsync(v => v.Id == request.VersionId, cancellationToken);
        if (version == null) throw new NotFoundException(nameof(WorkflowVersion), request.VersionId);

        version.Archive();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

// -----------------------------------------------------------------------------
// 7. ADD WORKFLOW STEP
// -----------------------------------------------------------------------------
public record AddWorkflowStepCommand(
    Guid WorkflowVersionId,
    string StepName,
    ApproverType ApproverType,
    Guid? ApproverTargetId = null,
    StepType StepType = StepType.Sequential,
    bool RequireAllApprovers = false,
    int TimeoutHours = 48
) : IRequest<WorkflowStepDto>;

public class AddWorkflowStepCommandValidator : AbstractValidator<AddWorkflowStepCommand>
{
    public AddWorkflowStepCommandValidator()
    {
        RuleFor(x => x.WorkflowVersionId).NotEmpty();
        RuleFor(x => x.StepName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TimeoutHours).GreaterThan(0);
    }
}

public class AddWorkflowStepCommandHandler : IRequestHandler<AddWorkflowStepCommand, WorkflowStepDto>
{
    private readonly IApplicationDbContext _context;

    public AddWorkflowStepCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowStepDto> Handle(AddWorkflowStepCommand request, CancellationToken cancellationToken)
    {
        var version = await _context.WorkflowVersions
            .Include(v => v.Steps)
            .FirstOrDefaultAsync(v => v.Id == request.WorkflowVersionId, cancellationToken);

        if (version == null) throw new NotFoundException(nameof(WorkflowVersion), request.WorkflowVersionId);

        if (version.Status != WorkflowVersionStatus.Draft)
        {
            throw new DomainException($"Cannot modify steps on a workflow version in status '{version.Status}'. Only Draft versions can be edited.");
        }

        int nextStepNumber = (version.Steps.Max(s => (int?)s.StepNumber) ?? 0) + 1;

        var step = new WorkflowStep(
            version.Id,
            nextStepNumber,
            request.StepName,
            request.ApproverType,
            request.ApproverTargetId,
            request.StepType,
            request.RequireAllApprovers,
            request.TimeoutHours);

        _context.WorkflowSteps.Add(step);
        await _context.SaveChangesAsync(cancellationToken);

        return new WorkflowStepDto(
            step.Id,
            step.WorkflowVersionId,
            step.StepNumber,
            step.StepName,
            step.StepType,
            step.ApproverType,
            step.ApproverTargetId,
            step.RequireAllApprovers,
            step.TimeoutHours,
            new List<WorkflowConditionDto>());
    }
}

// -----------------------------------------------------------------------------
// 8. UPDATE WORKFLOW STEP
// -----------------------------------------------------------------------------
public record UpdateWorkflowStepCommand(
    Guid StepId,
    string StepName,
    ApproverType ApproverType,
    Guid? ApproverTargetId = null,
    StepType StepType = StepType.Sequential,
    bool RequireAllApprovers = false,
    int TimeoutHours = 48
) : IRequest<WorkflowStepDto>;

public class UpdateWorkflowStepCommandHandler : IRequestHandler<UpdateWorkflowStepCommand, WorkflowStepDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateWorkflowStepCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowStepDto> Handle(UpdateWorkflowStepCommand request, CancellationToken cancellationToken)
    {
        var step = await _context.WorkflowSteps
            .Include(s => s.WorkflowVersion)
            .Include(s => s.Conditions)
            .FirstOrDefaultAsync(s => s.Id == request.StepId, cancellationToken);

        if (step == null) throw new NotFoundException(nameof(WorkflowStep), request.StepId);

        if (step.WorkflowVersion.Status != WorkflowVersionStatus.Draft)
        {
            throw new DomainException($"Cannot update steps on a workflow version in status '{step.WorkflowVersion.Status}'.");
        }

        // Re-instantiate or set fields via Reflection / updated model if properties are private
        var stepTypeProp = typeof(WorkflowStep).GetProperty(nameof(WorkflowStep.StepName));
        stepTypeProp?.SetValue(step, request.StepName.Trim());

        typeof(WorkflowStep).GetProperty(nameof(WorkflowStep.ApproverType))?.SetValue(step, request.ApproverType);
        typeof(WorkflowStep).GetProperty(nameof(WorkflowStep.ApproverTargetId))?.SetValue(step, request.ApproverTargetId);
        typeof(WorkflowStep).GetProperty(nameof(WorkflowStep.StepType))?.SetValue(step, request.StepType);
        typeof(WorkflowStep).GetProperty(nameof(WorkflowStep.RequireAllApprovers))?.SetValue(step, request.RequireAllApprovers);
        typeof(WorkflowStep).GetProperty(nameof(WorkflowStep.TimeoutHours))?.SetValue(step, request.TimeoutHours);

        await _context.SaveChangesAsync(cancellationToken);

        return new WorkflowStepDto(
            step.Id,
            step.WorkflowVersionId,
            step.StepNumber,
            step.StepName,
            step.StepType,
            step.ApproverType,
            step.ApproverTargetId,
            step.RequireAllApprovers,
            step.TimeoutHours,
            step.Conditions.Select(c => new WorkflowConditionDto(c.Id, c.WorkflowStepId, c.FieldName, c.Operator, c.Value, c.LogicGroup)).ToList());
    }
}

// -----------------------------------------------------------------------------
// 9. DELETE WORKFLOW STEP
// -----------------------------------------------------------------------------
public record DeleteWorkflowStepCommand(Guid StepId) : IRequest;

public class DeleteWorkflowStepCommandHandler : IRequestHandler<DeleteWorkflowStepCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteWorkflowStepCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteWorkflowStepCommand request, CancellationToken cancellationToken)
    {
        var step = await _context.WorkflowSteps
            .Include(s => s.WorkflowVersion)
                .ThenInclude(v => v.Steps)
            .FirstOrDefaultAsync(s => s.Id == request.StepId, cancellationToken);

        if (step == null) throw new NotFoundException(nameof(WorkflowStep), request.StepId);

        if (step.WorkflowVersion.Status != WorkflowVersionStatus.Draft)
        {
            throw new DomainException($"Cannot delete steps from a workflow version in status '{step.WorkflowVersion.Status}'.");
        }

        var version = step.WorkflowVersion;
        _context.WorkflowSteps.Remove(step);

        // Re-sequence remaining steps
        int sequence = 1;
        foreach (var remaining in version.Steps.Where(s => s.Id != step.Id).OrderBy(s => s.StepNumber))
        {
            typeof(WorkflowStep).GetProperty(nameof(WorkflowStep.StepNumber))?.SetValue(remaining, sequence++);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

// -----------------------------------------------------------------------------
// 10. ADD STEP CONDITION
// -----------------------------------------------------------------------------
public record AddStepConditionCommand(
    Guid WorkflowStepId,
    string FieldName,
    ConditionOperator Operator,
    string Value,
    string LogicGroup = "AND"
) : IRequest<WorkflowConditionDto>;

public class AddStepConditionCommandValidator : AbstractValidator<AddStepConditionCommand>
{
    public AddStepConditionCommandValidator()
    {
        RuleFor(x => x.WorkflowStepId).NotEmpty();
        RuleFor(x => x.FieldName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).NotEmpty().MaximumLength(500);
        RuleFor(x => x.LogicGroup).NotEmpty().MaximumLength(10);
    }
}

public class AddStepConditionCommandHandler : IRequestHandler<AddStepConditionCommand, WorkflowConditionDto>
{
    private readonly IApplicationDbContext _context;

    public AddStepConditionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowConditionDto> Handle(AddStepConditionCommand request, CancellationToken cancellationToken)
    {
        var step = await _context.WorkflowSteps
            .Include(s => s.WorkflowVersion)
            .FirstOrDefaultAsync(s => s.Id == request.WorkflowStepId, cancellationToken);

        if (step == null) throw new NotFoundException(nameof(WorkflowStep), request.WorkflowStepId);

        if (step.WorkflowVersion.Status != WorkflowVersionStatus.Draft)
        {
            throw new DomainException($"Cannot add conditions to a step in a workflow version in status '{step.WorkflowVersion.Status}'.");
        }

        var condition = new WorkflowCondition(
            step.Id,
            request.FieldName,
            request.Operator,
            request.Value,
            request.LogicGroup);

        _context.WorkflowConditions.Add(condition);
        await _context.SaveChangesAsync(cancellationToken);

        return new WorkflowConditionDto(
            condition.Id,
            condition.WorkflowStepId,
            condition.FieldName,
            condition.Operator,
            condition.Value,
            condition.LogicGroup);
    }
}

// -----------------------------------------------------------------------------
// 11. REMOVE STEP CONDITION
// -----------------------------------------------------------------------------
public record RemoveStepConditionCommand(Guid ConditionId) : IRequest;

public class RemoveStepConditionCommandHandler : IRequestHandler<RemoveStepConditionCommand>
{
    private readonly IApplicationDbContext _context;

    public RemoveStepConditionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(RemoveStepConditionCommand request, CancellationToken cancellationToken)
    {
        var condition = await _context.WorkflowConditions
            .Include(c => c.WorkflowStep)
                .ThenInclude(s => s.WorkflowVersion)
            .FirstOrDefaultAsync(c => c.Id == request.ConditionId, cancellationToken);

        if (condition == null) throw new NotFoundException(nameof(WorkflowCondition), request.ConditionId);

        if (condition.WorkflowStep.WorkflowVersion.Status != WorkflowVersionStatus.Draft)
        {
            throw new DomainException($"Cannot remove conditions from a step in a workflow version in status '{condition.WorkflowStep.WorkflowVersion.Status}'.");
        }

        _context.WorkflowConditions.Remove(condition);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

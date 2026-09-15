using FlowDesk.Domain.Enums;

namespace FlowDesk.Application.Features.Workflows;

public record WorkflowDto(
    Guid Id,
    Guid OrganizationId,
    Guid RequestTypeId,
    string Name,
    string Code,
    string Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    WorkflowVersionDto? ActiveVersion,
    List<WorkflowVersionDto> Versions
);

public record WorkflowVersionDto(
    Guid Id,
    Guid WorkflowId,
    int VersionNumber,
    WorkflowVersionStatus Status,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    DateTime CreatedAtUtc,
    List<WorkflowStepDto> Steps
);

public record WorkflowStepDto(
    Guid Id,
    Guid WorkflowVersionId,
    int StepNumber,
    string StepName,
    StepType StepType,
    ApproverType ApproverType,
    Guid? ApproverTargetId,
    bool RequireAllApprovers,
    int TimeoutHours,
    List<WorkflowConditionDto> Conditions
);

public record WorkflowConditionDto(
    Guid Id,
    Guid WorkflowStepId,
    string FieldName,
    ConditionOperator Operator,
    string Value,
    string LogicGroup
);

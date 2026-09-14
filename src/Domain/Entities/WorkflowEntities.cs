using FlowDesk.Domain.Common;
using FlowDesk.Domain.Enums;

namespace FlowDesk.Domain.Entities;

public class Workflow : AuditableEntity
{
    public Guid OrganizationId { get; private set; }
    public Organization Organization { get; private set; } = default!;

    public Guid RequestTypeId { get; private set; }
    public RequestType RequestType { get; private set; } = default!;

    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public ICollection<WorkflowVersion> Versions { get; private set; } = new List<WorkflowVersion>();

    private Workflow() { }

    public Workflow(Guid organizationId, Guid requestTypeId, string name, string code, string description)
    {
        OrganizationId = organizationId;
        RequestTypeId = requestTypeId;
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = description.Trim();
        IsActive = true;
    }
}

public class WorkflowVersion : AuditableEntity
{
    public Guid WorkflowId { get; private set; }
    public Workflow Workflow { get; private set; } = default!;

    public int VersionNumber { get; private set; } = 1;
    public WorkflowVersionStatus Status { get; private set; } = WorkflowVersionStatus.Draft;

    public DateTime? EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }

    public ICollection<WorkflowStep> Steps { get; private set; } = new List<WorkflowStep>();
    public ICollection<Request> Requests { get; private set; } = new List<Request>();

    private WorkflowVersion() { }

    public WorkflowVersion(Guid workflowId, int versionNumber)
    {
        WorkflowId = workflowId;
        VersionNumber = versionNumber;
        Status = WorkflowVersionStatus.Draft;
    }

    public void Publish()
    {
        if (Status != WorkflowVersionStatus.Draft)
        {
            throw new DomainException($"Workflow version in status '{Status}' cannot be published.");
        }

        Status = WorkflowVersionStatus.Published;
        EffectiveFromUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = WorkflowVersionStatus.Archived;
        EffectiveToUtc = DateTime.UtcNow;
    }
}

public class WorkflowStep : Entity
{
    public Guid WorkflowVersionId { get; private set; }
    public WorkflowVersion WorkflowVersion { get; private set; } = default!;

    public int StepNumber { get; private set; }
    public string StepName { get; private set; } = default!;
    public StepType StepType { get; private set; } = StepType.Sequential;
    public ApproverType ApproverType { get; private set; }
    public Guid? ApproverTargetId { get; private set; }
    public bool RequireAllApprovers { get; private set; } = false;
    public int TimeoutHours { get; private set; } = 48;

    public ICollection<WorkflowCondition> Conditions { get; private set; } = new List<WorkflowCondition>();
    public ICollection<ApprovalInstance> ApprovalInstances { get; private set; } = new List<ApprovalInstance>();

    private WorkflowStep() { }

    public WorkflowStep(
        Guid workflowVersionId,
        int stepNumber,
        string stepName,
        ApproverType approverType,
        Guid? approverTargetId = null,
        StepType stepType = StepType.Sequential,
        bool requireAllApprovers = false,
        int timeoutHours = 48)
    {
        WorkflowVersionId = workflowVersionId;
        StepNumber = stepNumber;
        StepName = stepName.Trim();
        ApproverType = approverType;
        ApproverTargetId = approverTargetId;
        StepType = stepType;
        RequireAllApprovers = requireAllApprovers;
        TimeoutHours = timeoutHours;
    }
}

public class WorkflowCondition : Entity
{
    public Guid WorkflowStepId { get; private set; }
    public WorkflowStep WorkflowStep { get; private set; } = default!;

    public string FieldName { get; private set; } = default!;
    public ConditionOperator Operator { get; private set; }
    public string Value { get; private set; } = default!;
    public string LogicGroup { get; private set; } = "AND";

    private WorkflowCondition() { }

    public WorkflowCondition(Guid workflowStepId, string fieldName, ConditionOperator op, string value, string logicGroup = "AND")
    {
        WorkflowStepId = workflowStepId;
        FieldName = fieldName.Trim();
        Operator = op;
        Value = value.Trim();
        LogicGroup = logicGroup.Trim().ToUpperInvariant();
    }
}

using FlowDesk.Domain.Common;
using FlowDesk.Domain.Enums;

namespace FlowDesk.Domain.Entities;

public class ApprovalInstance : AuditableEntity
{
    public Guid RequestId { get; private set; }
    public Request Request { get; private set; } = default!;

    public Guid WorkflowStepId { get; private set; }
    public WorkflowStep WorkflowStep { get; private set; } = default!;

    public int StepNumber { get; private set; }

    public Guid? AssignedUserId { get; private set; }
    public User? AssignedUser { get; private set; }

    public Guid? AssignedRoleId { get; private set; }
    public Role? AssignedRole { get; private set; }

    public ApprovalStatus Status { get; private set; } = ApprovalStatus.Pending;

    public DateTime AssignedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime DueAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }

    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();

    public ICollection<ApprovalAction> Actions { get; private set; } = new List<ApprovalAction>();

    private ApprovalInstance() { }

    public ApprovalInstance(
        Guid requestId,
        Guid workflowStepId,
        int stepNumber,
        Guid? assignedUserId,
        Guid? assignedRoleId,
        int timeoutHours = 48)
    {
        RequestId = requestId;
        WorkflowStepId = workflowStepId;
        StepNumber = stepNumber;
        AssignedUserId = assignedUserId;
        AssignedRoleId = assignedRoleId;
        Status = ApprovalStatus.Pending;
        AssignedAtUtc = DateTime.UtcNow;
        DueAtUtc = DateTime.UtcNow.AddHours(timeoutHours);
        RowVersion = Guid.NewGuid().ToByteArray();
    }

    public ApprovalAction RecordDecision(ApprovalDecision decision, Guid actorUserId, string? comment = null)
    {
        if (Status != ApprovalStatus.Pending)
        {
            throw new DomainException($"Cannot process approval action for instance in status '{Status}'.");
        }

        Status = decision switch
        {
            ApprovalDecision.Approved => ApprovalStatus.Approved,
            ApprovalDecision.Rejected => ApprovalStatus.Rejected,
            ApprovalDecision.Returned => ApprovalStatus.Returned,
            ApprovalDecision.Delegated => ApprovalStatus.Delegated,
            _ => throw new ArgumentOutOfRangeException(nameof(decision))
        };

        RespondedAtUtc = DateTime.UtcNow;

        var action = new ApprovalAction(Id, actorUserId, decision, comment);
        Actions.Add(action);
        return action;
    }

    public void Delegate(Guid newAssignedUserId, Guid actorUserId, string reason)
    {
        Status = ApprovalStatus.Delegated;
        RespondedAtUtc = DateTime.UtcNow;

        Actions.Add(new ApprovalAction(Id, actorUserId, ApprovalDecision.Delegated, $"Delegated to user '{newAssignedUserId}': {reason}"));
    }

    public void MarkExpired()
    {
        Status = ApprovalStatus.Expired;
    }
}

public class ApprovalAction : Entity
{
    public Guid ApprovalInstanceId { get; private set; }
    public ApprovalInstance ApprovalInstance { get; private set; } = default!;

    public Guid ActorUserId { get; private set; }
    public User ActorUser { get; private set; } = default!;

    public ApprovalDecision Decision { get; private set; }
    public string? Comment { get; private set; }
    public DateTime RespondedAtUtc { get; private set; } = DateTime.UtcNow;

    private ApprovalAction() { }

    public ApprovalAction(Guid approvalInstanceId, Guid actorUserId, ApprovalDecision decision, string? comment = null)
    {
        ApprovalInstanceId = approvalInstanceId;
        ActorUserId = actorUserId;
        Decision = decision;
        Comment = comment?.Trim();
        RespondedAtUtc = DateTime.UtcNow;
    }
}

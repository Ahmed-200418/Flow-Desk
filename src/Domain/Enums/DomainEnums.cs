namespace FlowDesk.Domain.Enums;

public enum RequestPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

public enum RequestStatus
{
    Draft = 1,
    Submitted = 2,
    PendingApproval = 3,
    Returned = 4,
    Approved = 5,
    Rejected = 6,
    Cancelled = 7,
    Completed = 8
}

public enum WorkflowVersionStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Returned = 4,
    Cancelled = 5,
    Delegated = 6,
    Expired = 7
}

public enum ApprovalDecision
{
    Approved = 1,
    Rejected = 2,
    Returned = 3,
    Delegated = 4
}

public enum StepType
{
    Sequential = 1,
    Parallel = 2
}

public enum ApproverType
{
    User = 1,
    Role = 2,
    Position = 3,
    Manager = 4,
    DepartmentManager = 5
}

public enum ConditionOperator
{
    Equals = 1,
    NotEquals = 2,
    GreaterThan = 3,
    GreaterThanOrEqual = 4,
    LessThan = 5,
    LessThanOrEqual = 6,
    Contains = 7,
    In = 8
}

public enum NotificationType
{
    InApp = 1,
    Email = 2,
    ApprovalRequired = 3,
    ApprovalResult = 4,
    Reminder = 5,
    Escalation = 6
}

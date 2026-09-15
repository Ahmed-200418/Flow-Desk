namespace FlowDesk.Domain.Constants;

public static class Permissions
{
    public static class Purchase
    {
        public const string Create = "Purchase.Create";
        public const string Read = "Purchase.Read";
        public const string Update = "Purchase.Update";
        public const string Approve = "Purchase.Approve";
        public const string Reject = "Purchase.Reject";
    }

    public static class Workflow
    {
        public const string Manage = "Workflow.Manage";
        public const string Publish = "Workflow.Publish";
    }

    public static class Users
    {
        public const string Manage = "Users.Manage";
    }

    public static class Reports
    {
        public const string Read = "Reports.Read";
    }

    public static class AuditLogs
    {
        public const string Read = "AuditLogs.Read";
    }

    public static class Delegation
    {
        public const string Manage = "Delegation.Manage";
        public const string Read = "Delegation.Read";
        public const string Create = "Delegation.Create";
    }

    public static class Sla
    {
        public const string Manage = "Sla.Manage";
        public const string Read = "Sla.Read";
    }

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Purchase.Create,
        Purchase.Read,
        Purchase.Update,
        Purchase.Approve,
        Purchase.Reject,
        Workflow.Manage,
        Workflow.Publish,
        Users.Manage,
        Reports.Read,
        AuditLogs.Read,
        Delegation.Manage,
        Delegation.Read,
        Delegation.Create,
        Sla.Manage,
        Sla.Read
    };
}

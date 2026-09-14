namespace FlowDesk.Domain.Constants;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string Employee = "Employee";
    public const string Manager = "Manager";
    public const string FinanceOfficer = "FinanceOfficer";
    public const string ProcurementOfficer = "ProcurementOfficer";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        SuperAdmin,
        OrganizationAdmin,
        Employee,
        Manager,
        FinanceOfficer,
        ProcurementOfficer
    };
}

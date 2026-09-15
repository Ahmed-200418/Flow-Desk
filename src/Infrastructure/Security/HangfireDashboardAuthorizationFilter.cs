using Hangfire.Dashboard;

namespace FlowDesk.Infrastructure.Security;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext.User?.Identity == null || !httpContext.User.Identity.IsAuthenticated)
        {
            return false;
        }

        var isSuperAdmin = httpContext.User.IsInRole("SuperAdmin");
        var isOrgAdmin = httpContext.User.IsInRole("OrganizationAdmin");
        var hasWorkflowPermission = httpContext.User.HasClaim("permission", "Permissions.Workflow.Manage");

        return isSuperAdmin || isOrgAdmin || hasWorkflowPermission;
    }
}

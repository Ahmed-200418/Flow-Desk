using FlowDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Organization> Organizations { get; }
    DbSet<Department> Departments { get; }
    DbSet<Position> Positions { get; }

    DbSet<RequestType> RequestTypes { get; }
    DbSet<Request> Requests { get; }
    DbSet<RequestItem> RequestItems { get; }
    DbSet<Comment> Comments { get; }
    DbSet<Attachment> Attachments { get; }

    DbSet<Workflow> Workflows { get; }
    DbSet<WorkflowVersion> WorkflowVersions { get; }
    DbSet<WorkflowStep> WorkflowSteps { get; }
    DbSet<WorkflowCondition> WorkflowConditions { get; }

    DbSet<ApprovalInstance> ApprovalInstances { get; }
    DbSet<ApprovalAction> ApprovalActions { get; }

    DbSet<Delegation> Delegations { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

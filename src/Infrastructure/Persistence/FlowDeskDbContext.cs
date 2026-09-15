using System.Reflection;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Infrastructure.Persistence;

public class FlowDeskDbContext : DbContext, IApplicationDbContext
{
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;

    public FlowDeskDbContext(
        DbContextOptions options,
        AuditableEntityInterceptor auditableEntityInterceptor)
        : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();

    public DbSet<RequestType> RequestTypes => Set<RequestType>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<RequestItem> RequestItems => Set<RequestItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowCondition> WorkflowConditions => Set<WorkflowCondition>();

    public DbSet<ApprovalInstance> ApprovalInstances => Set<ApprovalInstance>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();

    public DbSet<Delegation> Delegations => Set<Delegation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        if (Database.IsSqlServer())
        {
            modelBuilder.Entity<Request>().Property(r => r.RowVersion).IsRowVersion();
            modelBuilder.Entity<ApprovalInstance>().Property(ai => ai.RowVersion).IsRowVersion();
        }
        else
        {
            modelBuilder.Entity<Request>().Ignore(r => r.RowVersion);
            modelBuilder.Entity<ApprovalInstance>().Ignore(ai => ai.RowVersion);
        }

        // Performance & Query Optimization Indexes
        modelBuilder.Entity<Request>().HasIndex(r => new { r.Status, r.RequesterUserId });
        modelBuilder.Entity<Request>().HasIndex(r => new { r.DepartmentId, r.CreatedAtUtc });
        modelBuilder.Entity<Request>().HasIndex(r => new { r.RequestTypeId, r.Status });
        modelBuilder.Entity<ApprovalInstance>().HasIndex(a => new { a.AssignedUserId, a.Status });
        modelBuilder.Entity<ApprovalInstance>().HasIndex(a => new { a.RequestId, a.StepNumber });
        modelBuilder.Entity<Workflow>().HasIndex(w => new { w.RequestTypeId, w.IsActive });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.EntityName, a.EntityId });
        modelBuilder.Entity<AuditLog>().HasIndex(a => a.TimestampUtc);
        modelBuilder.Entity<Notification>().HasIndex(n => new { n.RecipientUserId, n.IsRead });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
    }
}

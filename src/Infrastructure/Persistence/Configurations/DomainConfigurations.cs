using FlowDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Name).HasMaxLength(150).IsRequired();
        builder.Property(o => o.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(o => o.Code).IsUnique();
        builder.Property(o => o.Description).HasMaxLength(500);
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(150).IsRequired();
        builder.Property(d => d.Code).HasMaxLength(50).IsRequired();

        builder.HasOne(d => d.Organization)
            .WithMany(o => o.Departments)
            .HasForeignKey(d => d.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ParentDepartment)
            .WithMany(d => d.SubDepartments)
            .HasForeignKey(d => d.ParentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ManagerUser)
            .WithMany()
            .HasForeignKey(d => d.ManagerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();

        builder.HasOne(p => p.Department)
            .WithMany(d => d.Positions)
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RequestTypeConfiguration : IEntityTypeConfiguration<RequestType>
{
    public void Configure(EntityTypeBuilder<RequestType> builder)
    {
        builder.ToTable("RequestTypes");
        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Name).HasMaxLength(150).IsRequired();
        builder.Property(rt => rt.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(rt => new { rt.OrganizationId, rt.Code }).IsUnique();

        builder.HasOne(rt => rt.Organization)
            .WithMany(o => o.RequestTypes)
            .HasForeignKey(rt => rt.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("Requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequestNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.RequestNumber).IsUnique();

        builder.Property(r => r.Title).HasMaxLength(250).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.TotalAmount).HasPrecision(18, 4);
        builder.Property(r => r.Currency).HasMaxLength(10).IsRequired();
        builder.Property(r => r.RowVersion).IsConcurrencyToken();

        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.RequesterUserId);
        builder.HasIndex(r => r.OrganizationId);
        builder.HasIndex(r => new { r.OrganizationId, r.Status });

        builder.HasOne(r => r.RequestType)
            .WithMany(rt => rt.Requests)
            .HasForeignKey(r => r.RequestTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RequesterUser)
            .WithMany()
            .HasForeignKey(r => r.RequesterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Organization)
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Department)
            .WithMany()
            .HasForeignKey(r => r.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.WorkflowVersion)
            .WithMany(wv => wv.Requests)
            .HasForeignKey(r => r.WorkflowVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RequestItemConfiguration : IEntityTypeConfiguration<RequestItem>
{
    public void Configure(EntityTypeBuilder<RequestItem> builder)
    {
        builder.ToTable("RequestItems");
        builder.HasKey(ri => ri.Id);
        builder.Property(ri => ri.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(ri => ri.UnitPrice).HasPrecision(18, 4);
        builder.Ignore(ri => ri.TotalPrice);

        builder.HasOne(ri => ri.Request)
            .WithMany(r => r.Items)
            .HasForeignKey(ri => ri.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Content).HasMaxLength(2000).IsRequired();

        builder.HasOne(c => c.Request)
            .WithMany(r => r.Comments)
            .HasForeignKey(c => c.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.AuthorUser)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.StoredFileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.FilePath).HasMaxLength(500).IsRequired();

        builder.HasOne(a => a.Request)
            .WithMany(r => r.Attachments)
            .HasForeignKey(a => a.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedByUser)
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("Workflows");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).HasMaxLength(150).IsRequired();
        builder.Property(w => w.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(w => new { w.OrganizationId, w.Code }).IsUnique();

        builder.HasOne(w => w.Organization)
            .WithMany(o => o.Workflows)
            .HasForeignKey(w => w.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.RequestType)
            .WithMany(rt => rt.Workflows)
            .HasForeignKey(w => w.RequestTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
    {
        builder.ToTable("WorkflowVersions");
        builder.HasKey(wv => wv.Id);
        builder.HasIndex(wv => new { wv.WorkflowId, wv.VersionNumber }).IsUnique();

        builder.HasOne(wv => wv.Workflow)
            .WithMany(w => w.Versions)
            .HasForeignKey(wv => wv.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("WorkflowSteps");
        builder.HasKey(ws => ws.Id);
        builder.Property(ws => ws.StepName).HasMaxLength(150).IsRequired();
        builder.HasIndex(ws => new { ws.WorkflowVersionId, ws.StepNumber }).IsUnique();

        builder.HasOne(ws => ws.WorkflowVersion)
            .WithMany(wv => wv.Steps)
            .HasForeignKey(ws => ws.WorkflowVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowConditionConfiguration : IEntityTypeConfiguration<WorkflowCondition>
{
    public void Configure(EntityTypeBuilder<WorkflowCondition> builder)
    {
        builder.ToTable("WorkflowConditions");
        builder.HasKey(wc => wc.Id);
        builder.Property(wc => wc.FieldName).HasMaxLength(100).IsRequired();
        builder.Property(wc => wc.Value).HasMaxLength(500).IsRequired();
        builder.Property(wc => wc.LogicGroup).HasMaxLength(10).IsRequired();

        builder.HasOne(wc => wc.WorkflowStep)
            .WithMany(ws => ws.Conditions)
            .HasForeignKey(wc => wc.WorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApprovalInstanceConfiguration : IEntityTypeConfiguration<ApprovalInstance>
{
    public void Configure(EntityTypeBuilder<ApprovalInstance> builder)
    {
        builder.ToTable("ApprovalInstances");
        builder.HasKey(ai => ai.Id);
        builder.Property(ai => ai.RowVersion).IsConcurrencyToken();
        builder.HasIndex(ai => ai.Status);
        builder.HasIndex(ai => ai.AssignedUserId);
        builder.HasIndex(ai => new { ai.RequestId, ai.Status });
        builder.HasIndex(ai => new { ai.AssignedUserId, ai.Status });

        builder.HasOne(ai => ai.Request)
            .WithMany(r => r.ApprovalInstances)
            .HasForeignKey(ai => ai.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ai => ai.WorkflowStep)
            .WithMany(ws => ws.ApprovalInstances)
            .HasForeignKey(ai => ai.WorkflowStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ai => ai.AssignedUser)
            .WithMany()
            .HasForeignKey(ai => ai.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ai => ai.AssignedRole)
            .WithMany()
            .HasForeignKey(ai => ai.AssignedRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalActionConfiguration : IEntityTypeConfiguration<ApprovalAction>
{
    public void Configure(EntityTypeBuilder<ApprovalAction> builder)
    {
        builder.ToTable("ApprovalActions");
        builder.HasKey(aa => aa.Id);
        builder.Property(aa => aa.Comment).HasMaxLength(1000);

        builder.HasOne(aa => aa.ApprovalInstance)
            .WithMany(ai => ai.Actions)
            .HasForeignKey(aa => aa.ApprovalInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(aa => aa.ActorUser)
            .WithMany()
            .HasForeignKey(aa => aa.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> builder)
    {
        builder.ToTable("Delegations");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(d => new { d.DelegatorUserId, d.IsActive });

        builder.HasOne(d => d.DelegatorUser)
            .WithMany()
            .HasForeignKey(d => d.DelegatorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.DelegateeUser)
            .WithMany()
            .HasForeignKey(d => d.DelegateeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.RequestType)
            .WithMany()
            .HasForeignKey(d => d.RequestTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(2000).IsRequired();
        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead });

        builder.HasOne(n => n.RecipientUser)
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(al => al.Id);
        builder.Property(al => al.Action).HasMaxLength(100).IsRequired();
        builder.Property(al => al.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(al => al.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(al => al.UserEmail).HasMaxLength(256);
        builder.Property(al => al.IpAddress).HasMaxLength(50);
        builder.Property(al => al.UserAgent).HasMaxLength(500);

        builder.HasIndex(al => new { al.EntityName, al.EntityId });
        builder.HasIndex(al => al.TimestampUtc);
    }
}

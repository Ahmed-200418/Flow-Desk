using System.Text.Json;
using System.Text.Json.Nodes;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Entities;
using FlowDesk.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FlowDesk.Infrastructure.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditableEntityInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        CreateAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        CreateAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        var userId = _currentUserService.UserId?.ToString() ?? "System";
        var utcNow = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = utcNow;
                entry.Entity.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = utcNow;
                entry.Entity.UpdatedBy = userId;
            }
        }
    }

    private void CreateAuditLogs(DbContext? context)
    {
        if (context == null) return;

        var auditEntries = new List<AuditLog>();
        var userId = _currentUserService.UserId;
        var userEmail = _currentUserService.UserEmail;
        var ipAddress = _currentUserService.IpAddress;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var entityName = entry.Entity.GetType().Name;
            var action = $"{entityName}.{entry.State}";

            var primaryKeyProperty = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            var entityId = primaryKeyProperty?.CurrentValue?.ToString() ?? Guid.NewGuid().ToString();

            Dictionary<string, object?> oldValues = new();
            Dictionary<string, object?> newValues = new();

            foreach (var prop in entry.Properties)
            {
                if (prop.Metadata.IsShadowProperty()) continue;

                var propName = prop.Metadata.Name;

                switch (entry.State)
                {
                    case EntityState.Added:
                        newValues[propName] = prop.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        oldValues[propName] = prop.OriginalValue;
                        break;
                    case EntityState.Modified:
                        if (prop.IsModified)
                        {
                            oldValues[propName] = prop.OriginalValue;
                            newValues[propName] = prop.CurrentValue;
                        }
                        break;
                }
            }

            var oldJson = oldValues.Count > 0 ? AuditService.SerializeAndRedact(oldValues) : null;
            var newJson = newValues.Count > 0 ? AuditService.SerializeAndRedact(newValues) : null;

            var log = new AuditLog(
                action: action,
                entityName: entityName,
                entityId: entityId,
                userId: userId,
                userEmail: userEmail,
                ipAddress: ipAddress,
                userAgent: null,
                oldValuesJson: oldJson,
                newValuesJson: newJson
            );

            auditEntries.Add(log);
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }
    }
}

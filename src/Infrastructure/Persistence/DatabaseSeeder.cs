using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Constants;
using FlowDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowDesk.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly FlowDeskDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        FlowDeskDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.MigrateAsync();
            }
            else
            {
                await _context.Database.EnsureCreatedAsync();
            }

            await SeedPermissionsAsync();
            await SeedRolesAsync();
            await SeedRolePermissionsAsync();
            await SeedSuperAdminUserAsync();

            _context.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task SeedPermissionsAsync()
    {
        var permissionsToSeed = new[]
        {
            new Permission(Permissions.Purchase.Create, "Purchase", "Ability to create purchase requests"),
            new Permission(Permissions.Purchase.Read, "Purchase", "Ability to view purchase requests"),
            new Permission(Permissions.Purchase.Update, "Purchase", "Ability to edit purchase requests"),
            new Permission(Permissions.Purchase.Approve, "Purchase", "Ability to approve purchase requests"),
            new Permission(Permissions.Purchase.Reject, "Purchase", "Ability to reject purchase requests"),
            new Permission(Permissions.Workflow.Manage, "Workflow", "Ability to manage and configure workflow definitions"),
            new Permission(Permissions.Workflow.Publish, "Workflow", "Ability to publish workflow versions"),
            new Permission(Permissions.Users.Manage, "Users", "Ability to manage users, positions, and departments"),
            new Permission(Permissions.Reports.Read, "Reports", "Ability to view system reports and analytics"),
            new Permission(Permissions.AuditLogs.Read, "AuditLogs", "Ability to view audit logs")
        };

        foreach (var p in permissionsToSeed)
        {
            if (!await _context.Permissions.AnyAsync(x => x.Code == p.Code))
            {
                _context.Permissions.Add(p);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedRolesAsync()
    {
        var rolesToSeed = new[]
        {
            new Role(Roles.SuperAdmin, "System Super Administrator with full system capabilities", true),
            new Role(Roles.OrganizationAdmin, "Organization Administrator managing users and workflow definitions", true),
            new Role(Roles.Employee, "Standard employee able to create requests", true),
            new Role(Roles.Manager, "Manager processing team approvals", true),
            new Role(Roles.FinanceOfficer, "Finance officer approving financial workflow steps", true),
            new Role(Roles.ProcurementOfficer, "Procurement officer handling purchase workflows", true)
        };

        foreach (var r in rolesToSeed)
        {
            if (!await _context.Roles.AnyAsync(x => x.Name == r.Name))
            {
                _context.Roles.Add(r);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedRolePermissionsAsync()
    {
        var roles = await _context.Roles.ToListAsync();
        var permissions = await _context.Permissions.ToListAsync();

        var superAdminRole = roles.First(r => r.Name == Roles.SuperAdmin);
        var orgAdminRole = roles.First(r => r.Name == Roles.OrganizationAdmin);
        var employeeRole = roles.First(r => r.Name == Roles.Employee);
        var managerRole = roles.First(r => r.Name == Roles.Manager);
        var financeRole = roles.First(r => r.Name == Roles.FinanceOfficer);
        var procurementRole = roles.First(r => r.Name == Roles.ProcurementOfficer);

        // SuperAdmin gets all permissions
        foreach (var p in permissions)
        {
            AddRolePermissionIfNotExists(superAdminRole.Id, p.Id);
        }

        // OrgAdmin gets users, workflows, reports, audit logs, read purchase
        foreach (var p in permissions.Where(x => x.Category is "Workflow" or "Users" or "Reports" or "AuditLogs" || x.Code == Permissions.Purchase.Read))
        {
            AddRolePermissionIfNotExists(orgAdminRole.Id, p.Id);
        }

        // Employee gets Purchase.Create, Purchase.Read, Purchase.Update
        foreach (var p in permissions.Where(x => x.Code is Permissions.Purchase.Create or Permissions.Purchase.Read or Permissions.Purchase.Update))
        {
            AddRolePermissionIfNotExists(employeeRole.Id, p.Id);
        }

        // Manager, Finance, Procurement get Purchase.Read, Purchase.Approve, Purchase.Reject
        foreach (var p in permissions.Where(x => x.Code is Permissions.Purchase.Read or Permissions.Purchase.Approve or Permissions.Purchase.Reject))
        {
            AddRolePermissionIfNotExists(managerRole.Id, p.Id);
            AddRolePermissionIfNotExists(financeRole.Id, p.Id);
            AddRolePermissionIfNotExists(procurementRole.Id, p.Id);
        }

        await _context.SaveChangesAsync();
    }

    private void AddRolePermissionIfNotExists(Guid roleId, Guid permissionId)
    {
        if (!_context.RolePermissions.Any(rp => rp.RoleId == roleId && rp.PermissionId == permissionId))
        {
            _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }
    }

    private async Task SeedSuperAdminUserAsync()
    {
        const string superAdminEmail = "admin@flowdesk.local";
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == superAdminEmail);

        if (existingUser == null)
        {
            var superAdminRole = await _context.Roles.FirstAsync(r => r.Name == Roles.SuperAdmin);
            var passwordHash = _passwordHasher.HashPassword("SuperAdmin@FlowDesk2026!");

            var user = new User(
                superAdminEmail,
                passwordHash,
                "Super",
                "Admin",
                "System Administrator");

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = superAdminRole.Id });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Super Admin user created successfully with email: {Email}", superAdminEmail);
        }
    }
}

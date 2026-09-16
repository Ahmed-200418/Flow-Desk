using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Constants;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowDesk.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly FlowDeskDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly IHostEnvironment _environment;

    public DatabaseSeeder(
        FlowDeskDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<DatabaseSeeder> logger,
        IHostEnvironment environment)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _environment = environment;
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
            await SeedComprehensiveDemoDataAsync();

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
            new Permission(Permissions.AuditLogs.Read, "AuditLogs", "Ability to view audit logs"),

            new Permission(Permissions.Delegation.Create, "Delegation", "Ability to create approval delegations"),
            new Permission(Permissions.Delegation.Read, "Delegation", "Ability to view approval delegations"),
            new Permission(Permissions.Delegation.Manage, "Delegation", "Ability to manage all approval delegations"),

            new Permission(Permissions.Sla.Read, "SLA", "Ability to view SLA dashboard and metrics"),
            new Permission(Permissions.Sla.Manage, "SLA", "Ability to configure SLA rules and escalations")
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

        // OrgAdmin gets users, workflows, reports, audit logs, read purchase, SLA
        foreach (var p in permissions.Where(x => x.Category is "Workflow" or "Users" or "Reports" or "AuditLogs" or "SLA" or "Delegation" || x.Code == Permissions.Purchase.Read))
        {
            AddRolePermissionIfNotExists(orgAdminRole.Id, p.Id);
        }

        // Employee gets Purchase.Create, Purchase.Read, Purchase.Update, Delegation.Create, Delegation.Read
        foreach (var p in permissions.Where(x => x.Code is Permissions.Purchase.Create or Permissions.Purchase.Read or Permissions.Purchase.Update or Permissions.Delegation.Create or Permissions.Delegation.Read))
        {
            AddRolePermissionIfNotExists(employeeRole.Id, p.Id);
        }

        // Manager, Finance, Procurement get Purchase.Read, Purchase.Approve, Purchase.Reject, Delegation.Create, Delegation.Read, SLA.Read
        foreach (var p in permissions.Where(x => x.Code is Permissions.Purchase.Read or Permissions.Purchase.Approve or Permissions.Purchase.Reject or Permissions.Delegation.Create or Permissions.Delegation.Read or Permissions.Sla.Read))
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
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            _logger.LogInformation("Database Seeder: Skipping default Super Admin seeding in Production environment.");
            return;
        }

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

    private async Task SeedComprehensiveDemoDataAsync()
    {
        if (await _context.Organizations.AnyAsync())
        {
            _logger.LogInformation("Database Seeder: Organizations already exist. Skipping demo data seeding.");
            return;
        }

        _logger.LogInformation("Database Seeder: Populating comprehensive demo dataset for all features...");

        var defaultPasswordHash = _passwordHasher.HashPassword("Password123!");

        // 1. Organizations
        var orgHq = new Organization("FlowDesk Enterprise HQ", "HQ", "Global Corporate Headquarters");
        var orgTech = new Organization("FlowDesk Tech & Innovation Hub", "TECH", "Research, Development & Cloud Infrastructure");
        _context.Organizations.AddRange(orgHq, orgTech);
        await _context.SaveChangesAsync();

        // 2. Departments
        var deptIt = new Department(orgHq.Id, "Information Technology", "IT");
        var deptHr = new Department(orgHq.Id, "Human Resources", "HR");
        var deptFin = new Department(orgHq.Id, "Finance & Accounting", "FIN");
        var deptProc = new Department(orgHq.Id, "Procurement & Supply", "PROC");
        var deptOps = new Department(orgHq.Id, "Operations & Logistics", "OPS");
        _context.Departments.AddRange(deptIt, deptHr, deptFin, deptProc, deptOps);
        await _context.SaveChangesAsync();

        // 3. Positions
        var posCeo = new Position(deptIt.Id, "Chief Executive Officer", "POS-CEO", 5);
        var posCfo = new Position(deptFin.Id, "Chief Financial Officer", "POS-CFO", 5);
        var posItDir = new Position(deptIt.Id, "IT Director", "POS-IT-DIR", 4);
        var posHrDir = new Position(deptHr.Id, "HR Director", "POS-HR-DIR", 4);
        var posSrEng = new Position(deptIt.Id, "Senior Software Engineer", "POS-SR-ENG", 3);
        var posDevOps = new Position(deptIt.Id, "DevOps Lead", "POS-DEVOPS", 3);
        var posHrSpec = new Position(deptHr.Id, "HR Specialist", "POS-HR-SPEC", 2);
        var posFinMgr = new Position(deptFin.Id, "Finance Manager", "POS-FIN-MGR", 3);
        var posSrAcct = new Position(deptFin.Id, "Senior Accountant", "POS-SR-ACCT", 2);
        var posProcLead = new Position(deptProc.Id, "Procurement Lead", "POS-PROC-LEAD", 3);
        var posOpsSpec = new Position(deptOps.Id, "Operations Specialist", "POS-OPS-SPEC", 2);
        _context.Positions.AddRange(posCeo, posCfo, posItDir, posHrDir, posSrEng, posDevOps, posHrSpec, posFinMgr, posSrAcct, posProcLead, posOpsSpec);
        await _context.SaveChangesAsync();

        // Roles lookup
        var roles = await _context.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);

        // 4. Demo Users
        var demoUsers = new List<(User user, string roleName)>
        {
            (new User("orgadmin@flowdesk.local", defaultPasswordHash, "Sarah", "Admin", "System Administrator", deptIt.Id, posItDir.Id), Roles.OrganizationAdmin),
            (new User("cfo.john@flowdesk.local", defaultPasswordHash, "John", "Miller", "Chief Financial Officer", deptFin.Id, posCfo.Id), Roles.FinanceOfficer),
            (new User("it.director@flowdesk.local", defaultPasswordHash, "David", "Clark", "IT Director", deptIt.Id, posItDir.Id), Roles.Manager),
            (new User("hr.director@flowdesk.local", defaultPasswordHash, "Emma", "Watson", "HR Director", deptHr.Id, posHrDir.Id), Roles.Manager),
            (new User("procurement.lead@flowdesk.local", defaultPasswordHash, "Robert", "Taylor", "Procurement Lead", deptProc.Id, posProcLead.Id), Roles.ProcurementOfficer),
            (new User("engineer.alex@flowdesk.local", defaultPasswordHash, "Alex", "Rivera", "Senior Software Engineer", deptIt.Id, posSrEng.Id), Roles.Employee),
            (new User("engineer.sarah@flowdesk.local", defaultPasswordHash, "Sarah", "Connor", "DevOps Lead", deptIt.Id, posDevOps.Id), Roles.Employee),
            (new User("hr.specialist@flowdesk.local", defaultPasswordHash, "Laura", "Palmer", "HR Specialist", deptHr.Id, posHrSpec.Id), Roles.Employee),
            (new User("accountant.mark@flowdesk.local", defaultPasswordHash, "Mark", "Davis", "Senior Accountant", deptFin.Id, posSrAcct.Id), Roles.Employee),
            (new User("ops.specialist@flowdesk.local", defaultPasswordHash, "James", "Wilson", "Operations Specialist", deptOps.Id, posOpsSpec.Id), Roles.Employee)
        };

        foreach (var item in demoUsers)
        {
            _context.Users.Add(item.user);
        }
        await _context.SaveChangesAsync();

        foreach (var item in demoUsers)
        {
            if (roles.TryGetValue(item.roleName, out var roleId))
            {
                _context.UserRoles.Add(new UserRole { UserId = item.user.Id, RoleId = roleId });
            }
        }
        await _context.SaveChangesAsync();

        // Assign Department Managers
        var uItDir = demoUsers.First(u => u.user.Email == "it.director@flowdesk.local").user;
        var uHrDir = demoUsers.First(u => u.user.Email == "hr.director@flowdesk.local").user;
        var uCfo = demoUsers.First(u => u.user.Email == "cfo.john@flowdesk.local").user;
        var uProcLead = demoUsers.First(u => u.user.Email == "procurement.lead@flowdesk.local").user;
        var uAlex = demoUsers.First(u => u.user.Email == "engineer.alex@flowdesk.local").user;
        var uSarahDev = demoUsers.First(u => u.user.Email == "engineer.sarah@flowdesk.local").user;
        var uMarkAcct = demoUsers.First(u => u.user.Email == "accountant.mark@flowdesk.local").user;
        var uHrSpec = demoUsers.First(u => u.user.Email == "hr.specialist@flowdesk.local").user;
        var uOpsSpec = demoUsers.First(u => u.user.Email == "ops.specialist@flowdesk.local").user;
        var uSuperAdmin = await _context.Users.FirstAsync(u => u.Email == "admin@flowdesk.local");

        deptIt.SetManager(uItDir.Id);
        deptHr.SetManager(uHrDir.Id);
        deptFin.SetManager(uCfo.Id);
        deptProc.SetManager(uProcLead.Id);
        await _context.SaveChangesAsync();

        // 5. Request Types
        var rtPurchase = new RequestType(orgHq.Id, "Purchase Request", "PURCHASE", "Equipment, hardware, software, and vendor procurement requests", "bi-cart-check");
        var rtLeave = new RequestType(orgHq.Id, "Leave & Absence Request", "LEAVE", "Annual leave, sick leave, and remote work authorization", "bi-calendar-event");
        var rtItEquip = new RequestType(orgHq.Id, "IT Hardware & Cloud Access", "IT-EQUIP", "Workstations, accessories, AWS/Azure access, and software licenses", "bi-laptop");
        var rtTravel = new RequestType(orgHq.Id, "Travel & Expense Reimbursement", "TRAVEL", "Flight tickets, hotel stay, conference fees, and travel expenses", "bi-airplane");
        var rtBudget = new RequestType(orgHq.Id, "Budget Approval", "BUDGET", "Departmental budget allocation and project funding requests", "bi-cash-coin");
        _context.RequestTypes.AddRange(rtPurchase, rtLeave, rtItEquip, rtTravel, rtBudget);
        await _context.SaveChangesAsync();

        // 6. Workflows, Versions, Steps, Conditions
        // Workflow 1: Purchase Request Workflow
        var wfPurchase = new Workflow(orgHq.Id, rtPurchase.Id, "Enterprise Purchase Approval Workflow", "WF-PURCHASE", "Multi-tier approval for purchase requests including Manager, Finance, and Procurement steps.");
        _context.Workflows.Add(wfPurchase);
        await _context.SaveChangesAsync();

        var wfVerPurchase = new WorkflowVersion(wfPurchase.Id, 1);
        wfVerPurchase.Publish();
        _context.WorkflowVersions.Add(wfVerPurchase);
        await _context.SaveChangesAsync();

        var stepP1 = new WorkflowStep(wfVerPurchase.Id, 1, "Direct Manager Approval", ApproverType.Manager, null, StepType.Sequential, false, 24);
        var stepP2 = new WorkflowStep(wfVerPurchase.Id, 2, "Finance Department Review", ApproverType.Role, roles[Roles.FinanceOfficer], StepType.Sequential, false, 48);
        var stepP3 = new WorkflowStep(wfVerPurchase.Id, 3, "Procurement Officer Execution", ApproverType.Role, roles[Roles.ProcurementOfficer], StepType.Sequential, false, 48);
        _context.WorkflowSteps.AddRange(stepP1, stepP2, stepP3);
        await _context.SaveChangesAsync();

        var condP2 = new WorkflowCondition(stepP2.Id, "TotalAmount", ConditionOperator.GreaterThanOrEqual, "1000", "AND");
        _context.WorkflowConditions.Add(condP2);
        await _context.SaveChangesAsync();

        // Workflow 2: Leave Request Workflow
        var wfLeave = new Workflow(orgHq.Id, rtLeave.Id, "Employee Leave Approval Workflow", "WF-LEAVE", "Standard leave approval by Manager and HR.");
        _context.Workflows.Add(wfLeave);
        await _context.SaveChangesAsync();

        var wfVerLeave = new WorkflowVersion(wfLeave.Id, 1);
        wfVerLeave.Publish();
        _context.WorkflowVersions.Add(wfVerLeave);
        await _context.SaveChangesAsync();

        var stepL1 = new WorkflowStep(wfVerLeave.Id, 1, "Direct Manager Approval", ApproverType.Manager, null, StepType.Sequential, false, 24);
        var stepL2 = new WorkflowStep(wfVerLeave.Id, 2, "HR Department Approval", ApproverType.DepartmentManager, deptHr.Id, StepType.Sequential, false, 48);
        _context.WorkflowSteps.AddRange(stepL1, stepL2);
        await _context.SaveChangesAsync();

        // Workflow 3: IT Hardware Workflow
        var wfIt = new Workflow(orgHq.Id, rtItEquip.Id, "IT Equipment Provisioning Workflow", "WF-IT-EQUIP", "Direct approval by IT Department Manager.");
        _context.Workflows.Add(wfIt);
        await _context.SaveChangesAsync();

        var wfVerIt = new WorkflowVersion(wfIt.Id, 1);
        wfVerIt.Publish();
        _context.WorkflowVersions.Add(wfVerIt);
        await _context.SaveChangesAsync();

        var stepIt1 = new WorkflowStep(wfVerIt.Id, 1, "IT Manager Verification", ApproverType.DepartmentManager, deptIt.Id, StepType.Sequential, false, 24);
        _context.WorkflowSteps.Add(stepIt1);
        await _context.SaveChangesAsync();

        // Workflow 4: Travel Expense Workflow
        var wfTravel = new Workflow(orgHq.Id, rtTravel.Id, "Travel & Expense Reimbursement Workflow", "WF-TRAVEL", "Manager and Finance approval workflow for travel expenses.");
        _context.Workflows.Add(wfTravel);
        await _context.SaveChangesAsync();

        var wfVerTravel = new WorkflowVersion(wfTravel.Id, 1);
        wfVerTravel.Publish();
        _context.WorkflowVersions.Add(wfVerTravel);
        await _context.SaveChangesAsync();

        var stepT1 = new WorkflowStep(wfVerTravel.Id, 1, "Direct Manager Approval", ApproverType.Manager, null, StepType.Sequential, false, 24);
        var stepT2 = new WorkflowStep(wfVerTravel.Id, 2, "Finance Officer Reimbursement Approval", ApproverType.Role, roles[Roles.FinanceOfficer], StepType.Sequential, false, 48);
        _context.WorkflowSteps.AddRange(stepT1, stepT2);
        await _context.SaveChangesAsync();

        // 7. Seed Requests in various realistic states

        // Request 1: Approved Purchase Request
        var req1 = new Request("REQ-2026-0001", rtPurchase.Id, uAlex.Id, orgHq.Id, "High-Performance Engineering Workstations & 4K Monitors", "Purchasing hardware specs for the high-concurrency microservices development squad.", 6400m, "EGP", RequestPriority.High, deptIt.Id);
        req1.Submit(wfVerPurchase.Id);
        req1.Approve();
        _context.Requests.Add(req1);
        await _context.SaveChangesAsync();

        var item1a = new RequestItem(req1.Id, "Dell XPS 16 Developer Laptop (64GB RAM, 2TB SSD)", 2, 2700m, "Top-tier developer workstation");
        var item1b = new RequestItem(req1.Id, "Dell UltraSharp 27\" 4K USB-C Hub Monitor", 2, 500m, "Dual monitor setup for engineers");
        _context.RequestItems.AddRange(item1a, item1b);

        var inst1_1 = new ApprovalInstance(req1.Id, stepP1.Id, 1, uItDir.Id, null, 24);
        inst1_1.RecordDecision(ApprovalDecision.Approved, uItDir.Id, "Hardware specifications verified and approved for IT squad.");
        var inst1_2 = new ApprovalInstance(req1.Id, stepP2.Id, 2, uCfo.Id, roles[Roles.FinanceOfficer], 48);
        inst1_2.RecordDecision(ApprovalDecision.Approved, uCfo.Id, "Budget verified against Q3 CAPEX allocation.");
        var inst1_3 = new ApprovalInstance(req1.Id, stepP3.Id, 3, uProcLead.Id, roles[Roles.ProcurementOfficer], 48);
        inst1_3.RecordDecision(ApprovalDecision.Approved, uProcLead.Id, "PO #9821 issued to authorized vendor.");
        _context.ApprovalInstances.AddRange(inst1_1, inst1_2, inst1_3);

        var cmt1a = new Comment(req1.Id, uAlex.Id, "Needed urgently before the next sprint starts.");
        var cmt1b = new Comment(req1.Id, uItDir.Id, "Approved based on squad headcount expansion.");
        _context.Comments.AddRange(cmt1a, cmt1b);

        var att1 = new Attachment(req1.Id, "Dell_Vendor_Quotation_2026.pdf", "att_dell_quote_9821.pdf", "application/pdf", 524288, "/uploads/att_dell_quote_9821.pdf", uAlex.Id);
        _context.Attachments.Add(att1);

        // Request 2: Pending Approval Purchase Request (Pending at Finance Step 2)
        var req2 = new Request("REQ-2026-0002", rtPurchase.Id, uSarahDev.Id, orgHq.Id, "AWS Enterprise Savings Plan & Reserved Instances", "Annual commitment for AWS Cloud infrastructure supporting production microservices.", 18500m, "EGP", RequestPriority.Urgent, deptIt.Id);
        req2.Submit(wfVerPurchase.Id);
        req2.AdvanceToStep(2);
        _context.Requests.Add(req2);
        await _context.SaveChangesAsync();

        var item2a = new RequestItem(req2.Id, "AWS EC2 Reserved Instances 3-Year All Upfront", 1, 12500m, "Production computing cluster");
        var item2b = new RequestItem(req2.Id, "AWS Aurora PostgreSQL Reserved DB Cluster", 1, 6000m, "High-availability database tier");
        _context.RequestItems.AddRange(item2a, item2b);

        var inst2_1 = new ApprovalInstance(req2.Id, stepP1.Id, 1, uItDir.Id, null, 24);
        inst2_1.RecordDecision(ApprovalDecision.Approved, uItDir.Id, "Essential for production scalability and 99.99% uptime compliance.");
        var inst2_2 = new ApprovalInstance(req2.Id, stepP2.Id, 2, uCfo.Id, roles[Roles.FinanceOfficer], 48);
        _context.ApprovalInstances.AddRange(inst2_1, inst2_2);

        var cmt2 = new Comment(req2.Id, uSarahDev.Id, "Expected cost savings of ~35% compared to on-demand pricing.");
        _context.Comments.Add(cmt2);

        // Request 3: Pending Approval Leave Request (Pending at Manager Step 1)
        var req3 = new Request("REQ-2026-0003", rtLeave.Id, uAlex.Id, orgHq.Id, "Annual Vacation Leave - 10 Business Days", "Requesting annual paid leave for family vacation.", 0m, "EGP", RequestPriority.Medium, deptIt.Id);
        req3.Submit(wfVerLeave.Id);
        _context.Requests.Add(req3);
        await _context.SaveChangesAsync();

        var inst3_1 = new ApprovalInstance(req3.Id, stepL1.Id, 1, uItDir.Id, null, 24);
        _context.ApprovalInstances.Add(inst3_1);

        // Request 4: Approved Leave Request
        var req4 = new Request("REQ-2026-0004", rtLeave.Id, uSarahDev.Id, orgHq.Id, "Medical Leave Request - 3 Days", "Medical leave due to health checkup and doctor prescribed rest.", 0m, "EGP", RequestPriority.High, deptIt.Id);
        req4.Submit(wfVerLeave.Id);
        req4.Approve();
        _context.Requests.Add(req4);
        await _context.SaveChangesAsync();

        var inst4_1 = new ApprovalInstance(req4.Id, stepL1.Id, 1, uItDir.Id, null, 24);
        inst4_1.RecordDecision(ApprovalDecision.Approved, uItDir.Id, "Approved. Get well soon!");
        var inst4_2 = new ApprovalInstance(req4.Id, stepL2.Id, 2, uHrDir.Id, null, 48);
        inst4_2.RecordDecision(ApprovalDecision.Approved, uHrDir.Id, "Medical report verified and logged in HR portal.");
        _context.ApprovalInstances.AddRange(inst4_1, inst4_2);

        var att4 = new Attachment(req4.Id, "Doctor_Medical_Certificate.pdf", "att_med_cert_331.pdf", "application/pdf", 312000, "/uploads/att_med_cert_331.pdf", uSarahDev.Id);
        _context.Attachments.Add(att4);

        // Request 5: Returned Request for Revision
        var req5 = new Request("REQ-2026-0005", rtTravel.Id, uMarkAcct.Id, orgHq.Id, "Q2 Financial Audit On-site Travel Expenses", "Travel expenses for conducting branch audit in Alexandria office.", 3200m, "EGP", RequestPriority.Medium, deptFin.Id);
        req5.Submit(wfVerTravel.Id);
        req5.ReturnToDraft();
        _context.Requests.Add(req5);
        await _context.SaveChangesAsync();

        var item5a = new RequestItem(req5.Id, "Express Train Tickets (Round Trip)", 2, 400m, "First class train ticket");
        var item5b = new RequestItem(req5.Id, "Hotel Accommodation (3 Nights)", 3, 800m, "Branch audit stay");
        var item5c = new RequestItem(req5.Id, "Local Transportation & Meals Allowance", 1, 400m, "Daily stipend");
        _context.RequestItems.AddRange(item5a, item5b, item5c);

        var inst5_1 = new ApprovalInstance(req5.Id, stepT1.Id, 1, uCfo.Id, null, 24);
        inst5_1.RecordDecision(ApprovalDecision.Returned, uCfo.Id, "Please attach itemized hotel receipts before resubmitting.");
        _context.ApprovalInstances.Add(inst5_1);

        var cmt5 = new Comment(req5.Id, uCfo.Id, "Hotel receipt missing. Please upload and click resubmit.");
        _context.Comments.Add(cmt5);

        // Request 6: Rejected Request
        var req6 = new Request("REQ-2026-0006", rtItEquip.Id, uOpsSpec.Id, orgHq.Id, "Unsanctioned Cloud Analytics Software License", "Individual subscription request for third-party analytics software.", 4500m, "EGP", RequestPriority.Low, deptOps.Id);
        req6.Submit(wfVerIt.Id);
        req6.Reject();
        _context.Requests.Add(req6);
        await _context.SaveChangesAsync();

        var inst6_1 = new ApprovalInstance(req6.Id, stepIt1.Id, 1, uItDir.Id, null, 24);
        inst6_1.RecordDecision(ApprovalDecision.Rejected, uItDir.Id, "Rejected. Enterprise BI tool PowerBI is already provided to all operations staff.");
        _context.ApprovalInstances.Add(inst6_1);

        var cmt6 = new Comment(req6.Id, uItDir.Id, "Use official PowerBI dashboards instead of purchasing standalone licenses.");
        _context.Comments.Add(cmt6);

        // Request 7: Draft Request
        var req7 = new Request("REQ-2026-0007", rtPurchase.Id, uHrSpec.Id, orgHq.Id, "Q3 Office Ergonomics & Refreshment Supplies", "Ergonomic chairs and refreshment station restock for HR department.", 2200m, "EGP", RequestPriority.Low, deptHr.Id);
        _context.Requests.Add(req7);
        await _context.SaveChangesAsync();

        var item7a = new RequestItem(req7.Id, "Ergonomic Mesh Task Chairs", 2, 850m, "Office chairs");
        var item7b = new RequestItem(req7.Id, "Coffee Machine Pods & Supplies", 1, 500m, "Refreshments");
        _context.RequestItems.AddRange(item7a, item7b);

        // Request 8: Completed IT Equipment Request
        var req8 = new Request("REQ-2026-0008", rtItEquip.Id, uAlex.Id, orgHq.Id, "Apple MacBook Pro M3 Max for Mobile Engineering", "Primary workstation for iOS & Android enterprise client app building.", 4200m, "EGP", RequestPriority.High, deptIt.Id);
        req8.Submit(wfVerIt.Id);
        req8.Approve();
        req8.Complete();
        _context.Requests.Add(req8);
        await _context.SaveChangesAsync();

        var item8 = new RequestItem(req8.Id, "MacBook Pro 16\" M3 Max 36GB RAM", 1, 4200m, "Mobile app build machine");
        _context.RequestItems.Add(item8);

        var inst8_1 = new ApprovalInstance(req8.Id, stepIt1.Id, 1, uItDir.Id, null, 24);
        inst8_1.RecordDecision(ApprovalDecision.Approved, uItDir.Id, "Approved for mobile development workload.");
        _context.ApprovalInstances.Add(inst8_1);

        await _context.SaveChangesAsync();

        // 8. Delegations
        var activeDelegation = new Delegation(
            uItDir.Id,
            uAlex.Id,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(7),
            "Delegating IT department manager approvals while attending AWS Summit conference.",
            rtPurchase.Id);

        var expiredDelegation = new Delegation(
            uCfo.Id,
            uMarkAcct.Id,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(-15),
            "Temporary financial review delegation during Q1 financial audit period.",
            null);
        expiredDelegation.Cancel();

        _context.Delegations.AddRange(activeDelegation, expiredDelegation);
        await _context.SaveChangesAsync();

        // 9. Notifications
        var notifs = new List<Notification>
        {
            new Notification(uCfo.Id, "Pending Approval Required", "Request REQ-2026-0002 (AWS Enterprise Savings Plan) requires your Finance approval.", NotificationType.ApprovalRequired),
            new Notification(uItDir.Id, "New Request Submitted", "Request REQ-2026-0003 (Annual Vacation Leave) was submitted by Alex Rivera.", NotificationType.ApprovalRequired),
            new Notification(uAlex.Id, "Request Approved", "Your request REQ-2026-0001 (High-Performance Engineering Workstations) has been fully approved!", NotificationType.ApprovalResult),
            new Notification(uSarahDev.Id, "Request Approved", "Your request REQ-2026-0004 (Medical Leave) has been approved by HR.", NotificationType.ApprovalResult),
            new Notification(uMarkAcct.Id, "Request Returned", "Your request REQ-2026-0005 (Travel Expenses) was returned for modification by John Miller.", NotificationType.ApprovalResult),
            new Notification(uAlex.Id, "Delegation Activated", "You have been assigned as active delegate for David Clark (IT Director) until " + DateTime.UtcNow.AddDays(7).ToString("MMM dd, yyyy") + ".", NotificationType.InApp),
            new Notification(uSuperAdmin.Id, "System Health Report", "All 5 recurring background jobs ran successfully with 0 errors.", NotificationType.InApp)
        };

        foreach (var n in notifs.Take(4))
        {
            // keep first 4 unread
        }
        foreach (var n in notifs.Skip(4))
        {
            n.MarkAsRead();
        }

        _context.Notifications.AddRange(notifs);
        await _context.SaveChangesAsync();

        // 10. Audit Logs
        var auditLogs = new List<AuditLog>
        {
            new AuditLog("USER_LOGIN", "User", uSuperAdmin.Id.ToString(), uSuperAdmin.Id, uSuperAdmin.Email, "127.0.0.1", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)", null, "{\"Event\":\"Successful Login\"}"),
            new AuditLog("WORKFLOW_PUBLISH", "WorkflowVersion", wfVerPurchase.Id.ToString(), uSuperAdmin.Id, uSuperAdmin.Email, "127.0.0.1", "Mozilla/5.0", null, "{\"VersionNumber\":1,\"Status\":\"Published\"}"),
            new AuditLog("REQUEST_CREATE", "Request", req1.Id.ToString(), uAlex.Id, uAlex.Email, "192.168.1.45", "Chrome/122.0", null, "{\"RequestNumber\":\"REQ-2026-0001\",\"TotalAmount\":6400}"),
            new AuditLog("APPROVAL_SUBMIT", "ApprovalInstance", inst1_1.Id.ToString(), uItDir.Id, uItDir.Email, "192.168.1.10", "Chrome/122.0", "{\"Status\":\"Pending\"}", "{\"Status\":\"Approved\",\"Decision\":\"Approved\"}"),
            new AuditLog("APPROVAL_SUBMIT", "ApprovalInstance", inst1_2.Id.ToString(), uCfo.Id, uCfo.Email, "192.168.1.12", "Edge/122.0", "{\"Status\":\"Pending\"}", "{\"Status\":\"Approved\",\"Decision\":\"Approved\"}"),
            new AuditLog("DELEGATION_CREATE", "Delegation", activeDelegation.Id.ToString(), uItDir.Id, uItDir.Email, "192.168.1.10", "Chrome/122.0", null, "{\"Delegatee\":\"engineer.alex@flowdesk.local\",\"Reason\":\"AWS Summit Conference\"}"),
            new AuditLog("REQUEST_RETURN", "Request", req5.Id.ToString(), uCfo.Id, uCfo.Email, "192.168.1.12", "Edge/122.0", "{\"Status\":\"PendingApproval\"}", "{\"Status\":\"Returned\"}")
        };

        _context.AuditLogs.AddRange(auditLogs);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Database Seeder: Comprehensive demo data successfully created!");
    }
}

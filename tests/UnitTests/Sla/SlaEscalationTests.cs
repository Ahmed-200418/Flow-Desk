using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Sla;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using OrganizationEntity = FlowDesk.Domain.Entities.Organization;
using WorkflowEntity = FlowDesk.Domain.Entities.Workflow;

namespace FlowDesk.UnitTests.SlaTests;

public class SlaEscalationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FlowDeskDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    public SlaEscalationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseSqlite(_connection)
            .Options;

        var interceptor = new AuditableEntityInterceptor(_currentUserServiceMock.Object);
        _context = new FlowDeskDbContext(options, interceptor);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task ProcessSla_ApproachingDueDate_ShouldSendReminderNotification()
    {
        var org = new OrganizationEntity("Acme Corp", "ACME");
        var reqType = new RequestType(org.Id, "Purchase", "PUR", "Purchase Requests");
        var user = new User("approver@flowdesk.local", "hash", "Approver", "User");
        var workflow = new WorkflowEntity(org.Id, reqType.Id, "Workflow", "WF", "Workflow");
        var version = new WorkflowVersion(workflow.Id, 1);
        var step = new WorkflowStep(version.Id, 1, "Manager Approval", ApproverType.User, user.Id);

        _context.Organizations.Add(org);
        _context.RequestTypes.Add(reqType);
        _context.Users.Add(user);
        _context.Workflows.Add(workflow);
        _context.WorkflowVersions.Add(version);
        _context.WorkflowSteps.Add(step);
        await _context.SaveChangesAsync();

        var req = new Request("REQ-2026-00010", reqType.Id, user.Id, org.Id, "Title", "Desc");
        _context.Requests.Add(req);
        await _context.SaveChangesAsync();

        // Create an approval instance assigned 30 hours ago, due in 18 hours (48h total, > 50% elapsed)
        var instance = new ApprovalInstance(req.Id, step.Id, 1, user.Id, null, timeoutHours: 48);
        instance.SetDates(DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(18));

        _context.ApprovalInstances.Add(instance);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var handler = new ProcessSlaAndEscalationsCommandHandler(_context);
        var result = await handler.Handle(new ProcessSlaAndEscalationsCommand(), CancellationToken.None);

        Assert.Equal(1, result.RemindersSent);

        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.RecipientUserId == user.Id);
        Assert.NotNull(notification);
        Assert.Equal(NotificationType.Reminder, notification.Type);
    }

    [Fact]
    public async Task ProcessSla_OverduePastEscalationWindow_ShouldEscalateToUpperManagement()
    {
        var org = new OrganizationEntity("Acme Corp", "ACME");
        var reqType = new RequestType(org.Id, "Purchase", "PUR", "Purchase Requests");

        _context.Organizations.Add(org);
        _context.RequestTypes.Add(reqType);
        await _context.SaveChangesAsync();

        var dept = new Department(org.Id, "Finance", "FIN");
        _context.Departments.Add(dept);
        await _context.SaveChangesAsync();

        var manager = new User("manager@flowdesk.local", "hash", "Dept", "Manager");
        _context.Users.Add(manager);
        await _context.SaveChangesAsync();

        dept.SetManager(manager.Id);
        await _context.SaveChangesAsync();

        var approver = new User("approver@flowdesk.local", "hash", "Approver", "User", departmentId: dept.Id);
        _context.Users.Add(approver);

        var workflow = new WorkflowEntity(org.Id, reqType.Id, "Workflow", "WF", "Workflow");
        var version = new WorkflowVersion(workflow.Id, 1);
        var step = new WorkflowStep(version.Id, 1, "Approval Step", ApproverType.User, approver.Id);

        _context.Workflows.Add(workflow);
        _context.WorkflowVersions.Add(version);
        _context.WorkflowSteps.Add(step);
        await _context.SaveChangesAsync();

        var req = new Request("REQ-2026-00020", reqType.Id, approver.Id, org.Id, "Laptop Purchase", "High priority", departmentId: dept.Id);
        _context.Requests.Add(req);
        await _context.SaveChangesAsync();

        var instance = new ApprovalInstance(req.Id, step.Id, 1, approver.Id, null, timeoutHours: 24);
        instance.SetDates(DateTime.UtcNow.AddHours(-60), DateTime.UtcNow.AddHours(-36));
        _context.ApprovalInstances.Add(instance);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var handler = new ProcessSlaAndEscalationsCommandHandler(_context);
        var result = await handler.Handle(new ProcessSlaAndEscalationsCommand(), CancellationToken.None);

        Assert.Equal(1, result.EscalationsProcessed);

        var escNotification = await _context.Notifications.FirstOrDefaultAsync(n => n.Type == NotificationType.Escalation);
        Assert.NotNull(escNotification);
        Assert.Equal(manager.Id, escNotification.RecipientUserId);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

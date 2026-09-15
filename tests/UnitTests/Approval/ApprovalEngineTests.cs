using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Approvals;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using FlowDesk.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using OrganizationEntity = FlowDesk.Domain.Entities.Organization;
using WorkflowEntity = FlowDesk.Domain.Entities.Workflow;

namespace FlowDesk.UnitTests.ApprovalTests;

public class ApprovalEngineTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FlowDeskDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly InMemoryIdempotencyService _idempotencyService = new();

    public ApprovalEngineTests()
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
    public async Task Approve_FinalStep_ShouldCompleteRequest()
    {
        var org = new OrganizationEntity("Acme Corp", "ACME");
        var reqType = new RequestType(org.Id, "Purchase", "PUR", "Purchase Requests");
        var user = new User("actor@flowdesk.local", "hash", "Actor", "User");
        _context.Organizations.Add(org);
        _context.RequestTypes.Add(reqType);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns(user.Id);

        var request = new Request("REQ-2026-00001", reqType.Id, user.Id, org.Id, "Title", "Desc");
        var workflow = new WorkflowEntity(org.Id, reqType.Id, "Purchase Flow", "PUR-FLOW", "Workflow");
        var version = new WorkflowVersion(workflow.Id, 1);
        _context.Workflows.Add(workflow);
        _context.WorkflowVersions.Add(version);
        await _context.SaveChangesAsync();

        request.Submit(version.Id);

        var step = new WorkflowStep(version.Id, 1, "Manager Approval", ApproverType.User, user.Id);
        var instance = new ApprovalInstance(request.Id, step.Id, 1, user.Id, null);

        _context.Requests.Add(request);
        _context.WorkflowSteps.Add(step);
        _context.ApprovalInstances.Add(instance);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var handler = new ApproveRequestCommandHandler(_context, _currentUserServiceMock.Object, _idempotencyService);
        var command = new ApproveRequestCommand(request.Id, instance.Id, "Looks good!");

        await handler.Handle(command, CancellationToken.None);

        var updatedReq = await _context.Requests.FirstAsync(r => r.Id == request.Id);
        var updatedInstance = await _context.ApprovalInstances.FirstAsync(ai => ai.Id == instance.Id);

        Assert.Equal(RequestStatus.Completed, updatedReq.Status);
        Assert.Equal(ApprovalStatus.Approved, updatedInstance.Status);
        Assert.Single(updatedInstance.Actions);
    }

    [Fact]
    public async Task Reject_ShouldSetRequestStatusToRejected()
    {
        var org = new OrganizationEntity("Acme Corp", "ACME");
        var reqType = new RequestType(org.Id, "Purchase", "PUR", "Purchase Requests");
        var user = new User("actor2@flowdesk.local", "hash", "Actor2", "User");
        _context.Organizations.Add(org);
        _context.RequestTypes.Add(reqType);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns(user.Id);

        var request = new Request("REQ-2026-00002", reqType.Id, user.Id, org.Id, "Title", "Desc");
        var workflow = new WorkflowEntity(org.Id, reqType.Id, "Purchase Flow", "PUR-FLOW2", "Workflow");
        var version = new WorkflowVersion(workflow.Id, 1);
        _context.Workflows.Add(workflow);
        _context.WorkflowVersions.Add(version);
        await _context.SaveChangesAsync();

        request.Submit(version.Id);

        var step = new WorkflowStep(version.Id, 1, "Finance Approval", ApproverType.User, user.Id);
        var instance = new ApprovalInstance(request.Id, step.Id, 1, user.Id, null);

        _context.Requests.Add(request);
        _context.WorkflowSteps.Add(step);
        _context.ApprovalInstances.Add(instance);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var handler = new RejectRequestCommandHandler(_context, _currentUserServiceMock.Object, _idempotencyService);
        var command = new RejectRequestCommand(request.Id, instance.Id, "Budget limit exceeded.");

        await handler.Handle(command, CancellationToken.None);

        var updatedReq = await _context.Requests.FirstAsync(r => r.Id == request.Id);
        var updatedInstance = await _context.ApprovalInstances.FirstAsync(ai => ai.Id == instance.Id);

        Assert.Equal(RequestStatus.Rejected, updatedReq.Status);
        Assert.Equal(ApprovalStatus.Rejected, updatedInstance.Status);
    }

    [Fact]
    public void IdempotencyService_ShouldPreventDuplicateExecution()
    {
        var key = "UNIQUE-IDEMPOTENCY-KEY-123";

        Assert.False(_idempotencyService.IsProcessed(key));
        _idempotencyService.MarkAsProcessed(key);
        Assert.True(_idempotencyService.IsProcessed(key));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

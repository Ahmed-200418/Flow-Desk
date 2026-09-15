using FlowDesk.Application.Features.Workflows;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using OrganizationEntity = FlowDesk.Domain.Entities.Organization;

namespace FlowDesk.UnitTests.Workflow;

public class WorkflowManagementTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FlowDeskDbContext _context;

    public WorkflowManagementTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseSqlite(_connection)
            .Options;

        var currentUserServiceMock = new Mock<FlowDesk.Application.Common.Interfaces.ICurrentUserService>();
        var interceptor = new AuditableEntityInterceptor(currentUserServiceMock.Object);
        _context = new FlowDeskDbContext(options, interceptor);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CreateWorkflow_ShouldCreateWorkflowAndInitialDraftVersion()
    {
        var org = new OrganizationEntity("Acme", "ACME");
        var reqType = new RequestType(org.Id, "Purchase", "PUR", "Purchase requests");
        _context.Organizations.Add(org);
        _context.RequestTypes.Add(reqType);
        await _context.SaveChangesAsync();

        var handler = new CreateWorkflowCommandHandler(_context);
        var command = new CreateWorkflowCommand(org.Id, reqType.Id, "Purchase Approval Flow", "PUR-FLOW", "Description");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PUR-FLOW", result.Code);
        Assert.Single(result.Versions);
        Assert.Equal(WorkflowVersionStatus.Draft, result.Versions[0].Status);
    }

    [Fact]
    public async Task PublishWorkflowVersion_ShouldSetStatusToPublishedAndArchiveOldVersion()
    {
        var org = new OrganizationEntity("Acme", "ACME");
        var reqType = new RequestType(org.Id, "Purchase", "PUR", "Purchase requests");
        var workflow = new FlowDesk.Domain.Entities.Workflow(org.Id, reqType.Id, "Flow", "FLOW-1", "Desc");
        var version1 = new WorkflowVersion(workflow.Id, 1);
        var step1 = new WorkflowStep(version1.Id, 1, "Step 1", ApproverType.User, Guid.NewGuid());

        _context.Organizations.Add(org);
        _context.RequestTypes.Add(reqType);
        _context.Workflows.Add(workflow);
        _context.WorkflowVersions.Add(version1);
        _context.WorkflowSteps.Add(step1);
        await _context.SaveChangesAsync();

        // Publish version 1
        var publishHandler = new PublishWorkflowVersionCommandHandler(_context);
        await publishHandler.Handle(new PublishWorkflowVersionCommand(version1.Id), CancellationToken.None);

        var v1Published = await _context.WorkflowVersions.FirstAsync(v => v.Id == version1.Id);
        Assert.Equal(WorkflowVersionStatus.Published, v1Published.Status);

        // Create version 2 with a step
        var version2 = new WorkflowVersion(workflow.Id, 2);
        var step2 = new WorkflowStep(version2.Id, 1, "Step 1", ApproverType.User, Guid.NewGuid());
        _context.WorkflowVersions.Add(version2);
        _context.WorkflowSteps.Add(step2);
        await _context.SaveChangesAsync();

        // Publish version 2
        await publishHandler.Handle(new PublishWorkflowVersionCommand(version2.Id), CancellationToken.None);

        var v1Archived = await _context.WorkflowVersions.FirstAsync(v => v.Id == version1.Id);
        var v2Published = await _context.WorkflowVersions.FirstAsync(v => v.Id == version2.Id);

        Assert.Equal(WorkflowVersionStatus.Archived, v1Archived.Status);
        Assert.Equal(WorkflowVersionStatus.Published, v2Published.Status);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

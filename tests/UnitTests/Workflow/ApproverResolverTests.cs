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

namespace FlowDesk.UnitTests.Workflow;

public class ApproverResolverTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FlowDeskDbContext _context;
    private readonly ApproverResolver _resolver;

    public ApproverResolverTests()
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

        _resolver = new ApproverResolver(_context);
    }

    [Fact]
    public async Task ResolveApproverAsync_UserApproverType_ShouldReturnUserId()
    {
        var targetUserId = Guid.NewGuid();
        var step = new WorkflowStep(Guid.NewGuid(), 1, "User Step", ApproverType.User, targetUserId);
        var request = new Request("REQ-1", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc");

        var (userId, roleId) = await _resolver.ResolveApproverAsync(step, request);

        Assert.Equal(targetUserId, userId);
        Assert.Null(roleId);
    }

    [Fact]
    public async Task ResolveApproverAsync_RoleApproverType_ShouldReturnRoleId()
    {
        var targetRoleId = Guid.NewGuid();
        var step = new WorkflowStep(Guid.NewGuid(), 1, "Role Step", ApproverType.Role, targetRoleId);
        var request = new Request("REQ-2", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc");

        var (userId, roleId) = await _resolver.ResolveApproverAsync(step, request);

        Assert.Null(userId);
        Assert.Equal(targetRoleId, roleId);
    }

    [Fact]
    public async Task ResolveApproverAsync_DepartmentManager_ShouldReturnDepartmentManagerUserId()
    {
        var org = new OrganizationEntity("Acme", "ACME");
        var dept = new Department(org.Id, "Finance", "FIN");
        var managerUser = new User("finmanager@acme.local", "hash", "Finance", "Manager");

        _context.Organizations.Add(org);
        _context.Departments.Add(dept);
        _context.Users.Add(managerUser);
        await _context.SaveChangesAsync();

        // Assign manager to department
        var deptManagerProp = typeof(Department).GetProperty(nameof(Department.ManagerUserId));
        deptManagerProp?.SetValue(dept, managerUser.Id);
        await _context.SaveChangesAsync();

        var request = new Request("REQ-3", Guid.NewGuid(), managerUser.Id, org.Id, "Title", "Desc", departmentId: dept.Id);
        var step = new WorkflowStep(Guid.NewGuid(), 1, "Dept Manager Approval", ApproverType.DepartmentManager);

        var (userId, roleId) = await _resolver.ResolveApproverAsync(step, request);

        Assert.Equal(managerUser.Id, userId);
        Assert.Null(roleId);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

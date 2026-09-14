using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Departments;
using FlowDesk.Application.Features.Organizations;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FlowDesk.UnitTests.Organization;

public class OrganizationFeaturesTests
{
    private readonly FlowDeskDbContext _context;

    public OrganizationFeaturesTests()
    {
        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        var interceptor = new AuditableEntityInterceptor(mockCurrentUserService.Object);

        _context = new FlowDeskDbContext(options, interceptor);
    }

    [Fact]
    public async Task CreateOrganization_ShouldAddOrganizationToDatabase()
    {
        var handler = new CreateOrganizationCommandHandler(_context);
        var command = new CreateOrganizationCommand("Acme Corp", "ACME", "Global Enterprise");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Acme Corp", result.Name);
        Assert.Equal("ACME", result.Code);
        Assert.Single(_context.Organizations);
    }

    [Fact]
    public async Task CreateOrganization_WithDuplicateCode_ShouldThrowValidationException()
    {
        var handler = new CreateOrganizationCommandHandler(_context);
        await handler.Handle(new CreateOrganizationCommand("Acme Corp", "ACME", "Description"), CancellationToken.None);

        var duplicateCommand = new CreateOrganizationCommand("Acme Subsidiary", "ACME", "Other Description");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(duplicateCommand, CancellationToken.None));
    }

    [Fact]
    public async Task GetDepartmentHierarchy_ShouldBuildTreeStructure()
    {
        var orgHandler = new CreateOrganizationCommandHandler(_context);
        var org = await orgHandler.Handle(new CreateOrganizationCommand("Tech Corp", "TECH", "Tech Company"), CancellationToken.None);

        var deptHandler = new CreateDepartmentCommandHandler(_context);
        var rootDept = await deptHandler.Handle(new CreateDepartmentCommand(org.Id, "Executive", "EXEC"), CancellationToken.None);
        var subDept1 = await deptHandler.Handle(new CreateDepartmentCommand(org.Id, "Engineering", "ENG", rootDept.Id), CancellationToken.None);
        var subDept2 = await deptHandler.Handle(new CreateDepartmentCommand(org.Id, "DevOps", "DEVOPS", subDept1.Id), CancellationToken.None);

        var hierarchyHandler = new GetDepartmentHierarchyQueryHandler(_context);
        var hierarchy = await hierarchyHandler.Handle(new GetDepartmentHierarchyQuery(org.Id), CancellationToken.None);

        Assert.Single(hierarchy);
        Assert.Equal("Executive", hierarchy[0].Name);
        Assert.Single(hierarchy[0].SubDepartments);
        Assert.Equal("Engineering", hierarchy[0].SubDepartments[0].Name);
        Assert.Single(hierarchy[0].SubDepartments[0].SubDepartments);
        Assert.Equal("DevOps", hierarchy[0].SubDepartments[0].SubDepartments[0].Name);
    }
}

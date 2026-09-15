using NetArchTest.Rules;
using Xunit;

namespace FlowDesk.ArchitectureTests;

public class CleanArchitectureTests
{
    private const string DomainNamespace = "FlowDesk.Domain";
    private const string ApplicationNamespace = "FlowDesk.Application";
    private const string InfrastructureNamespace = "FlowDesk.Infrastructure";
    private const string ApiNamespace = "FlowDesk.API";

    [Fact]
    public void Domain_Should_Not_HaveDependencyOnOtherProjects()
    {
        var result = Types.InAssembly(typeof(Domain.Common.Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain layer must not depend on Application, Infrastructure, or API layers.");
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application layer must not depend on Infrastructure or API layers.");
    }

    [Fact]
    public void Controllers_Should_Not_DependOnDbContext()
    {
        var result = Types.InAssembly(typeof(API.Controllers.ApiControllerBase).Assembly)
            .That()
            .ResideInNamespace($"{ApiNamespace}.Controllers")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "API Controllers must not depend directly on Infrastructure/DbContext.");
    }

    [Fact]
    public void Handlers_Should_ResideInApplicationProject()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .That()
            .HaveNameEndingWith("Handler")
            .And()
            .DoNotResideInNamespace("FlowDesk.API.Authorization")
            .Should()
            .ResideInNamespaceStartingWith(ApplicationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application Command/Query handlers must reside within Application project.");
    }

    [Fact]
    public void Controllers_Should_InheritFromApiControllerBase()
    {
        var result = Types.InAssembly(typeof(API.Controllers.ApiControllerBase).Assembly)
            .That()
            .ResideInNamespace($"{ApiNamespace}.Controllers")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveName("ApiControllerBase")
            .And()
            .HaveNameEndingWith("Controller")
            .Should()
            .Inherit(typeof(API.Controllers.ApiControllerBase))
            .GetResult();

        Assert.True(result.IsSuccessful, "All API controllers must inherit from ApiControllerBase.");
    }
}

using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Audit;
using FlowDesk.Domain.Entities;
using FlowDesk.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FlowDesk.UnitTests.Audit;

public class AuditLogTests
{
    private static IApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<FlowDesk.Infrastructure.Persistence.FlowDeskDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var mockAuditableInterceptor = new Mock<FlowDesk.Infrastructure.Persistence.Interceptors.AuditableEntityInterceptor>(Mock.Of<ICurrentUserService>());
        return new FlowDesk.Infrastructure.Persistence.FlowDeskDbContext(options, mockAuditableInterceptor.Object);
    }

    [Fact]
    public void AuditService_SerializeAndRedact_RedactsSensitiveFields()
    {
        // Arrange
        var payload = new
        {
            Username = "john_doe",
            Password = "SuperSecretPassword123!",
            SecretKey = "sk_live_12345",
            RefreshToken = "token_abc_xyz",
            Details = new
            {
                Role = "Admin",
                SecurityStamp = "stamp_999"
            }
        };

        // Act
        var json = AuditService.SerializeAndRedact(payload);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("john_doe", json);
        Assert.Contains("Admin", json);
        Assert.DoesNotContain("SuperSecretPassword123!", json);
        Assert.DoesNotContain("sk_live_12345", json);
        Assert.DoesNotContain("token_abc_xyz", json);
        Assert.DoesNotContain("stamp_999", json);
        Assert.Contains("[REDACTED]", json);
    }

    [Fact]
    public async Task GetAuditLogsQuery_FiltersByEntityAndActionCorrectly()
    {
        // Arrange
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var userGuid = Guid.NewGuid();

        db.AuditLogs.Add(new AuditLog("Request.Create", "Request", "REQ-1001", userGuid, "user@test.com", "127.0.0.1", "Mozilla", null, "{ \"Amount\": 5000 }"));
        db.AuditLogs.Add(new AuditLog("Request.Approve", "Request", "REQ-1001", userGuid, "manager@test.com", "127.0.0.1", "Mozilla", "{ \"Status\": \"Pending\" }", "{ \"Status\": \"Approved\" }"));
        db.AuditLogs.Add(new AuditLog("Auth.Login", "User", userGuid.ToString(), userGuid, "user@test.com", "127.0.0.1", "Mozilla", null, null));
        await db.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetAuditLogsQuery(PageNumber: 1, PageSize: 10, EntityName: "Request"), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal("Request", item.EntityName));
    }

    [Fact]
    public async Task GetEntityAuditTrailQuery_ReturnsChronologicalEntityHistory()
    {
        // Arrange
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var entityId = "REQ-2002";

        db.AuditLogs.Add(new AuditLog("Request.Created", "Request", entityId, Guid.NewGuid(), "employee@test.com"));
        db.AuditLogs.Add(new AuditLog("Request.Approved", "Request", entityId, Guid.NewGuid(), "manager@test.com"));
        db.AuditLogs.Add(new AuditLog("Workflow.Updated", "Workflow", "WF-1", Guid.NewGuid(), "admin@test.com"));
        await db.SaveChangesAsync();

        var handler = new GetEntityAuditTrailQueryHandler(db);

        // Act
        var history = await handler.Handle(new GetEntityAuditTrailQuery("Request", entityId), CancellationToken.None);

        // Assert
        Assert.Equal(2, history.Count);
        Assert.All(history, h => Assert.Equal("Request", h.EntityName));
        Assert.All(history, h => Assert.Equal(entityId, h.EntityId));
    }

    [Fact]
    public async Task GetAuditStatsQuery_CalculatesMetricsCorrectly()
    {
        // Arrange
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        db.AuditLogs.Add(new AuditLog("Auth.Login", "User", user1.ToString(), user1, "user1@test.com"));
        db.AuditLogs.Add(new AuditLog("Request.Create", "Request", "REQ-1", user2, "user2@test.com"));
        db.AuditLogs.Add(new AuditLog("Workflow.Publish", "Workflow", "WF-1", user1, "user1@test.com"));
        await db.SaveChangesAsync();

        var handler = new GetAuditStatsQueryHandler(db);

        // Act
        var stats = await handler.Handle(new GetAuditStatsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, stats.TotalLogs);
        Assert.Equal(1, stats.SecurityEventsCount);
        Assert.Equal(1, stats.RequestEventsCount);
        Assert.Equal(1, stats.WorkflowEventsCount);
        Assert.Equal(2, stats.UniqueUsersCount);
    }
}

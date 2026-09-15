using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Reports;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FlowDesk.UnitTests.Reports;

public class ReportingFeaturesTests
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
    public async Task GetEmployeeDashboardReportQuery_CalculatesStatusCountsCorrectly()
    {
        // Arrange
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var reqType = new RequestType(orgId, "Expense", "EXP", "Expense Request");
        db.RequestTypes.Add(reqType);

        var dept = new Department(orgId, "Engineering", "ENG");
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var req1 = new Request("REQ-001", reqType.Id, userId, orgId, "Req 1", "Description 1", departmentId: dept.Id);
        req1.Submit(versionId); // PendingApproval

        var req2 = new Request("REQ-002", reqType.Id, userId, orgId, "Req 2", "Description 2", departmentId: dept.Id);
        req2.Submit(versionId);
        req2.Approve(); // Approved

        db.Requests.AddRange(req1, req2);
        await db.SaveChangesAsync();

        var handler = new GetEmployeeDashboardReportQueryHandler(db);

        // Act
        var report = await handler.Handle(new GetEmployeeDashboardReportQuery(userId), CancellationToken.None);

        // Assert
        Assert.Equal(2, report.TotalMyRequests);
        Assert.Equal(1, report.PendingCount);
        Assert.Equal(1, report.ApprovedCount);
        Assert.Equal(0, report.RejectedCount);
    }

    [Fact]
    public async Task GetAdminDashboardReportQuery_CalculatesDepartmentAndSlaMetrics()
    {
        // Arrange
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var reqType = new RequestType(orgId, "Purchase", "PUR", "Purchase Request");
        var dept = new Department(orgId, "Finance", "FIN");
        db.RequestTypes.Add(reqType);
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var requester = Guid.NewGuid();
        var req = new Request("REQ-003", reqType.Id, requester, orgId, "Laptop Purchase", "Procurement of 1 Laptop", departmentId: dept.Id);
        req.Submit(versionId);
        req.Approve();
        db.Requests.Add(req);
        await db.SaveChangesAsync();

        var handler = new GetAdminDashboardReportQueryHandler(db);

        // Act
        var report = await handler.Handle(new GetAdminDashboardReportQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(1, report.TotalRequests);
        Assert.Equal(1, report.ApprovedRequests);
        Assert.Single(report.DepartmentAnalytics);
        Assert.Equal("Finance", report.DepartmentAnalytics.First().DepartmentName);
        Assert.Equal(100.0, report.SlaCompliancePercentage);
    }
}

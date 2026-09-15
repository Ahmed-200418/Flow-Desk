using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using FlowDesk.Infrastructure.Services;
using Hangfire;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FlowDesk.UnitTests.Jobs;

public class BackgroundJobTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FlowDeskDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IBackgroundJobClient> _jobClientMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ILogger<HangfireBackgroundJobService>> _loggerMock = new();

    public BackgroundJobTests()
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
    public void EnqueueNotification_ShouldDelegateToHangfireClient()
    {
        var jobService = new HangfireBackgroundJobService(_jobClientMock.Object, _serviceProviderMock.Object, _loggerMock.Object);
        var userId = Guid.NewGuid();

        jobService.EnqueueNotification(userId, "Title", "Message", NotificationType.InApp);

        _jobClientMock.Verify(x => x.Create(
            It.IsAny<Hangfire.Common.Job>(),
            It.IsAny<Hangfire.States.IState>()), Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

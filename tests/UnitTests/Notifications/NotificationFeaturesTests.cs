using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Notifications;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using FlowDesk.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FlowDesk.UnitTests.Notifications;

public class NotificationFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FlowDeskDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<ILogger<NotificationService>> _loggerMock = new();

    public NotificationFeaturesTests()
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
    public async Task NotificationService_SendNotification_ShouldSaveEntityAndTriggerEmail()
    {
        var user = new User("user@flowdesk.local", "hash", "Test", "User");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var service = new NotificationService(_context, _emailSenderMock.Object, _loggerMock.Object);
        await service.SendNotificationAsync(user.Id, "Test Title", "Test Message", NotificationType.ApprovalRequired);

        var savedNotification = await _context.Notifications.FirstOrDefaultAsync(n => n.RecipientUserId == user.Id);
        Assert.NotNull(savedNotification);
        Assert.Equal("Test Title", savedNotification.Title);
        Assert.Equal("Test Message", savedNotification.Message);

        _emailSenderMock.Verify(x => x.SendEmailAsync(user.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyNotifications_ShouldReturnUserNotifications()
    {
        var user = new User("user2@flowdesk.local", "hash", "User", "Two");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns(user.Id);

        var n1 = new Notification(user.Id, "Title 1", "Message 1", NotificationType.InApp);
        var n2 = new Notification(user.Id, "Title 2", "Message 2", NotificationType.Reminder);
        _context.Notifications.AddRange(n1, n2);
        await _context.SaveChangesAsync();

        var handler = new GetMyNotificationsQueryHandler(_context, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetMyNotificationsQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task MarkAsRead_ShouldUpdateIsReadStatus()
    {
        var user = new User("user3@flowdesk.local", "hash", "User", "Three");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns(user.Id);

        var notification = new Notification(user.Id, "Title", "Message", NotificationType.InApp);
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        Assert.False(notification.IsRead);

        var handler = new MarkNotificationAsReadCommandHandler(_context, _currentUserServiceMock.Object);
        await handler.Handle(new MarkNotificationAsReadCommand(notification.Id), CancellationToken.None);

        var updatedNotification = await _context.Notifications.FirstAsync(n => n.Id == notification.Id);
        Assert.True(updatedNotification.IsRead);
        Assert.NotNull(updatedNotification.ReadAtUtc);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

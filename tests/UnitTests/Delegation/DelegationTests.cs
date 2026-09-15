using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Delegations;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Entities;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FlowDesk.UnitTests.DelegationTests;

public class DelegationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FlowDeskDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    public DelegationTests()
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
    public void Delegation_SelfDelegation_ShouldThrowDomainException()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new Delegation(
            userId,
            userId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(5),
            "Test Reason"));
    }

    [Fact]
    public async Task CreateDelegation_ValidInputs_ShouldCreateDelegationAndAuditLog()
    {
        var user1 = new User("user1@flowdesk.local", "hash", "User", "One");
        var user2 = new User("user2@flowdesk.local", "hash", "User", "Two");
        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns(user1.Id);
        _currentUserServiceMock.Setup(x => x.UserEmail).Returns(user1.Email);

        var handler = new CreateDelegationCommandHandler(_context, _currentUserServiceMock.Object);
        var command = new CreateDelegationCommand(
            user2.Id,
            DateTime.UtcNow.AddMinutes(5),
            DateTime.UtcNow.AddDays(3),
            "Vacation Leave");

        var delegationId = await handler.Handle(command, CancellationToken.None);

        var created = await _context.Delegations.FirstOrDefaultAsync(d => d.Id == delegationId);
        Assert.NotNull(created);
        Assert.Equal(user1.Id, created.DelegatorUserId);
        Assert.Equal(user2.Id, created.DelegateeUserId);
        Assert.True(created.IsActive);

        var audit = await _context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == delegationId.ToString());
        Assert.NotNull(audit);
        Assert.Equal("CreateDelegation", audit.Action);
    }

    [Fact]
    public async Task CreateDelegation_OverlappingActiveDelegation_ShouldThrowValidationException()
    {
        var user1 = new User("u1@flowdesk.local", "hash", "U1", "Test");
        var user2 = new User("u2@flowdesk.local", "hash", "U2", "Test");
        var user3 = new User("u3@flowdesk.local", "hash", "U3", "Test");
        _context.Users.AddRange(user1, user2, user3);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns(user1.Id);

        var existing = new Delegation(
            user1.Id,
            user2.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(10),
            "Existing delegation");
        _context.Delegations.Add(existing);
        await _context.SaveChangesAsync();

        var handler = new CreateDelegationCommandHandler(_context, _currentUserServiceMock.Object);
        var command = new CreateDelegationCommand(
            user3.Id,
            DateTime.UtcNow.AddDays(2),
            DateTime.UtcNow.AddDays(5),
            "Overlapping delegation");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateDelegation_CircularDelegationChain_ShouldThrowValidationException()
    {
        var userA = new User("userA@flowdesk.local", "hash", "User", "A");
        var userB = new User("userB@flowdesk.local", "hash", "User", "B");
        _context.Users.AddRange(userA, userB);
        await _context.SaveChangesAsync();

        // userB delegates to userA
        var delegationBtoA = new Delegation(
            userB.Id,
            userA.Id,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(10),
            "B to A");
        _context.Delegations.Add(delegationBtoA);
        await _context.SaveChangesAsync();

        // Now userA attempts to delegate to userB (creates cycle A -> B -> A)
        _currentUserServiceMock.Setup(x => x.UserId).Returns(userA.Id);

        var handler = new CreateDelegationCommandHandler(_context, _currentUserServiceMock.Object);
        var command = new CreateDelegationCommand(
            userB.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(5),
            "A to B");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

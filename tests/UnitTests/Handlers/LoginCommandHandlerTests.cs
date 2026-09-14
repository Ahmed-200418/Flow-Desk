using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Features.Auth.Commands.Login;
using FlowDesk.Domain.Entities;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FlowDesk.UnitTests.Handlers;

public class LoginCommandHandlerTests
{
    private readonly FlowDeskDbContext _context;
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    public LoginCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        var interceptor = new AuditableEntityInterceptor(mockCurrentUserService.Object);

        _context = new FlowDeskDbContext(options, interceptor);
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ShouldThrowUnauthorizedException()
    {
        var handler = new LoginCommandHandler(_context, _passwordHasherMock.Object, _jwtTokenGeneratorMock.Object, _currentUserServiceMock.Object);
        var command = new LoginCommand("nonexistent@flowdesk.local", "Password123!");

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ShouldRecordFailedLoginAndThrow()
    {
        var user = new User("employee@flowdesk.local", "correct_hash", "Jane", "Doe");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _passwordHasherMock.Setup(x => x.VerifyPassword("correct_hash", "wrong_pwd")).Returns(false);

        var handler = new LoginCommandHandler(_context, _passwordHasherMock.Object, _jwtTokenGeneratorMock.Object, _currentUserServiceMock.Object);
        var command = new LoginCommand("employee@flowdesk.local", "wrong_pwd");

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(command, CancellationToken.None));

        var updatedUser = await _context.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal(1, updatedUser.AccessFailedCount);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldReturnAuthResponse()
    {
        var user = new User("employee@flowdesk.local", "correct_hash", "Jane", "Doe");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _passwordHasherMock.Setup(x => x.VerifyPassword("correct_hash", "Password123!")).Returns(true);
        _jwtTokenGeneratorMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()))
            .Returns(("access_token_123", DateTime.UtcNow.AddMinutes(15)));
        _jwtTokenGeneratorMock.Setup(x => x.GenerateRefreshToken()).Returns("raw_refresh_token_123");
        _jwtTokenGeneratorMock.Setup(x => x.HashToken("raw_refresh_token_123")).Returns("hashed_refresh_token_123");

        var handler = new LoginCommandHandler(_context, _passwordHasherMock.Object, _jwtTokenGeneratorMock.Object, _currentUserServiceMock.Object);
        var command = new LoginCommand("employee@flowdesk.local", "Password123!");

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("access_token_123", response.AccessToken);
        Assert.Equal("raw_refresh_token_123", response.RefreshToken);
        Assert.Single(_context.RefreshTokens);
    }
}

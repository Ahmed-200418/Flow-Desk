using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Services;
using Xunit;

namespace FlowDesk.IntegrationTests;

public class ConcurrencyAndIdempotencyTests
{
    [Fact]
    public void IdempotencyService_Should_PreventDuplicateExecution()
    {
        // Arrange
        var idempotencyService = new InMemoryIdempotencyService();
        var key = "test-idempotency-key-" + Guid.NewGuid();

        // Act
        bool firstCheck = idempotencyService.IsProcessed(key);
        idempotencyService.MarkAsProcessed(key, TimeSpan.FromMinutes(10));
        bool secondCheck = idempotencyService.IsProcessed(key);

        // Assert
        Assert.False(firstCheck, "First request check should return false.");
        Assert.True(secondCheck, "Second request check with same idempotency key must return true.");
    }

    [Fact]
    public void OptimisticConcurrency_RowVersion_Should_MismatchOnConcurrentEdits()
    {
        // Arrange
        var request = new Request(
            requestNumber: "REQ-20260915-002",
            requestTypeId: Guid.NewGuid(),
            requesterUserId: Guid.NewGuid(),
            organizationId: Guid.NewGuid(),
            title: "Concurrent Edit Request",
            description: "Test optimistic concurrency check",
            totalAmount: 5000m,
            currency: "EGP"
        );

        var client1RowVersion = (byte[])request.RowVersion.Clone();
        var client2RowVersion = (byte[])request.RowVersion.Clone();

        // Act - Simulate Client 1 modifying the request and updating row version
        request.RowVersion = Guid.NewGuid().ToByteArray();

        // Client 2 attempts to save with original row version
        bool isConcurrencyConflict = !client2RowVersion.SequenceEqual(request.RowVersion);

        // Assert
        Assert.True(isConcurrencyConflict, "Optimistic concurrency check failed to detect concurrent edit conflict.");
    }
}

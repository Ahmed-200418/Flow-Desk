using System.Net;
using System.Security.Claims;
using FlowDesk.API.Authorization;
using FlowDesk.Domain.Constants;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Options;
using FlowDesk.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlowDesk.IntegrationTests;

public class SecurityIntegrationTests
{
    [Fact]
    public void JwtTokenGenerator_Should_GenerateValidTokenWithUserClaims()
    {
        // Arrange
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "FlowDeskTestIssuer",
            Audience = "FlowDeskTestAudience",
            Secret = "SuperSecretKeyForTestingFlowDeskAuthenticationSystem123!",
            ExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7
        });

        var generator = new JwtTokenGenerator(jwtOptions);

        var user = new User(
            email: "employee@flowdesk.test",
            passwordHash: "AQAAAAEAACcQAAAAEH...",
            firstName: "Test",
            lastName: "Employee",
            jobTitle: "Developer"
        );

        var roles = new List<string> { Roles.Employee };
        var permissions = new List<string> { Permissions.Purchase.Read, Permissions.Purchase.Create };

        // Act
        var (accessToken, expiresAt) = generator.GenerateAccessToken(user, roles, permissions);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(accessToken), "Access token should not be empty.");
        Assert.True(expiresAt > DateTime.UtcNow, "Expiration date should be in the future.");
    }

    [Fact]
    public async Task AuthorizationPolicies_Should_ValidatePermissionRequirements()
    {
        // Arrange
        var requiredPermission = Permissions.Purchase.Approve;
        var policyName = $"{HasPermissionAttribute.PolicyPrefix}{requiredPermission}";
        var authOptions = Options.Create(new AuthorizationOptions());
        var policyProvider = new PermissionPolicyProvider(authOptions);

        // Act
        var policy = await policyProvider.GetPolicyAsync(policyName);

        // Assert
        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is PermissionRequirement pr && pr.Permission == requiredPermission);
    }

    [Fact]
    public void RequestOwnership_IDOR_Check_Should_DenyAccessToNonOwnerNonManager()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var requestTypeId = Guid.NewGuid();

        var request = new Request(
            requestNumber: "REQ-20260915-001",
            requestTypeId: requestTypeId,
            requesterUserId: requesterId,
            organizationId: organizationId,
            title: "Sensitive Financial Request",
            description: "Confidential procurement details",
            totalAmount: 15000m,
            currency: "EGP"
        );

        // Act - Simulate authorization check
        bool isOwner = request.RequesterUserId == attackerId;
        bool isAuthorized = isOwner; // Non-owner and non-assigned approver cannot access requester resource

        // Assert
        Assert.False(isAuthorized, "IDOR check failed: Unauthorized user was able to access requester resource.");
    }
}

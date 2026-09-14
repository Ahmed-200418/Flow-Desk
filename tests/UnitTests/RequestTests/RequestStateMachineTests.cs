using FlowDesk.Domain.Common;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Services;
using Xunit;

namespace FlowDesk.UnitTests.RequestTests;

public class RequestStateMachineTests
{
    [Fact]
    public void Submit_FromDraftStatus_ShouldTransitionToPendingApproval()
    {
        var request = new Request("REQ-2026-00001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Laptops Purchase", "Procurement of 5 Laptops");

        Assert.Equal(RequestStatus.Draft, request.Status);

        var versionId = Guid.NewGuid();
        request.Submit(versionId);

        Assert.Equal(RequestStatus.PendingApproval, request.Status);
        Assert.Equal(versionId, request.WorkflowVersionId);
        Assert.Equal(1, request.CurrentStepNumber);
        Assert.NotNull(request.SubmittedAtUtc);
    }

    [Fact]
    public void Submit_FromApprovedStatus_ShouldThrowDomainException()
    {
        var request = new Request("REQ-2026-00001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Laptops Purchase", "Procurement of 5 Laptops");
        request.Submit(Guid.NewGuid());
        request.Approve();

        Assert.Throws<DomainException>(() => request.Submit(Guid.NewGuid()));
    }

    [Fact]
    public void AttachmentStorageService_ShouldValidateFileExtensionsCorrectly()
    {
        var service = new AttachmentStorageService();

        Assert.True(service.IsAllowedExtension("invoice.pdf"));
        Assert.True(service.IsAllowedExtension("screenshot.png"));
        Assert.True(service.IsAllowedExtension("data.xlsx"));

        Assert.False(service.IsAllowedExtension("malware.exe"));
        Assert.False(service.IsAllowedExtension("script.bat"));
        Assert.False(service.IsAllowedExtension("payload.dll"));
    }
}

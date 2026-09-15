using FlowDesk.Domain.Entities;

namespace FlowDesk.Application.Common.Interfaces;

public interface IApproverResolver
{
    Task<(Guid? AssignedUserId, Guid? AssignedRoleId)> ResolveApproverAsync(WorkflowStep step, Request request, CancellationToken cancellationToken = default);
}

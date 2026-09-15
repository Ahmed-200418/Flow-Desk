using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Infrastructure.Services;

public class ApproverResolver : IApproverResolver
{
    private readonly IApplicationDbContext _context;

    public ApproverResolver(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(Guid? AssignedUserId, Guid? AssignedRoleId)> ResolveApproverAsync(
        WorkflowStep step,
        Request request,
        CancellationToken cancellationToken = default)
    {
        if (step == null || request == null)
            return (null, null);

        switch (step.ApproverType)
        {
            case ApproverType.User:
                return (step.ApproverTargetId, null);

            case ApproverType.Role:
                return (null, step.ApproverTargetId);

            case ApproverType.Position:
                if (!step.ApproverTargetId.HasValue) return (null, null);
                var positionUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.PositionId == step.ApproverTargetId.Value && u.IsActive, cancellationToken);
                return (positionUser?.Id, null);

            case ApproverType.DepartmentManager:
                Guid? targetDeptId = request.DepartmentId;
                if (!targetDeptId.HasValue)
                {
                    var requesterUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == request.RequesterUserId, cancellationToken);
                    targetDeptId = requesterUser?.DepartmentId;
                }

                if (targetDeptId.HasValue)
                {
                    var dept = await _context.Departments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.Id == targetDeptId.Value, cancellationToken);
                    if (dept?.ManagerUserId != null)
                    {
                        return (dept.ManagerUserId, null);
                    }
                }
                return (null, null);

            case ApproverType.Manager:
                var reqUser = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.Id == request.RequesterUserId, cancellationToken);

                if (reqUser?.Department?.ManagerUserId != null && reqUser.Department.ManagerUserId != reqUser.Id)
                {
                    return (reqUser.Department.ManagerUserId, null);
                }
                return (null, null);

            default:
                return (null, null);
        }
    }
}

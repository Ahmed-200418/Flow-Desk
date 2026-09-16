using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Departments;

public record DepartmentDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Code,
    Guid? ParentDepartmentId,
    string? ParentDepartmentName,
    Guid? ManagerUserId,
    string? ManagerUserName,
    DateTime CreatedAtUtc
);

public class DepartmentHierarchyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public Guid? ManagerUserId { get; set; }
    public string? ManagerUserName { get; set; }
    public List<DepartmentHierarchyDto> SubDepartments { get; set; } = new();
}

// Create Department Command
public record CreateDepartmentCommand(
    Guid OrganizationId,
    string Name,
    string Code,
    Guid? ParentDepartmentId = null,
    Guid? ManagerUserId = null
) : IRequest<DepartmentDto>;

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}

public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IApplicationDbContext _context;

    public CreateDepartmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (request.ParentDepartmentId.HasValue)
        {
            var parentExists = await _context.Departments.AnyAsync(d => d.Id == request.ParentDepartmentId.Value && d.OrganizationId == request.OrganizationId, cancellationToken);
            if (!parentExists) throw new NotFoundException("Parent Department not found in specified organization.");
        }

        if (request.ManagerUserId.HasValue)
        {
            var managerExists = await _context.Users.AnyAsync(u => u.Id == request.ManagerUserId.Value, cancellationToken);
            if (!managerExists) throw new NotFoundException("Manager User not found.");
        }

        var department = new Department(request.OrganizationId, request.Name, code, request.ParentDepartmentId, request.ManagerUserId);
        _context.Departments.Add(department);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetDepartmentDtoAsync(department.Id, cancellationToken);
    }

    private async Task<DepartmentDto> GetDepartmentDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var dept = await _context.Departments
            .Include(d => d.ParentDepartment)
            .Include(d => d.ManagerUser)
            .FirstAsync(d => d.Id == id, cancellationToken);

        return new DepartmentDto(
            dept.Id,
            dept.OrganizationId,
            dept.Name,
            dept.Code,
            dept.ParentDepartmentId,
            dept.ParentDepartment?.Name,
            dept.ManagerUserId,
            dept.ManagerUser?.FullName,
            dept.CreatedAtUtc);
    }
}

// Assign Manager Command
public record AssignDepartmentManagerCommand(Guid DepartmentId, Guid ManagerUserId) : IRequest;

public class AssignDepartmentManagerCommandHandler : IRequestHandler<AssignDepartmentManagerCommand>
{
    private readonly IApplicationDbContext _context;

    public AssignDepartmentManagerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(AssignDepartmentManagerCommand request, CancellationToken cancellationToken)
    {
        var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken);
        if (dept == null) throw new NotFoundException(nameof(Department), request.DepartmentId);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.ManagerUserId, cancellationToken);
        if (user == null) throw new NotFoundException(nameof(User), request.ManagerUserId);

        var managerProp = typeof(Department).GetProperty(nameof(Department.ManagerUserId));
        managerProp?.SetValue(dept, request.ManagerUserId);

        await _context.SaveChangesAsync(cancellationToken);
    }
}

// Get Department Hierarchy Tree Query
public record GetDepartmentHierarchyQuery(Guid OrganizationId) : IRequest<List<DepartmentHierarchyDto>>;

public class GetDepartmentHierarchyQueryHandler : IRequestHandler<GetDepartmentHierarchyQuery, List<DepartmentHierarchyDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDepartmentHierarchyQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DepartmentHierarchyDto>> Handle(GetDepartmentHierarchyQuery request, CancellationToken cancellationToken)
    {
        var allDepts = await _context.Departments
            .AsNoTracking()
            .Include(d => d.ManagerUser)
            .Where(d => d.OrganizationId == request.OrganizationId)
            .ToListAsync(cancellationToken);

        var rootNodes = allDepts.Where(d => d.ParentDepartmentId == null).ToList();
        var result = new List<DepartmentHierarchyDto>();

        foreach (var root in rootNodes)
        {
            result.Add(BuildHierarchy(root, allDepts));
        }

        return result;
    }

    private static DepartmentHierarchyDto BuildHierarchy(Department node, List<Department> allNodes)
    {
        var dto = new DepartmentHierarchyDto
        {
            Id = node.Id,
            Name = node.Name,
            Code = node.Code,
            ManagerUserId = node.ManagerUserId,
            ManagerUserName = node.ManagerUser?.FullName
        };

        var children = allNodes.Where(d => d.ParentDepartmentId == node.Id).ToList();
        foreach (var child in children)
        {
            dto.SubDepartments.Add(BuildHierarchy(child, allNodes));
        }

        return dto;
    }
}

// Get Departments List Query
public record GetDepartmentsQuery(Guid OrganizationId) : IRequest<List<DepartmentDto>>;

public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, List<DepartmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDepartmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Departments
            .AsNoTracking()
            .Include(d => d.ParentDepartment)
            .Include(d => d.ManagerUser)
            .Where(d => d.OrganizationId == request.OrganizationId)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(
                d.Id,
                d.OrganizationId,
                d.Name,
                d.Code,
                d.ParentDepartmentId,
                d.ParentDepartment != null ? d.ParentDepartment.Name : null,
                d.ManagerUserId,
                d.ManagerUser != null ? d.ManagerUser.FullName : null,
                d.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

// Get Department By Id Query
public record GetDepartmentByIdQuery(Guid Id) : IRequest<DepartmentDto>;

public class GetDepartmentByIdQueryHandler : IRequestHandler<GetDepartmentByIdQuery, DepartmentDto>
{
    private readonly IApplicationDbContext _context;

    public GetDepartmentByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DepartmentDto> Handle(GetDepartmentByIdQuery request, CancellationToken cancellationToken)
    {
        var dept = await _context.Departments
            .AsNoTracking()
            .Include(d => d.ParentDepartment)
            .Include(d => d.ManagerUser)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (dept == null) throw new NotFoundException(nameof(Department), request.Id);

        return new DepartmentDto(
            dept.Id,
            dept.OrganizationId,
            dept.Name,
            dept.Code,
            dept.ParentDepartmentId,
            dept.ParentDepartment?.Name,
            dept.ManagerUserId,
            dept.ManagerUser?.FullName,
            dept.CreatedAtUtc);
    }
}

// Update Department Command
public record UpdateDepartmentCommand(
    Guid Id,
    string Name,
    Guid? ParentDepartmentId = null,
    Guid? ManagerUserId = null
) : IRequest<DepartmentDto>;

public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateDepartmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (dept == null) throw new NotFoundException(nameof(Department), request.Id);

        if (request.ParentDepartmentId.HasValue && request.ParentDepartmentId.Value != dept.Id)
        {
            var parentExists = await _context.Departments.AnyAsync(d => d.Id == request.ParentDepartmentId.Value, cancellationToken);
            if (!parentExists) throw new NotFoundException("Parent Department not found.");
            var parentProp = typeof(Department).GetProperty(nameof(Department.ParentDepartmentId));
            parentProp?.SetValue(dept, request.ParentDepartmentId);
        }
        else if (!request.ParentDepartmentId.HasValue)
        {
            var parentProp = typeof(Department).GetProperty(nameof(Department.ParentDepartmentId));
            parentProp?.SetValue(dept, (Guid?)null);
        }

        var nameProp = typeof(Department).GetProperty(nameof(Department.Name));
        nameProp?.SetValue(dept, request.Name.Trim());

        var mgrProp = typeof(Department).GetProperty(nameof(Department.ManagerUserId));
        mgrProp?.SetValue(dept, request.ManagerUserId);

        await _context.SaveChangesAsync(cancellationToken);

        var updated = await _context.Departments
            .Include(d => d.ParentDepartment)
            .Include(d => d.ManagerUser)
            .FirstAsync(d => d.Id == dept.Id, cancellationToken);

        return new DepartmentDto(
            updated.Id,
            updated.OrganizationId,
            updated.Name,
            updated.Code,
            updated.ParentDepartmentId,
            updated.ParentDepartment?.Name,
            updated.ManagerUserId,
            updated.ManagerUser?.FullName,
            updated.CreatedAtUtc);
    }
}

// Delete Department Command
public record DeleteDepartmentCommand(Guid Id) : IRequest;

public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteDepartmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (dept == null) throw new NotFoundException(nameof(Department), request.Id);

        _context.Departments.Remove(dept);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

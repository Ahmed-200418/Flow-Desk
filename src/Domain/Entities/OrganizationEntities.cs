using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Entities;

public class Organization : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<Department> Departments { get; private set; } = new List<Department>();
    public ICollection<RequestType> RequestTypes { get; private set; } = new List<RequestType>();
    public ICollection<Workflow> Workflows { get; private set; } = new List<Workflow>();

    private Organization() { }

    public Organization(string name, string code, string? description = null)
    {
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = description?.Trim();
        IsActive = true;
    }
}

public class Department : AuditableEntity
{
    public Guid OrganizationId { get; private set; }
    public Organization Organization { get; private set; } = default!;

    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    
    public Guid? ParentDepartmentId { get; private set; }
    public Department? ParentDepartment { get; private set; }
    public ICollection<Department> SubDepartments { get; private set; } = new List<Department>();

    public Guid? ManagerUserId { get; private set; }
    public User? ManagerUser { get; private set; }

    public ICollection<Position> Positions { get; private set; } = new List<Position>();
    public ICollection<User> Users { get; private set; } = new List<User>();

    private Department() { }

    public Department(Guid organizationId, string name, string code, Guid? parentDepartmentId = null, Guid? managerUserId = null)
    {
        OrganizationId = organizationId;
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        ParentDepartmentId = parentDepartmentId;
        ManagerUserId = managerUserId;
    }
}

public class Position : AuditableEntity
{
    public Guid DepartmentId { get; private set; }
    public Department Department { get; private set; } = default!;

    public string Title { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public int Level { get; private set; } = 1;

    private Position() { }

    public Position(Guid departmentId, string title, string code, int level = 1)
    {
        DepartmentId = departmentId;
        Title = title.Trim();
        Code = code.Trim().ToUpperInvariant();
        Level = level;
    }
}

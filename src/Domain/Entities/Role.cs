using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Entities;

public class Role : Entity
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public bool IsSystemRole { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    private Role() { }

    public Role(string name, string description, bool isSystemRole = false)
    {
        Name = name;
        Description = description;
        IsSystemRole = isSystemRole;
    }
}

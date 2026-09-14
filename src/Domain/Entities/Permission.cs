using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Entities;

public class Permission : Entity
{
    public string Code { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public string Description { get; private set; } = default!;

    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    private Permission() { }

    public Permission(string code, string category, string description)
    {
        Code = code;
        Category = category;
        Description = description;
    }
}

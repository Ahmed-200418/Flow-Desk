namespace FlowDesk.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

public abstract class Entity<TId>
{
    public TId Id { get; protected set; } = default!;
}

namespace RepoNavAI.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; }
    public DateTimeOffset CreatedAtUtc { get; protected set; }
    public DateTimeOffset UpdatedAtUtc { get; protected set; }

    protected Entity() { }

    protected Entity(Guid id)
    {
        Id = id;
        CreatedAtUtc = UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    protected void MarkUpdated() => UpdatedAtUtc = DateTimeOffset.UtcNow;
}

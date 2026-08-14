using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Repositories;

public sealed class RepositoryRemovalAudit : Entity
{
    private RepositoryRemovalAudit() { }

    public RepositoryRemovalAudit(Guid organizationId, Guid repositoryId, Guid actorUserId, RepositoryProvider provider, string owner, string name, DateTimeOffset removedAtUtc)
        : base(Guid.NewGuid())
    {
        OrganizationId = organizationId;
        RepositoryId = repositoryId;
        ActorUserId = actorUserId;
        Provider = provider;
        Owner = owner;
        Name = name;
        RemovedAtUtc = removedAtUtc;
    }

    public Guid OrganizationId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public RepositoryProvider Provider { get; private set; }
    public string Owner { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset RemovedAtUtc { get; private set; }
}

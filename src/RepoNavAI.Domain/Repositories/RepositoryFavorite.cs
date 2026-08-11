using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Repositories;

public sealed class RepositoryFavorite : Entity
{
    private RepositoryFavorite() { }

    public RepositoryFavorite(Guid organizationId, Guid repositoryId, Guid userId) : base(Guid.NewGuid())
    {
        OrganizationId = organizationId;
        RepositoryId = repositoryId;
        UserId = userId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid UserId { get; private set; }
}

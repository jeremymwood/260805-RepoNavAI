using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Repositories;

public sealed class RepositoryIndexingRequest : Entity
{
    private RepositoryIndexingRequest() { }

    public RepositoryIndexingRequest(Guid organizationId, Guid repositoryId, Guid requestedByUserId) : base(Guid.NewGuid())
    {
        OrganizationId = organizationId;
        RepositoryId = repositoryId;
        RequestedByUserId = requestedByUserId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public IndexingRequestStatus Status { get; private set; } = IndexingRequestStatus.Pending;
    public RegisteredRepository Repository { get; private set; } = null!;
}

public enum IndexingRequestStatus { Pending = 1, Processing = 2, Completed = 3, Failed = 4, Cancelled = 5 }

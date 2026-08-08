using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Repositories;

public enum RepositoryChatStatus { Streaming, Completed, Cancelled, Failed }

public sealed class RepositoryChatSession : Entity
{
    private RepositoryChatSession() { }
    public RepositoryChatSession(Guid organizationId, Guid repositoryId, Guid userId, string model, DateTimeOffset createdAtUtc) : base(Guid.NewGuid())
    {
        OrganizationId = organizationId; RepositoryId = repositoryId; UserId = userId; Model = model; CreatedAtUtc = UpdatedAtUtc = createdAtUtc;
    }

    public Guid OrganizationId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid UserId { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public RepositoryChatStatus Status { get; private set; } = RepositoryChatStatus.Streaming;
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void Finish(RepositoryChatStatus status, DateTimeOffset completedAtUtc)
    {
        if (status == RepositoryChatStatus.Streaming) throw new ArgumentOutOfRangeException(nameof(status));
        if (Status != RepositoryChatStatus.Streaming) return;
        Status = status; CompletedAtUtc = UpdatedAtUtc = completedAtUtc;
    }
}

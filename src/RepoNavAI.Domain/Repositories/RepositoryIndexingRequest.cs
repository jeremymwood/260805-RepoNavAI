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
    public IndexingCheckpoint Checkpoint { get; private set; } = IndexingCheckpoint.Queued;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
    public Guid? LeaseOwnerId { get; private set; }
    public DateTimeOffset? CancellationRequestedAtUtc { get; private set; }
    public string? CommitSha { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public RegisteredRepository Repository { get; private set; } = null!;

    public bool IsCancellationRequested => CancellationRequestedAtUtc is not null;

    public void Start(DateTimeOffset now, TimeSpan lease, Guid leaseOwnerId)
    {
        if (leaseOwnerId == Guid.Empty) throw new ArgumentException("A lease owner is required.", nameof(leaseOwnerId));
        if (Status == IndexingRequestStatus.Processing && LeaseExpiresAtUtc >= now) throw new InvalidOperationException("An active indexing lease cannot be claimed by another worker.");
        Status = IndexingRequestStatus.Processing;
        Checkpoint = IndexingCheckpoint.Acquiring;
        StartedAtUtc ??= now;
        LeaseExpiresAtUtc = now.Add(lease);
        LeaseOwnerId = leaseOwnerId;
        AttemptCount++;
        ErrorCode = ErrorMessage = null;
        MarkUpdated();
    }

    public bool RenewLease(Guid leaseOwnerId, DateTimeOffset now, TimeSpan lease)
    {
        if (Status != IndexingRequestStatus.Processing || LeaseOwnerId != leaseOwnerId || LeaseExpiresAtUtc <= now) return false;
        LeaseExpiresAtUtc = now.Add(lease);
        MarkUpdated();
        return true;
    }

    public void Advance(IndexingCheckpoint checkpoint, DateTimeOffset now, TimeSpan lease, string? commitSha = null)
    {
        if (Status != IndexingRequestStatus.Processing) throw new InvalidOperationException("Only processing jobs can advance.");
        Checkpoint = checkpoint;
        LeaseExpiresAtUtc = now.Add(lease);
        CommitSha = commitSha ?? CommitSha;
        MarkUpdated();
    }

    public void Complete(string commitSha, DateTimeOffset now)
    {
        Status = IndexingRequestStatus.Completed; Checkpoint = IndexingCheckpoint.Completed; CommitSha = commitSha;
        CompletedAtUtc = now; LeaseExpiresAtUtc = null; LeaseOwnerId = null; MarkUpdated();
    }

    public void RequestCancellation(DateTimeOffset now)
    {
        if (Status is not (IndexingRequestStatus.Pending or IndexingRequestStatus.Processing)) throw new InvalidOperationException("Indexing request is already final.");
        CancellationRequestedAtUtc = now;
        if (Status == IndexingRequestStatus.Pending) Cancel(now);
        MarkUpdated();
    }

    public void Cancel(DateTimeOffset now)
    {
        Status = IndexingRequestStatus.Cancelled; Checkpoint = IndexingCheckpoint.Cancelled;
        CompletedAtUtc = now; LeaseExpiresAtUtc = null; LeaseOwnerId = null; MarkUpdated();
    }

    public void Fail(string code, string message, DateTimeOffset now, int maxAttempts)
    {
        ErrorCode = code; ErrorMessage = message; LeaseExpiresAtUtc = null; LeaseOwnerId = null;
        Status = AttemptCount < maxAttempts ? IndexingRequestStatus.Pending : IndexingRequestStatus.Failed;
        Checkpoint = Status == IndexingRequestStatus.Pending ? IndexingCheckpoint.Queued : IndexingCheckpoint.Failed;
        if (Status == IndexingRequestStatus.Failed) CompletedAtUtc = now;
        MarkUpdated();
    }

    public void Retry()
    {
        if (Status is not (IndexingRequestStatus.Failed or IndexingRequestStatus.Cancelled)) throw new InvalidOperationException("Only failed or cancelled requests can be retried.");
        Status = IndexingRequestStatus.Pending; Checkpoint = IndexingCheckpoint.Queued; CompletedAtUtc = null;
        CancellationRequestedAtUtc = null; ErrorCode = ErrorMessage = null; AttemptCount = 0; StartedAtUtc = null; LeaseOwnerId = null; MarkUpdated();
    }
}

public enum IndexingRequestStatus { Pending = 1, Processing = 2, Completed = 3, Failed = 4, Cancelled = 5 }
public enum IndexingCheckpoint { Queued = 1, Acquiring = 2, Parsing = 3, Persisting = 4, Completed = 5, Failed = 6, Cancelled = 7 }

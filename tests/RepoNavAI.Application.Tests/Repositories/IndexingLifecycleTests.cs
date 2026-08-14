using FluentAssertions;
using RepoNavAI.Domain.Repositories;
using Xunit;

namespace RepoNavAI.Application.Tests.Repositories;

public sealed class IndexingLifecycleTests
{
    [Fact]
    public void ExpiredJobCanBeStartedAgainWithoutResettingOriginalStartTime()
    {
        var job = new RepositoryIndexingRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); var first = DateTimeOffset.UtcNow;
        job.Start(first, TimeSpan.FromMinutes(1), Guid.NewGuid()); job.Start(first.AddMinutes(2), TimeSpan.FromMinutes(1), Guid.NewGuid());
        job.AttemptCount.Should().Be(2); job.StartedAtUtc.Should().Be(first); job.Status.Should().Be(IndexingRequestStatus.Processing);
    }

    [Fact]
    public void FailureRetriesUntilMaximumAttemptsThenBecomesActionableFailure()
    {
        var job = new RepositoryIndexingRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); var now = DateTimeOffset.UtcNow;
        for (var attempt = 1; attempt <= 3; attempt++) { job.Start(now, TimeSpan.FromMinutes(1), Guid.NewGuid()); job.Fail("INDEXING_FAILED", "Safe message", now, 3); }
        job.Status.Should().Be(IndexingRequestStatus.Failed); job.ErrorMessage.Should().Be("Safe message"); job.AttemptCount.Should().Be(3);
    }

    [Fact]
    public void DeterministicFailureStopsAfterCurrentAttempt()
    {
        var job = new RepositoryIndexingRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); var now = DateTimeOffset.UtcNow;
        job.Start(now, TimeSpan.FromMinutes(1), Guid.NewGuid());

        job.Fail("ARCHIVE_MALFORMED", "The archive is malformed.", now, job.AttemptCount);

        job.Status.Should().Be(IndexingRequestStatus.Failed);
        job.AttemptCount.Should().Be(1);
        job.ErrorCode.Should().Be("ARCHIVE_MALFORMED");
        job.ErrorMessage.Should().Be("The archive is malformed.");
    }

    [Fact]
    public void LeaseRenewal_RequiresCurrentOwnerAndUnexpiredProcessingLease()
    {
        var now = DateTimeOffset.UtcNow; var owner = Guid.NewGuid();
        var job = new RepositoryIndexingRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        job.Start(now, TimeSpan.FromSeconds(45), owner);

        job.RenewLease(Guid.NewGuid(), now.AddSeconds(10), TimeSpan.FromSeconds(45)).Should().BeFalse();
        job.RenewLease(owner, now.AddSeconds(10), TimeSpan.FromSeconds(45)).Should().BeTrue();
        job.LeaseExpiresAtUtc.Should().Be(now.AddSeconds(55));
        job.RenewLease(owner, now.AddMinutes(1), TimeSpan.FromSeconds(45)).Should().BeFalse();
    }

    [Fact]
    public void CompetingWorker_CannotClaimUntilExistingLeaseExpires()
    {
        var now = DateTimeOffset.UtcNow; var job = new RepositoryIndexingRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        job.Start(now, TimeSpan.FromSeconds(45), Guid.NewGuid());

        var competingClaim = () => job.Start(now.AddSeconds(44), TimeSpan.FromSeconds(45), Guid.NewGuid());
        competingClaim.Should().Throw<InvalidOperationException>();

        job.Start(now.AddSeconds(46), TimeSpan.FromSeconds(45), Guid.NewGuid());
        job.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void CompletingJob_ReleasesLeaseOwnership()
    {
        var now = DateTimeOffset.UtcNow; var job = new RepositoryIndexingRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        job.Start(now, TimeSpan.FromSeconds(45), Guid.NewGuid());
        job.Complete("abcdef12", now.AddSeconds(1));
        job.LeaseOwnerId.Should().BeNull();
        job.LeaseExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public void PendingCancellationIsFinalButCanBeRetried()
    {
        var job = new RepositoryIndexingRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); job.RequestCancellation(DateTimeOffset.UtcNow);
        job.Status.Should().Be(IndexingRequestStatus.Cancelled); job.Retry(); job.Status.Should().Be(IndexingRequestStatus.Pending); job.IsCancellationRequested.Should().BeFalse();
    }
}

public sealed class RepositoryChatSessionLifecycleTests
{
    [Fact]
    public void Finish_RecordsTerminalStatusAndTimestamp()
    {
        var started = DateTimeOffset.UtcNow; var finished = started.AddSeconds(2);
        var session = new RepositoryChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "test-model", started);

        session.Finish(RepositoryChatStatus.Completed, finished);

        session.Status.Should().Be(RepositoryChatStatus.Completed);
        session.CompletedAtUtc.Should().Be(finished);
        session.UpdatedAtUtc.Should().Be(finished);
    }

    [Fact]
    public void Finish_RejectsNonTerminalStatus()
    {
        var session = new RepositoryChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "test-model", DateTimeOffset.UtcNow);
        var act = () => session.Finish(RepositoryChatStatus.Streaming, DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

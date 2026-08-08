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
        job.Start(first, TimeSpan.FromMinutes(1)); job.Start(first.AddMinutes(2), TimeSpan.FromMinutes(1));
        job.AttemptCount.Should().Be(2); job.StartedAtUtc.Should().Be(first); job.Status.Should().Be(IndexingRequestStatus.Processing);
    }

    [Fact]
    public void FailureRetriesUntilMaximumAttemptsThenBecomesActionableFailure()
    {
        var job = new RepositoryIndexingRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); var now = DateTimeOffset.UtcNow;
        for (var attempt = 1; attempt <= 3; attempt++) { job.Start(now, TimeSpan.FromMinutes(1)); job.Fail("INDEXING_FAILED", "Safe message", now, 3); }
        job.Status.Should().Be(IndexingRequestStatus.Failed); job.ErrorMessage.Should().Be("Safe message"); job.AttemptCount.Should().Be(3);
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

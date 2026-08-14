using System.Text.Json;
using FluentAssertions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using Xunit;

namespace RepoNavAI.Application.Tests.Repositories;

public sealed class RepositoryAssistantHistoryTests
{
    [Fact]
    public void Entry_BoundsDefaultTitleAndControlsLifecycleMetadata()
    {
        var now = DateTimeOffset.UtcNow; var prompt = new string('x', 200);
        var entry = new RepositoryAssistantHistory(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RepositoryAssistantHistoryMode.Search, prompt, "abc123", now);

        entry.DisplayTitle.Should().HaveLength(120).And.EndWith("...");
        entry.Rename("Important result", now.AddMinutes(1)); entry.SetStarred(true, now.AddMinutes(2));
        entry.Complete(RepositoryAssistantHistorySchemas.SearchV1, "{\"results\":[]}", null, now.AddMinutes(3));

        entry.DisplayTitle.Should().Be("Important result"); entry.IsStarred.Should().BeTrue();
        entry.Status.Should().Be(RepositoryAssistantHistoryStatus.Completed); entry.CompletedAtUtc.Should().Be(now.AddMinutes(3));
    }

    [Fact]
    public void Summary_MarksOlderCommitsAndUnsupportedContracts()
    {
        var entry = new RepositoryAssistantHistory(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RepositoryAssistantHistoryMode.Answer, "Question", "old", DateTimeOffset.UtcNow);
        entry.Complete("answer/99", "{}", null, DateTimeOffset.UtcNow);

        var summary = RepositoryAssistantHistoryMapping.ToSummary(entry, "new");

        summary.IsStale.Should().BeTrue(); summary.IsSupported.Should().BeFalse();
        RepositoryAssistantHistorySchemas.IsSupported(RepositoryAssistantHistoryMode.Search, RepositoryAssistantHistorySchemas.SearchV1).Should().BeTrue();
        RepositoryAssistantHistorySchemas.IsSupported(RepositoryAssistantHistoryMode.CodeFlow, RepositoryAssistantHistorySchemas.CodeFlowV1).Should().BeTrue();
    }

    [Fact]
    public void SafeStoredContracts_ContainCitationMetadataButNotRetrievedSourceContent()
    {
        var result = new StoredSearchHistory([new("src/Auth.cs", 10, 20, "abc123", "https://example.test/source", .9)]);

        var json = JsonSerializer.Serialize(result);
        var codeFlow = JsonSerializer.Serialize(new StoredCodeFlowHistory("1.0", Guid.NewGuid(), "abc123", "Summary", [], []));

        json.Should().Contain("src/Auth.cs").And.NotContain("Content").And.NotContain("source chunk");
        codeFlow.Should().NotContain("Sources").And.NotContain("Content");
    }
}

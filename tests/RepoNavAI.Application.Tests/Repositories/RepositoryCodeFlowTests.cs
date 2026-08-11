using FluentAssertions;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using Xunit;

namespace RepoNavAI.Application.Tests.Repositories;

public sealed class RepositoryCodeFlowTests
{
    private static readonly SemanticSearchResult Source = new(Guid.NewGuid(), "src/Worker.cs", 10, 20, "content", .9, "abc123", "https://example.test/source");

    [Fact]
    public void Map_ResolvesProviderCitationAgainstTrustedSourceMetadata()
    {
        var draft = new CodeFlowDraft("Summary", [new("start", "Start", "API", "Run", "Handles request", "Calls worker",
            CodeFlowBoundary.Synchronous, OrientationEvidenceLevel.Confirmed, [1])], []);

        var result = CodeFlowMapping.Map(Guid.NewGuid(), "abc123", draft, [Source]);

        result.Steps.Single().Citations.Single().Should().Be(new OrientationCitation(Source.Path, Source.StartLine, Source.EndLine, Source.CommitSha, Source.SourceUrl));
        result.Sources.Should().ContainSingle().Which.Should().Be(Source);
    }

    [Fact]
    public void Map_RejectsCitationOutsideRetrievedEvidence()
    {
        var draft = new CodeFlowDraft("Summary", [new("start", "Start", "API", "Run", "Handles request", "Calls worker",
            CodeFlowBoundary.Synchronous, OrientationEvidenceLevel.Confirmed, [2])], []);

        var act = () => CodeFlowMapping.Map(Guid.NewGuid(), "abc123", draft, [Source]);

        act.Should().Throw<ExternalServiceException>().WithMessage("*invalid citation*");
    }

    [Fact]
    public void Map_RejectsConfirmedStepWithoutCitation()
    {
        var draft = new CodeFlowDraft("Summary", [new("start", "Start", "API", "Run", "Handles request", "Calls worker",
            CodeFlowBoundary.Synchronous, OrientationEvidenceLevel.Confirmed, [])], []);

        var act = () => CodeFlowMapping.Map(Guid.NewGuid(), "abc123", draft, [Source]);

        act.Should().Throw<ExternalServiceException>().WithMessage("*unsupported confirmed step*");
    }
}

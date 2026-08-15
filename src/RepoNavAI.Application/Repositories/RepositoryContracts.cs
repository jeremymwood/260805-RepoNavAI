using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Application.Repositories;

public sealed record GitHubRepositoryAddress(string Owner, string Name)
{
    public static bool TryParse(string? value, out GitHubRepositoryAddress? address)
    {
        address = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2) return false;
        var name = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
        if (string.IsNullOrWhiteSpace(segments[0]) || string.IsNullOrWhiteSpace(name)) return false;
        address = new GitHubRepositoryAddress(segments[0].ToLowerInvariant(), name.ToLowerInvariant());
        return true;
    }
}

public sealed record ProviderRepository(string ProviderRepositoryId, string Owner, string Name, string DefaultBranch, RepositoryVisibility Visibility, string WebUrl);
public sealed record RepositoryDto(Guid Id, Guid OrganizationId, string Owner, string Name, string FullName, string DefaultBranch, RepositoryVisibility Visibility, string WebUrl, IndexingRequestStatus IndexingStatus, DateTimeOffset RegisteredAtUtc, IndexingCheckpoint IndexingCheckpoint = IndexingCheckpoint.Queued, string? CommitSha = null, string? ErrorMessage = null, bool IsFavorite = false);
public sealed record RepositoryPage(IReadOnlyCollection<RepositoryDto> Items, int Page, int PageSize, int TotalCount)
{
    public bool HasMore => Page * PageSize < TotalCount;
}
public sealed record IndexingRequestDto(Guid Id, Guid RepositoryId, IndexingRequestStatus Status, IndexingCheckpoint Checkpoint, int AttemptCount, string? CommitSha, string? ErrorCode, string? ErrorMessage, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);
public sealed record RepositorySourceFile(string Path, string Language, byte[] Content);
public sealed record RepositoryLanguageCoverage(string Language, int Indexed, int SkippedUnsupported, int SkippedExcluded, int SkippedBinary);
public sealed record ParsedSymbol(string Name, string QualifiedName, SymbolKind Kind, int Line);
public sealed record RepositorySnapshotData(string CommitSha, IReadOnlyCollection<RepositorySourceFile> Files, IReadOnlyCollection<RepositoryLanguageCoverage>? Coverage = null);
public sealed record ParsedEndpoint(string HttpMethod, string Route, string Handler, string Path, int Line, bool RequiresAuthorization, IReadOnlyCollection<string> DownstreamSymbols);
public sealed record RepositoryEndpointDto(Guid Id, string HttpMethod, string Route, string Handler, string Path, int Line, bool RequiresAuthorization, IReadOnlyCollection<string> DownstreamSymbols, string CommitSha, string SourceUrl);
public sealed record RepositoryCapabilitiesDto(bool HasIndexedContent, bool HasSourceCode, bool HasTests, bool HasDocumentation, bool HasApiEndpoints, IReadOnlyCollection<string> RepresentativePaths, string CoverageStatus = "none", IReadOnlyCollection<RepositoryLanguageCoverage>? Languages = null);
public sealed record RepositoryArchitectureNode(string Id, string Label, string Kind, string? Path, string? Language, int ChildCount, string? SourceUrl);
public sealed record RepositoryArchitectureEdge(string Id, string SourceId, string TargetId, string Kind, string Label);
public sealed record RepositoryArchitectureGraphDto(string SchemaVersion, string CommitSha, bool IsTruncated, int TotalNodeCount,
    IReadOnlyCollection<RepositoryArchitectureNode> Nodes, IReadOnlyCollection<RepositoryArchitectureEdge> Edges);
public sealed record TextChunk(int Ordinal, int StartLine, int EndLine, string Content);
public sealed record SemanticSearchResult(Guid ChunkId, string Path, int StartLine, int EndLine, string Content, double Score, string CommitSha, string SourceUrl);
public sealed record RepositoryChatCitation(int Number, string Path, int StartLine, int EndLine, string CommitSha, string SourceUrl, double Score);
public sealed record OrientationProfile(OrientationRole Role, OrientationExperience Experience, OrientationFocus Focus, int TimeBudgetMinutes, string? Objective);
public sealed record OrientationDraftStep(string Key, string Title, string Objective, string Evidence, OrientationEvidenceLevel EvidenceLevel, IReadOnlyCollection<int> CitationNumbers);
public sealed record OrientationDraft(string Summary, IReadOnlyCollection<OrientationDraftStep> Steps, IReadOnlyCollection<string> MissingEvidence);
public sealed record OrientationCitation(string Path, int StartLine, int EndLine, string CommitSha, string SourceUrl);
public sealed record OrientationStep(string Key, string Title, string Objective, string Evidence, OrientationEvidenceLevel EvidenceLevel, IReadOnlyCollection<OrientationCitation> Citations, bool Completed);
public sealed record OrientationPlanDto(Guid Id, Guid RepositoryId, string CommitSha, OrientationRole Role, OrientationExperience Experience, OrientationFocus Focus, int TimeBudgetMinutes, string Summary, IReadOnlyCollection<OrientationStep> Steps, IReadOnlyCollection<string> MissingEvidence, bool IsStale, DateTimeOffset CreatedAtUtc);
public sealed record RepositorySnapshotReference(Guid Id, string CommitSha);
public enum CodeFlowBoundary { Synchronous, Asynchronous, Background, Persistence, External }
public sealed record CodeFlowDraftStep(string Key, string Title, string Component, string Symbol, string Responsibility,
    string Handoff, CodeFlowBoundary Boundary, OrientationEvidenceLevel EvidenceLevel, IReadOnlyCollection<int> CitationNumbers);
public sealed record CodeFlowDraft(string Summary, IReadOnlyCollection<CodeFlowDraftStep> Steps, IReadOnlyCollection<string> MissingEvidence);
public sealed record CodeFlowStep(string Key, int Order, string Title, string Component, string Symbol, string Responsibility,
    string Handoff, CodeFlowBoundary Boundary, OrientationEvidenceLevel EvidenceLevel, IReadOnlyCollection<OrientationCitation> Citations);
public sealed record CodeFlowTraceDto(string SchemaVersion, Guid RepositoryId, string CommitSha, string Summary,
    IReadOnlyCollection<CodeFlowStep> Steps, IReadOnlyCollection<string> MissingEvidence, IReadOnlyCollection<SemanticSearchResult> Sources);
public sealed record RepositoryAssistantHistorySummaryDto(Guid Id, RepositoryAssistantHistoryMode Mode,
    RepositoryAssistantHistoryStatus Status, string Prompt, string DisplayTitle, string CommitSha, string? SchemaVersion,
    bool IsStarred, bool IsStale, bool IsSupported, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);
public sealed record RepositoryAssistantHistoryPage(IReadOnlyCollection<RepositoryAssistantHistorySummaryDto> Items,
    int Page, int PageSize, int TotalCount)
{
    public bool HasMore => Page * PageSize < TotalCount;
}
public sealed record RepositoryAssistantHistoryDetailDto(RepositoryAssistantHistorySummaryDto Summary, object? Result);
public sealed record StoredAssistantCitation(string Path, int StartLine, int EndLine, string CommitSha, string SourceUrl, double? Score = null, int? Number = null);
public sealed record StoredSearchHistory(IReadOnlyCollection<StoredAssistantCitation> Results);
public sealed record StoredAnswerHistory(string Answer, IReadOnlyCollection<StoredAssistantCitation> Citations);
public sealed record StoredCodeFlowHistory(string SchemaVersion, Guid RepositoryId, string CommitSha, string Summary,
    IReadOnlyCollection<CodeFlowStep> Steps, IReadOnlyCollection<string> MissingEvidence);

public enum RepositoryChatEventType { Citations, Delta, Completed, Error }
public sealed record RepositoryChatEvent(RepositoryChatEventType Type, string? Delta = null, IReadOnlyCollection<RepositoryChatCitation>? Citations = null);

public interface IRepositoryProvider
{
    Task<ProviderRepository?> GetAsync(GitHubRepositoryAddress address, CancellationToken cancellationToken);
}

public interface IRepositoryRegistrationRepository
{
    Task<bool> ExistsAsync(Guid organizationId, string owner, string name, CancellationToken cancellationToken);
    Task AddAsync(RegisteredRepository repository, RepositoryIndexingRequest indexingRequest, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IRepositoryRemovalStore
{
    Task RemoveAsync(Guid organizationId, Guid repositoryId, Guid actorUserId, string confirmation, DateTimeOffset removedAtUtc, CancellationToken cancellationToken);
}

public interface IRepositoryQueries
{
    Task<RepositoryPage> ListAsync(Guid organizationId, Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<IndexingRequestDto?> GetIndexingRequestAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RepositoryEndpointDto>> ListEndpointsAsync(Guid organizationId, Guid repositoryId, string? method, string? search, bool? requiresAuthorization, CancellationToken cancellationToken);
    Task<RepositoryCapabilitiesDto> GetCapabilitiesAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken);
    Task<RepositoryArchitectureGraphDto> GetArchitectureAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken);
}

public interface IRepositoryFavoriteStore
{
    Task SetAsync(Guid organizationId, Guid repositoryId, Guid userId, bool isFavorite, CancellationToken cancellationToken);
}

public interface IIndexingRequestRepository
{
    Task<RepositoryIndexingRequest?> GetLatestAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken);
    Task AddAsync(RepositoryIndexingRequest request, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IRepositorySnapshotProvider
{
    Task<RepositorySnapshotData> FetchAsync(string owner, string name, string branch, CancellationToken cancellationToken);
}

public interface ISourceSymbolParser { IReadOnlyCollection<ParsedSymbol> Parse(string path, byte[] content); }
public interface IRepositoryEndpointAnalyzer { IReadOnlyCollection<ParsedEndpoint> Analyze(IReadOnlyCollection<RepositorySourceFile> files); }
public interface ISourceChunker { IReadOnlyCollection<TextChunk> Chunk(string path, string content); }
public interface IEmbeddingGenerator { string Model { get; } int Dimensions { get; } bool IsConfigured { get; } Task<IReadOnlyList<float[]>> GenerateAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken); }
public interface IVectorStore
{
    Task UpsertAsync(Guid organizationId, Guid repositoryId, Guid snapshotId, IReadOnlyCollection<(Guid ChunkId, float[] Embedding)> embeddings, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SemanticSearchResult>> SearchAsync(Guid organizationId, Guid repositoryId, float[] query, int limit, CancellationToken cancellationToken);
}

public interface IRepositoryAnswerGenerator
{
    bool IsConfigured { get; }
    string Model { get; }
    IAsyncEnumerable<string> StreamAsync(string question, IReadOnlyCollection<SemanticSearchResult> sources, CancellationToken cancellationToken);
}

public interface IRepositoryOrientationGenerator
{
    bool IsConfigured { get; }
    string Model { get; }
    Task<OrientationDraft> GenerateAsync(OrientationProfile profile, IReadOnlyCollection<SemanticSearchResult> sources, CancellationToken cancellationToken);
}

public interface IRepositoryCodeFlowGenerator
{
    bool IsConfigured { get; }
    string Model { get; }
    Task<CodeFlowDraft> GenerateAsync(string question, IReadOnlyCollection<SemanticSearchResult> sources, CancellationToken cancellationToken);
}

public interface IRepositoryOrientationStore
{
    Task<RepositorySnapshotReference?> GetLatestSnapshotAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken);
    Task<RepositoryOrientationPlan?> GetLatestAsync(Guid organizationId, Guid repositoryId, Guid userId, CancellationToken cancellationToken);
    Task<RepositoryOrientationPlan?> GetAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid planId, CancellationToken cancellationToken);
    Task AddAsync(RepositoryOrientationPlan plan, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IRepositoryChatSessionStore
{
    Task<Guid> StartAsync(Guid organizationId, Guid repositoryId, Guid userId, string model, CancellationToken cancellationToken);
    Task FinishAsync(Guid sessionId, RepositoryChatStatus status, CancellationToken cancellationToken);
}

public interface IRepositoryAssistantHistoryStore
{
    Task<RepositoryAssistantHistory> StartAsync(Guid organizationId, Guid repositoryId, Guid userId,
        RepositoryAssistantHistoryMode mode, string prompt, string commitSha, CancellationToken cancellationToken);
    Task CompleteAsync(Guid historyId, string schemaVersion, string? resultJson, Guid? orientationPlanId, CancellationToken cancellationToken);
    Task FinishIncompleteAsync(Guid historyId, RepositoryAssistantHistoryStatus status, CancellationToken cancellationToken);
    Task<RepositoryAssistantHistoryPage> ListAsync(Guid organizationId, Guid repositoryId, Guid userId, string? latestCommitSha, int page, int pageSize, CancellationToken cancellationToken);
    Task<RepositoryAssistantHistory?> GetAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, CancellationToken cancellationToken);
    Task SetStarredAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, bool isStarred, CancellationToken cancellationToken);
    Task RenameAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, string title, CancellationToken cancellationToken);
    Task DeleteAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, CancellationToken cancellationToken);
    Task ClearAsync(Guid organizationId, Guid repositoryId, Guid userId, CancellationToken cancellationToken);
}

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
public sealed record RepositoryDto(Guid Id, Guid OrganizationId, string Owner, string Name, string FullName, string DefaultBranch, RepositoryVisibility Visibility, string WebUrl, IndexingRequestStatus IndexingStatus, DateTimeOffset RegisteredAtUtc, IndexingCheckpoint IndexingCheckpoint = IndexingCheckpoint.Queued, string? CommitSha = null, string? ErrorMessage = null);
public sealed record IndexingRequestDto(Guid Id, Guid RepositoryId, IndexingRequestStatus Status, IndexingCheckpoint Checkpoint, int AttemptCount, string? CommitSha, string? ErrorCode, string? ErrorMessage, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);
public sealed record RepositorySourceFile(string Path, string Language, byte[] Content);
public sealed record ParsedSymbol(string Name, string QualifiedName, SymbolKind Kind, int Line);
public sealed record RepositorySnapshotData(string CommitSha, IReadOnlyCollection<RepositorySourceFile> Files);
public sealed record ParsedEndpoint(string HttpMethod, string Route, string Handler, string Path, int Line, bool RequiresAuthorization, IReadOnlyCollection<string> DownstreamSymbols);
public sealed record RepositoryEndpointDto(Guid Id, string HttpMethod, string Route, string Handler, string Path, int Line, bool RequiresAuthorization, IReadOnlyCollection<string> DownstreamSymbols, string CommitSha, string SourceUrl);
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

public interface IRepositoryQueries
{
    Task<IReadOnlyCollection<RepositoryDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IndexingRequestDto?> GetIndexingRequestAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RepositoryEndpointDto>> ListEndpointsAsync(Guid organizationId, Guid repositoryId, string? method, string? search, bool? requiresAuthorization, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken);
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

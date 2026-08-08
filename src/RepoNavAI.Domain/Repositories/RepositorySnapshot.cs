using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Repositories;

public sealed class RepositorySnapshot : Entity
{
    private RepositorySnapshot() { }
    public RepositorySnapshot(Guid organizationId, Guid repositoryId, string commitSha) : base(Guid.NewGuid())
    {
        OrganizationId = organizationId; RepositoryId = repositoryId;
        CommitSha = string.IsNullOrWhiteSpace(commitSha) ? throw new ArgumentException("Commit SHA is required.", nameof(commitSha)) : commitSha;
    }
    public Guid OrganizationId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public string CommitSha { get; private set; } = string.Empty;
    public RegisteredRepository Repository { get; private set; } = null!;
    public ICollection<RepositoryDocument> Documents { get; private set; } = new List<RepositoryDocument>();
    public ICollection<RepositoryEndpoint> Endpoints { get; private set; } = new List<RepositoryEndpoint>();
    public ICollection<RepositoryChunk> Chunks { get; private set; } = new List<RepositoryChunk>();
}

public sealed class RepositoryChunk : Entity
{
    private RepositoryChunk() { }
    public RepositoryChunk(Guid organizationId, Guid snapshotId, Guid documentId, int ordinal, int startLine, int endLine, string content, string contentHash, string embeddingModel) : base(Guid.NewGuid())
    { OrganizationId = organizationId; SnapshotId = snapshotId; DocumentId = documentId; Ordinal = ordinal; StartLine = startLine; EndLine = endLine; Content = content; ContentHash = contentHash; EmbeddingModel = embeddingModel; }
    public Guid OrganizationId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public Guid DocumentId { get; private set; }
    public int Ordinal { get; private set; }
    public int StartLine { get; private set; }
    public int EndLine { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public string EmbeddingModel { get; private set; } = string.Empty;
    public RepositorySnapshot Snapshot { get; private set; } = null!;
    public RepositoryDocument Document { get; private set; } = null!;
}

public sealed class RepositoryEndpoint : Entity
{
    private RepositoryEndpoint() { }
    public RepositoryEndpoint(Guid organizationId, Guid snapshotId, string httpMethod, string route, string handler, string path, int line, bool requiresAuthorization, string? downstreamSymbols) : base(Guid.NewGuid())
    { OrganizationId = organizationId; SnapshotId = snapshotId; HttpMethod = httpMethod; Route = route; Handler = handler; Path = path; Line = line; RequiresAuthorization = requiresAuthorization; DownstreamSymbols = downstreamSymbols; }
    public Guid OrganizationId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public string HttpMethod { get; private set; } = string.Empty;
    public string Route { get; private set; } = string.Empty;
    public string Handler { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public int Line { get; private set; }
    public bool RequiresAuthorization { get; private set; }
    public string? DownstreamSymbols { get; private set; }
    public RepositorySnapshot Snapshot { get; private set; } = null!;
}

public sealed class RepositoryDocument : Entity
{
    private RepositoryDocument() { }
    public RepositoryDocument(Guid organizationId, Guid snapshotId, string path, string language, string contentHash, int byteCount, string content) : base(Guid.NewGuid())
    { OrganizationId = organizationId; SnapshotId = snapshotId; Path = path; Language = language; ContentHash = contentHash; ByteCount = byteCount; Content = content; }
    public Guid OrganizationId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public string Path { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public int ByteCount { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public RepositorySnapshot Snapshot { get; private set; } = null!;
    public ICollection<RepositorySymbol> Symbols { get; private set; } = new List<RepositorySymbol>();
    public ICollection<RepositoryChunk> Chunks { get; private set; } = new List<RepositoryChunk>();
}

public sealed class RepositorySymbol : Entity
{
    private RepositorySymbol() { }
    public RepositorySymbol(Guid organizationId, Guid documentId, string name, string qualifiedName, SymbolKind kind, int line) : base(Guid.NewGuid())
    { OrganizationId = organizationId; DocumentId = documentId; Name = name; QualifiedName = qualifiedName; Kind = kind; Line = line; }
    public Guid OrganizationId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string QualifiedName { get; private set; } = string.Empty;
    public SymbolKind Kind { get; private set; }
    public int Line { get; private set; }
    public RepositoryDocument Document { get; private set; } = null!;
}

public enum SymbolKind { Namespace = 1, Class = 2, Interface = 3, Struct = 4, Record = 5, Enum = 6, Method = 7, Property = 8 }

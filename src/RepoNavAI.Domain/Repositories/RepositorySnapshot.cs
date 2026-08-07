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

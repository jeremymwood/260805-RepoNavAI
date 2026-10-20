using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Persistence;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class RepositoryChunkBackfillTests
{
    [Fact]
    public void LegacySnapshot_GeneratesChunksFromStoredDocuments_PreservingSnapshotAndDocuments()
    {
        var snapshot = Snapshot();
        var document = Document(snapshot, "src/Auth.cs", string.Join('\n', Enumerable.Range(1, 250).Select(x => $"line {x}")));
        var snapshotId = snapshot.Id;
        var documentId = document.Id;

        Backfill(snapshot);

        snapshot.Id.Should().Be(snapshotId);
        snapshot.Documents.Should().ContainSingle().Which.Id.Should().Be(documentId);
        snapshot.Chunks.Should().HaveCount(3);
        snapshot.Chunks.Should().OnlyContain(x => x.OrganizationId == snapshot.OrganizationId && x.SnapshotId == snapshotId && x.DocumentId == documentId);
        snapshot.Chunks.Select(x => x.Ordinal).Should().Equal(0, 1, 2);
        snapshot.Chunks.Select(x => x.StartLine).Should().Equal(1, 101, 201);
        snapshot.Chunks.Select(x => x.EndLine).Should().Equal(120, 220, 250);
        document.Chunks.Should().BeEquivalentTo(snapshot.Chunks);
        foreach (var chunk in snapshot.Chunks)
        {
            chunk.Content.Should().StartWith("File: src/Auth.cs\n");
            chunk.ContentHash.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chunk.Content))));
            chunk.EmbeddingModel.Should().Be("test-embedding");
        }
    }

    [Fact]
    public void RepeatedBackfill_DoesNotDuplicateOrReplaceChunks()
    {
        var snapshot = Snapshot();
        Document(snapshot, "README.md", "Repository overview");
        Backfill(snapshot);
        var original = snapshot.Chunks.ToArray();

        Backfill(snapshot);

        snapshot.Chunks.Should().Equal(original);
        snapshot.Documents.Single().Chunks.Should().Equal(original);
    }

    [Fact]
    public void MixedSnapshot_FillsOnlyDocumentsWithoutChunks_AndPreservesExistingBoundaries()
    {
        var snapshot = Snapshot();
        var indexed = Document(snapshot, "old.cs", "Stored source with historical boundaries");
        var original = new RepositoryChunk(snapshot.OrganizationId, snapshot.Id, indexed.Id, 0, 1, 1, "Historical chunk", "historical-hash", "historical-model");
        indexed.Chunks.Add(original); snapshot.Chunks.Add(original);
        var missing = Document(snapshot, "new.cs", "A document that predates semantic indexing");

        Backfill(snapshot);

        snapshot.Chunks.Should().HaveCount(2);
        indexed.Chunks.Should().ContainSingle().Which.Should().BeSameAs(original);
        original.Content.Should().Be("Historical chunk");
        original.EmbeddingModel.Should().Be("historical-model");
        missing.Chunks.Should().ContainSingle().Which.DocumentId.Should().Be(missing.Id);
    }

    [Fact]
    public void EmptyDocuments_DoNotCreateSyntheticSearchEvidence()
    {
        var snapshot = Snapshot();
        Document(snapshot, "empty.md", " \n\t");
        Backfill(snapshot);
        Backfill(snapshot);
        snapshot.Chunks.Should().BeEmpty();
    }

    [Fact]
    public void CancellationBeforeBackfill_DoesNotModifySnapshot()
    {
        var snapshot = Snapshot();
        Document(snapshot, "source.cs", "source");
        var action = () => RepositoryChunkBackfill.AddMissingChunks(snapshot, new SourceChunker(), "test-embedding", new CancellationToken(true));
        action.Should().Throw<OperationCanceledException>();
        snapshot.Chunks.Should().BeEmpty();
    }

    [Fact]
    public void TrackedLegacySnapshot_NewChunksAreInserts_NotUpdates()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options);
        var snapshot = Snapshot();
        Document(snapshot, "src/Auth.cs", "Authentication source");
        db.Attach(snapshot);

        Backfill(snapshot);
        db.ChangeTracker.DetectChanges();

        db.Entry(snapshot).State.Should().Be(EntityState.Unchanged);
        db.Entry(snapshot.Documents.Single()).State.Should().Be(EntityState.Unchanged);
        db.Entry(snapshot.Chunks.Single()).State.Should().Be(EntityState.Added);
        snapshot.Chunks.Single().Document.Should().BeSameAs(snapshot.Documents.Single());
    }

    private static RepositorySnapshot Snapshot() => new(Guid.NewGuid(), Guid.NewGuid(), new string('a', 40));
    private static RepositoryDocument Document(RepositorySnapshot snapshot, string path, string content)
    {
        var document = new RepositoryDocument(snapshot.OrganizationId, snapshot.Id, path, "C#", "stored-hash", Encoding.UTF8.GetByteCount(content), content);
        snapshot.Documents.Add(document);
        return document;
    }
    private static void Backfill(RepositorySnapshot snapshot) =>
        RepositoryChunkBackfill.AddMissingChunks(snapshot, new SourceChunker(), "test-embedding", CancellationToken.None);
}

using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Diagnostics;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Identity;
using RepoNavAI.Infrastructure.Persistence;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

// CI supplies a disposable pgvector service. Local developers can opt in with the documented test container.
public sealed class BackfillPostgresFactAttribute : FactAttribute
{
    public BackfillPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BackfillPostgresFixture.ConnectionVariable)))
            Skip = "Requires the isolated REPONAV_TEST_POSTGRES database; see docs/testing/indexing-backfill.md.";
    }
}

public sealed class BackfillPostgresFixture : IAsyncLifetime
{
    public const string ConnectionVariable = "REPONAV_TEST_POSTGRES";
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        var connection = new NpgsqlConnectionStringBuilder(_connectionString);
        if (connection.Database != "reponav_backfill_tests" || connection.Host is not ("localhost" or "127.0.0.1"))
            throw new InvalidOperationException("Backfill tests require a loopback reponav_backfill_tests database. Never use application credentials.");
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_connectionString ?? throw new InvalidOperationException("The isolated test database is not configured."), postgres => postgres.UseVector()).Options);
    public Task DisposeAsync() => Task.CompletedTask;
}

public sealed class RepositoryChunkBackfillPersistenceTests(BackfillPostgresFixture fixture) : IClassFixture<BackfillPostgresFixture>
{
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [BackfillPostgresFact]
    public async Task ReindexUnchangedLegacyCommit_PersistsEmbeddingsAndRestoresSearch_WithoutDuplicates()
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(); // All fixture rows roll back.
        var job = await SeedAsync(db, documents: 2);
        var snapshotId = await db.RepositorySnapshots.Where(x => x.RepositoryId == job.RepositoryId).Select(x => x.Id).SingleAsync();
        var documentIds = await db.RepositoryDocuments.Where(x => x.SnapshotId == snapshotId).Select(x => x.Id).ToArrayAsync();

        await PersistAsync(db, job, new TestEmbeddings());
        var chunks = await db.RepositoryChunks.Where(x => x.SnapshotId == snapshotId).OrderBy(x => x.Id).ToArrayAsync();
        chunks.Should().HaveCount(2);
        await AssertSearchableAsync(db, job, expected: 2);
        job.Checkpoint.Should().Be(IndexingCheckpoint.Persisting);
        (await db.RepositoryDocuments.Where(x => x.SnapshotId == snapshotId).Select(x => x.Id).ToArrayAsync()).Should().BeEquivalentTo(documentIds);
        chunks.Should().OnlyContain(x => x.Content.Contains("Original stored authentication source", StringComparison.Ordinal));

        db.ChangeTracker.Clear();
        job = await db.RepositoryIndexingRequests.SingleAsync(x => x.Id == job.Id);
        await PersistAsync(db, job, new TestEmbeddings());
        (await db.RepositoryChunks.Where(x => x.SnapshotId == snapshotId).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync())
            .Should().Equal(chunks.Select(x => x.Id));
        (await db.RepositorySnapshots.CountAsync(x => x.RepositoryId == job.RepositoryId)).Should().Be(1);
        await AssertSearchableAsync(db, job, expected: 2);
    }

    [BackfillPostgresFact]
    public async Task FailedEmbeddingBatch_RetryReusesPersistedChunks_AndFinishesMissingVectors()
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var job = await SeedAsync(db, documents: 65); // Two provider batches.
        var attempt = () => PersistAsync(db, job, new TestEmbeddings(failOnCall: 2));
        await attempt.Should().ThrowAsync<ExternalServiceException>();
        var ids = await db.RepositoryChunks.Where(x => x.Snapshot.RepositoryId == job.RepositoryId).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        ids.Should().HaveCount(65);
        (await db.RepositoryChunks.CountAsync(x => x.Snapshot.RepositoryId == job.RepositoryId && EF.Property<Vector?>(x, "Embedding") != null)).Should().Be(64);

        db.ChangeTracker.Clear();
        job = await db.RepositoryIndexingRequests.SingleAsync(x => x.Id == job.Id);
        await PersistAsync(db, job, new TestEmbeddings());

        (await db.RepositoryChunks.Where(x => x.Snapshot.RepositoryId == job.RepositoryId).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync()).Should().Equal(ids);
        (await db.RepositoryChunks.CountAsync(x => x.Snapshot.RepositoryId == job.RepositoryId && EF.Property<Vector?>(x, "Embedding") != null)).Should().Be(65);
        await AssertSearchableAsync(db, job, expected: 10);
    }

    [BackfillPostgresFact]
    public async Task FreshSnapshot_UsesTheSameChunkBuilder_AndRemainsSearchable()
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var job = await SeedAsync(db, documents: 0, legacySnapshot: false);

        await PersistAsync(db, job, new TestEmbeddings());

        (await db.RepositorySnapshots.CountAsync(x => x.RepositoryId == job.RepositoryId)).Should().Be(1);
        await AssertSearchableAsync(db, job, expected: 1);
    }

    private static Task PersistAsync(AppDbContext db, RepositoryIndexingRequest job, IEmbeddingGenerator embeddings) =>
        new IndexingQueueStore(db, TimeProvider.System, Options.Create(new IndexingOptions())).PersistAsync(job,
            new RepositorySnapshotData(Commit, [new RepositorySourceFile("download.cs", "C#", Encoding.UTF8.GetBytes("public class DownloadedSource { }"))]),
            new CSharpSourceSymbolParser(), new AspNetEndpointAnalyzer(), new SourceChunker(), embeddings, new PgVectorStore(db), CancellationToken.None);

    private static async Task AssertSearchableAsync(AppDbContext db, RepositoryIndexingRequest job, int expected)
    {
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Search);
        var store = new PgVectorStore(db);
        var matches = await store.SearchAsync(job.OrganizationId, job.RepositoryId, TestEmbeddings.Vector(), 10, CancellationToken.None);
        matches.Should().HaveCount(expected).And.OnlyContain(x => x.CommitSha == Commit);
        var manifest = run.Retrievals.Single();
        manifest.SnapshotId.Should().Be(await db.RepositorySnapshots.Where(x => x.RepositoryId == job.RepositoryId).Select(x => x.Id).SingleAsync());
        manifest.CommitSha.Should().Be(Commit);
        manifest.QueryDimensions.Should().Be(512);
        manifest.Candidates.Select(x => x.ChunkId).Should().Equal(matches.Select(x => x.ChunkId));
        manifest.Candidates.Select(x => x.Score).Should().Equal(matches.Select(x => (double?)x.Score));
        manifest.IndexedEmbeddingModels.Should().ContainSingle();
        (await store.SearchAsync(Guid.NewGuid(), job.RepositoryId, TestEmbeddings.Vector(), 10, CancellationToken.None)).Should().BeEmpty();
        run.Retrievals.Last().SnapshotId.Should().BeNull();
        run.Retrievals.Last().Candidates.Should().BeEmpty();
        run.Finish(RepositoryOutcome.Completed);
    }

    private static async Task<RepositoryIndexingRequest> SeedAsync(AppDbContext db, int documents, bool legacySnapshot = true)
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Backfill fixture" };
        var organization = new Organization("Backfill fixture", $"backfill-{Guid.NewGuid():N}");
        var repository = new RegisteredRepository(organization.Id, Guid.NewGuid().ToString(), "fixture", "backfill", "main", RepositoryVisibility.Public, "https://github.com/fixture/backfill", user.Id);
        var job = new RepositoryIndexingRequest(organization.Id, repository.Id, user.Id);
        job.Start(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), Guid.NewGuid());
        db.AddRange(user, organization, repository, job);
        if (legacySnapshot)
        {
            var snapshot = new RepositorySnapshot(organization.Id, repository.Id, Commit);
            for (var i = 0; i < documents; i++)
                snapshot.Documents.Add(new RepositoryDocument(organization.Id, snapshot.Id, $"src/Auth{i}.cs", "C#", "fixture-hash", 38, "Original stored authentication source"));
            db.Add(snapshot);
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await db.RepositoryIndexingRequests.SingleAsync(x => x.Id == job.Id);
    }

    private sealed class TestEmbeddings(int? failOnCall = null) : IEmbeddingGenerator
    {
        private int _calls;
        public string Model => "test-embedding";
        public int Dimensions => 512;
        public bool IsConfigured => true;
        public Task<IReadOnlyList<float[]>> GenerateAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++_calls == failOnCall) throw new ExternalServiceException("Simulated embedding failure.");
            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => Vector()).ToArray());
        }
        public static float[] Vector() { var vector = new float[512]; vector[0] = 1; return vector; }
    }
}

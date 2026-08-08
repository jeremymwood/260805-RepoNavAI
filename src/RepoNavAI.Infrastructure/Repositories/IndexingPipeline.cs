using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Persistence;

namespace RepoNavAI.Infrastructure.Repositories;

public sealed class IndexingOptions
{
    public const string SectionName = "Indexing";
    public int PollSeconds { get; init; } = 2;
    public int LeaseMinutes { get; init; } = 5;
    public int MaxAttempts { get; init; } = 3;
    public int MaximumFiles { get; init; } = 5000;
    public int MaximumFileBytes { get; init; } = 1_048_576;
    public int MaximumSnapshotBytes { get; init; } = 52_428_800;
}

public sealed class GitHubSnapshotProvider(HttpClient httpClient, IOptions<GitHubOptions> github, IOptions<IndexingOptions> indexing) : IRepositorySnapshotProvider
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".cs", ".csproj", ".sln", ".json", ".ts", ".tsx", ".js", ".jsx", ".md", ".yml", ".yaml" };

    public async Task<RepositorySnapshotData> FetchAsync(string owner, string name, string branch, CancellationToken cancellationToken)
    {
        using var commitRequest = CreateRequest(HttpMethod.Get, $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/commits/{Uri.EscapeDataString(branch)}");
        using var commitResponse = await httpClient.SendAsync(commitRequest, cancellationToken);
        if (!commitResponse.IsSuccessStatusCode) throw new ExternalServiceException("The repository snapshot could not be resolved from GitHub.");
        var commit = await commitResponse.Content.ReadFromJsonAsync<CommitResponse>(cancellationToken) ?? throw new ExternalServiceException("GitHub returned an invalid commit response.");
        using var archiveRequest = CreateRequest(HttpMethod.Get, $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/tarball/{commit.Sha}");
        using var archiveResponse = await httpClient.SendAsync(archiveRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!archiveResponse.IsSuccessStatusCode) throw new ExternalServiceException("The repository snapshot could not be downloaded from GitHub.");
        await using var stream = await archiveResponse.Content.ReadAsStreamAsync(cancellationToken);
        await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        var files = new List<RepositorySourceFile>(); var total = 0;
        while (await tar.GetNextEntryAsync(copyData: false, cancellationToken) is { } entry)
        {
            if (entry.EntryType is not TarEntryType.RegularFile || entry.DataStream is null) continue;
            var path = NormalizePath(entry.Name); var extension = Path.GetExtension(path);
            if (path.Length == 0 || !Extensions.Contains(extension) || IsIgnored(path)) continue;
            if (entry.Length > indexing.Value.MaximumFileBytes) continue;
            total += checked((int)entry.Length);
            if (total > indexing.Value.MaximumSnapshotBytes || files.Count >= indexing.Value.MaximumFiles) throw new InvalidOperationException("Repository exceeds the configured indexing limits.");
            using var memory = new MemoryStream((int)entry.Length); await entry.DataStream.CopyToAsync(memory, cancellationToken);
            files.Add(new RepositorySourceFile(path, Language(extension), memory.ToArray()));
        }
        return new RepositorySnapshotData(commit.Sha, files);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json")); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(github.Value.AccessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", github.Value.AccessToken);
        return request;
    }
    private static string NormalizePath(string name) { var value = name.Replace('\\', '/'); var slash = value.IndexOf('/'); return slash < 0 ? string.Empty : value[(slash + 1)..]; }
    private static bool IsIgnored(string path) => path.Split('/').Any(segment => segment is ".git" or "node_modules" or "bin" or "obj" or "dist" or "coverage");
    private static string Language(string extension) => extension.ToLowerInvariant() switch { ".cs" => "csharp", ".ts" or ".tsx" => "typescript", ".js" or ".jsx" => "javascript", ".json" => "json", ".md" => "markdown", ".yml" or ".yaml" => "yaml", _ => "text" };
    private sealed record CommitResponse(string Sha);
}

public sealed class CSharpSourceSymbolParser : ISourceSymbolParser
{
    public IReadOnlyCollection<ParsedSymbol> Parse(string path, byte[] content)
    {
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return [];
        var root = CSharpSyntaxTree.ParseText(Encoding.UTF8.GetString(content)).GetRoot();
        return root.DescendantNodes().Select(node => node switch
        {
            BaseNamespaceDeclarationSyntax value => Create(value.Name.ToString(), value.Name.ToString(), SymbolKind.Namespace, value),
            ClassDeclarationSyntax value => Create(value.Identifier.Text, Qualified(value), SymbolKind.Class, value),
            InterfaceDeclarationSyntax value => Create(value.Identifier.Text, Qualified(value), SymbolKind.Interface, value),
            StructDeclarationSyntax value => Create(value.Identifier.Text, Qualified(value), SymbolKind.Struct, value),
            RecordDeclarationSyntax value => Create(value.Identifier.Text, Qualified(value), SymbolKind.Record, value),
            EnumDeclarationSyntax value => Create(value.Identifier.Text, Qualified(value), SymbolKind.Enum, value),
            MethodDeclarationSyntax value => Create(value.Identifier.Text, Qualified(value), SymbolKind.Method, value),
            PropertyDeclarationSyntax value => Create(value.Identifier.Text, Qualified(value), SymbolKind.Property, value),
            _ => null
        }).Where(x => x is not null).Cast<ParsedSymbol>().ToArray();
    }
    private static ParsedSymbol Create(string name, string qualified, SymbolKind kind, Microsoft.CodeAnalysis.SyntaxNode node) => new(name, qualified, kind, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    private static string Qualified(MemberDeclarationSyntax node)
    {
        var names = node.Ancestors().OfType<MemberDeclarationSyntax>().Select(Name).Where(x => x is not null).Reverse().Append(Name(node)!).ToArray();
        var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString(); return string.Join('.', string.IsNullOrEmpty(ns) ? names : [ns, .. names]);
    }
    private static string? Name(MemberDeclarationSyntax node) => node switch { TypeDeclarationSyntax x => x.Identifier.Text, MethodDeclarationSyntax x => x.Identifier.Text, PropertyDeclarationSyntax x => x.Identifier.Text, EnumDeclarationSyntax x => x.Identifier.Text, _ => null };
}

public sealed class IndexingQueueStore(AppDbContext db, TimeProvider timeProvider, IOptions<IndexingOptions> options) : IIndexingRequestRepository
{
    public async Task AddAsync(RepositoryIndexingRequest request, CancellationToken cancellationToken) => await db.RepositoryIndexingRequests.AddAsync(request, cancellationToken);
    public Task<RepositoryIndexingRequest?> GetLatestAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) =>
        db.RepositoryIndexingRequests
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId, cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
    public Task RefreshAsync(RepositoryIndexingRequest job, CancellationToken cancellationToken) => db.Entry(job).ReloadAsync(cancellationToken);
    public async Task<RepositoryIndexingRequest?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken); var now = timeProvider.GetUtcNow();
        var job = await db.RepositoryIndexingRequests.FromSqlInterpolated($"SELECT * FROM reponav.\"RepositoryIndexingRequests\" WHERE (\"Status\" = 'Pending' OR (\"Status\" = 'Processing' AND \"LeaseExpiresAtUtc\" < {now})) ORDER BY \"CreatedAtUtc\" FOR UPDATE SKIP LOCKED LIMIT 1").SingleOrDefaultAsync(cancellationToken);
        if (job is null) { await transaction.CommitAsync(cancellationToken); return null; }
        await db.Entry(job).Reference(x => x.Repository).LoadAsync(cancellationToken); job.Start(now, TimeSpan.FromMinutes(options.Value.LeaseMinutes)); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return job;
    }
    public async Task PersistAsync(RepositoryIndexingRequest job, RepositorySnapshotData data, ISourceSymbolParser parser, IRepositoryEndpointAnalyzer endpointAnalyzer, ISourceChunker chunker, IEmbeddingGenerator embeddingGenerator, IVectorStore vectorStore, CancellationToken cancellationToken)
    {
        var existing = await db.RepositorySnapshots.Include(x => x.Chunks).SingleOrDefaultAsync(x => x.RepositoryId == job.RepositoryId && x.CommitSha == data.CommitSha, cancellationToken);
        if (existing is not null) { await EmbedAsync(job, existing, embeddingGenerator, vectorStore, cancellationToken); return; }
        var snapshot = new RepositorySnapshot(job.OrganizationId, job.RepositoryId, data.CommitSha);
        foreach (var file in data.Files)
        {
            var text = Encoding.UTF8.GetString(file.Content); var hash = Convert.ToHexString(SHA256.HashData(file.Content));
            var document = new RepositoryDocument(job.OrganizationId, snapshot.Id, file.Path, file.Language, hash, file.Content.Length, text); snapshot.Documents.Add(document);
            foreach (var symbol in parser.Parse(file.Path, file.Content)) document.Symbols.Add(new RepositorySymbol(job.OrganizationId, document.Id, symbol.Name, symbol.QualifiedName, symbol.Kind, symbol.Line));
            foreach (var chunk in chunker.Chunk(file.Path, text))
            {
                var chunkHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chunk.Content)));
                var entity = new RepositoryChunk(job.OrganizationId, snapshot.Id, document.Id, chunk.Ordinal, chunk.StartLine, chunk.EndLine, chunk.Content, chunkHash, embeddingGenerator.Model);
                document.Chunks.Add(entity); snapshot.Chunks.Add(entity);
            }
        }
        foreach (var endpoint in endpointAnalyzer.Analyze(data.Files))
            snapshot.Endpoints.Add(new RepositoryEndpoint(job.OrganizationId, snapshot.Id, endpoint.HttpMethod, endpoint.Route, endpoint.Handler, endpoint.Path, endpoint.Line, endpoint.RequiresAuthorization, System.Text.Json.JsonSerializer.Serialize(endpoint.DownstreamSymbols)));
        job.Advance(IndexingCheckpoint.Persisting, timeProvider.GetUtcNow(), TimeSpan.FromMinutes(options.Value.LeaseMinutes));
        await db.RepositorySnapshots.AddAsync(snapshot, cancellationToken); await db.SaveChangesAsync(cancellationToken);
        await EmbedAsync(job, snapshot, embeddingGenerator, vectorStore, cancellationToken);
    }

    private static async Task EmbedAsync(RepositoryIndexingRequest job, RepositorySnapshot snapshot, IEmbeddingGenerator embeddingGenerator, IVectorStore vectorStore, CancellationToken cancellationToken)
    {
        if (embeddingGenerator.IsConfigured)
        {
            foreach (var batch in snapshot.Chunks.Chunk(64))
            {
                var vectors = await embeddingGenerator.GenerateAsync(batch.Select(x => x.Content).ToArray(), cancellationToken);
                await vectorStore.UpsertAsync(job.OrganizationId, job.RepositoryId, snapshot.Id, batch.Zip(vectors, (chunk, vector) => (chunk.Id, vector)).ToArray(), cancellationToken);
            }
        }
    }
}

public sealed class RepositoryIndexingWorker(IServiceScopeFactory scopes, TimeProvider timeProvider, IOptions<IndexingOptions> options, ILogger<RepositoryIndexingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { if (!await ProcessOneAsync(stoppingToken)) await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Indexing worker polling failed"); await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), stoppingToken); }
        }
    }
    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<IndexingQueueStore>(); var job = await store.ClaimAsync(cancellationToken); if (job is null) return false;
        try
        {
            if (job.IsCancellationRequested) { job.Cancel(timeProvider.GetUtcNow()); await store.SaveChangesAsync(cancellationToken); return true; }
            var provider = scope.ServiceProvider.GetRequiredService<IRepositorySnapshotProvider>(); var parser = scope.ServiceProvider.GetRequiredService<ISourceSymbolParser>(); var endpointAnalyzer = scope.ServiceProvider.GetRequiredService<IRepositoryEndpointAnalyzer>(); var chunker = scope.ServiceProvider.GetRequiredService<ISourceChunker>(); var embeddingGenerator = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator>(); var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
            var data = await provider.FetchAsync(job.Repository.Owner, job.Repository.Name, job.Repository.DefaultBranch, cancellationToken);
            await store.RefreshAsync(job, cancellationToken);
            if (job.IsCancellationRequested) { job.Cancel(timeProvider.GetUtcNow()); await store.SaveChangesAsync(cancellationToken); return true; }
            job.Advance(IndexingCheckpoint.Parsing, timeProvider.GetUtcNow(), TimeSpan.FromMinutes(options.Value.LeaseMinutes), data.CommitSha); await store.SaveChangesAsync(cancellationToken);
            if (job.IsCancellationRequested) { job.Cancel(timeProvider.GetUtcNow()); await store.SaveChangesAsync(cancellationToken); return true; }
            await store.PersistAsync(job, data, parser, endpointAnalyzer, chunker, embeddingGenerator, vectorStore, cancellationToken); job.Complete(data.CommitSha, timeProvider.GetUtcNow()); await store.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Indexing job {JobId} failed with {ErrorType}", job.Id, exception.GetType().Name); job.Fail("INDEXING_FAILED", "Repository indexing failed. Retry the job or check provider access and repository limits.", timeProvider.GetUtcNow(), options.Value.MaxAttempts); await store.SaveChangesAsync(cancellationToken);
        }
        return true;
    }
}

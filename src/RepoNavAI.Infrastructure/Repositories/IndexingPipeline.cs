using System.Formats.Tar;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics.Metrics;
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
    public int LeaseSeconds { get; init; } = 45;
    public int HeartbeatSeconds { get; init; } = 10;
    public int MaxAttempts { get; init; } = 3;
    public int MaximumFiles { get; init; } = 5000;
    public int MaximumArchiveEntries { get; init; } = 100_000;
    public int MaximumFileBytes { get; init; } = 1_048_576;
    public int MaximumSnapshotBytes { get; init; } = 52_428_800;
    public long MaximumDownloadBytes { get; init; } = 262_144_000;
    public long MaximumExpandedBytes { get; init; } = 1_073_741_824;
    public int AcquisitionTimeoutSeconds { get; init; } = 120;
}

public sealed class RepositoryAcquisitionException(string code, string safeMessage, bool retryable, Exception? innerException = null)
    : Exception(safeMessage, innerException)
{
    public string Code { get; } = code;
    public string SafeMessage { get; } = safeMessage;
    public bool Retryable { get; } = retryable;
}

public sealed class GitHubSnapshotProvider(HttpClient httpClient, IOptions<GitHubOptions> github, IOptions<IndexingOptions> indexing, ILogger<GitHubSnapshotProvider> logger) : IRepositorySnapshotProvider
{
    private static readonly SourceLanguageRegistry LanguageRegistry = new();
    private static readonly HashSet<string> ArchiveMediaTypes = new(StringComparer.OrdinalIgnoreCase) { "application/x-gzip", "application/gzip", "application/octet-stream" };
    private static readonly Meter Meter = new("RepoNavAI.Indexing.Acquisition");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("reponav.indexing.acquisition.duration", "s");
    private static readonly Histogram<long> DownloadBytes = Meter.CreateHistogram<long>("reponav.indexing.acquisition.download_bytes", "By");
    private static readonly Histogram<long> ExpandedBytes = Meter.CreateHistogram<long>("reponav.indexing.acquisition.expanded_bytes", "By");
    private static readonly Histogram<long> ArchiveEntries = Meter.CreateHistogram<long>("reponav.indexing.acquisition.entries", "{entry}");
    private static readonly Histogram<long> SkippedFiles = Meter.CreateHistogram<long>("reponav.indexing.acquisition.skipped_files", "{file}");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("reponav.indexing.acquisition.failures");

    public async Task<RepositorySnapshotData> FetchAsync(string owner, string name, string branch, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(indexing.Value.AcquisitionTimeoutSeconds));
        var token = timeout.Token;
        long downloaded = 0, expanded = 0, entries = 0, skipped = 0;
        try
        {
            using var commitRequest = CreateRequest(HttpMethod.Get, $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/commits/{Uri.EscapeDataString(branch)}");
            using var commitResponse = await SendAsync(commitRequest, HttpCompletionOption.ResponseContentRead, token);
            EnsureSuccess(commitResponse, "resolved");
            var commit = await commitResponse.Content.ReadFromJsonAsync<CommitResponse>(token)
                ?? throw Failure("ARCHIVE_COMMIT_INVALID", "GitHub returned an invalid commit response.");
            if (string.IsNullOrWhiteSpace(commit.Sha)) throw Failure("ARCHIVE_COMMIT_INVALID", "GitHub returned an invalid commit response.");

            using var archiveRequest = CreateRequest(HttpMethod.Get, $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/tarball/{commit.Sha}");
            using var archiveResponse = await SendAsync(archiveRequest, HttpCompletionOption.ResponseHeadersRead, token);
            EnsureSuccess(archiveResponse, "downloaded");
            ValidateArchiveHeaders(archiveResponse);
            await using var responseStream = await archiveResponse.Content.ReadAsStreamAsync(token);
            await using var compressed = new BoundedReadStream(responseStream, indexing.Value.MaximumDownloadBytes,
                () => Failure("ARCHIVE_DOWNLOAD_LIMIT", "Repository archive exceeds the configured download limit."));
            var signature = new byte[2];
            await ReadExactlyAsync(compressed, signature, token);
            if (signature[0] != 0x1f || signature[1] != 0x8b) throw Failure("ARCHIVE_FORMAT_UNSUPPORTED", "GitHub returned an unsupported repository archive format.");
            await using var prefixed = new PrefixReadStream(signature, compressed);
            await using var gzip = new GZipStream(prefixed, CompressionMode.Decompress);
            await using var decompressed = new BoundedReadStream(gzip, indexing.Value.MaximumExpandedBytes,
                () => Failure("ARCHIVE_EXPANDED_LIMIT", "Repository archive exceeds the configured expanded-size limit."));
            using var tar = new TarReader(decompressed);
            var files = new List<RepositorySourceFile>(); var snapshotBytes = 0;
            var coverage = new Dictionary<string, MutableCoverage>(StringComparer.OrdinalIgnoreCase);
            while (await tar.GetNextEntryAsync(copyData: false, token) is { } entry)
            {
                entries++;
                if (entries > indexing.Value.MaximumArchiveEntries) throw Failure("ARCHIVE_ENTRY_LIMIT", "Repository archive contains more entries than the configured limit.");
                ValidateEntry(entry);
                if (entry.EntryType is not TarEntryType.RegularFile || entry.DataStream is null) continue;
                var path = NormalizePath(entry.Name); var classification = LanguageRegistry.ClassifyPath(path);
                if (!classification.IsSupported)
                {
                    skipped++; AddSkipped(coverage, classification.Language?.Name ?? "other", classification.SkipReason!); continue;
                }
                if (entry.Length < 0 || entry.Length > indexing.Value.MaximumFileBytes) throw Failure("ARCHIVE_FILE_LIMIT", $"A supported source file exceeds the configured {indexing.Value.MaximumFileBytes}-byte limit.");
                snapshotBytes = checked(snapshotBytes + (int)entry.Length);
                if (snapshotBytes > indexing.Value.MaximumSnapshotBytes) throw Failure("ARCHIVE_SNAPSHOT_LIMIT", "Supported source files exceed the configured snapshot-size limit.");
                if (files.Count >= indexing.Value.MaximumFiles) throw Failure("ARCHIVE_FILE_COUNT_LIMIT", "Repository contains more supported source files than the configured limit.");
                using var memory = new MemoryStream((int)entry.Length);
                await entry.DataStream.CopyToAsync(memory, token);
                var content = memory.ToArray();
                if (!SourceLanguageRegistry.IsText(content)) { skipped++; AddSkipped(coverage, classification.Language!.Name, SourceLanguageRegistry.Binary); continue; }
                files.Add(new RepositorySourceFile(path, classification.Language!.Name, content));
                GetCoverage(coverage, classification.Language.Name).Indexed++;
            }
            downloaded = compressed.BytesRead; expanded = decompressed.BytesRead;
            logger.LogInformation("Acquired repository archive with {DownloadBytes} compressed bytes, {ExpandedBytes} expanded bytes, {EntryCount} entries, {IndexedFileCount} indexed files, and {SkippedFileCount} skipped files", downloaded, expanded, entries, files.Count, skipped);
            return new RepositorySnapshotData(commit.Sha, files, coverage.OrderBy(item => item.Key).Select(item => item.Value.ToContract(item.Key)).ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            var failure = Failure("ARCHIVE_TIMEOUT", "Repository archive acquisition exceeded the configured time limit."); RecordFailure(failure); throw failure;
        }
        catch (InvalidDataException exception) { var failure = Failure("ARCHIVE_MALFORMED", "GitHub returned a malformed or truncated repository archive.", exception); RecordFailure(failure); throw failure; }
        catch (EndOfStreamException exception) { var failure = Failure("ARCHIVE_TRUNCATED", "GitHub returned a truncated repository archive.", exception); RecordFailure(failure); throw failure; }
        catch (HttpRequestException exception) { var failure = new RepositoryAcquisitionException("ARCHIVE_PROVIDER_TRANSIENT", "GitHub could not complete the repository archive request. Retry later.", true, exception); RecordFailure(failure); throw failure; }
        catch (RepositoryAcquisitionException exception) { RecordFailure(exception); throw; }
        finally
        {
            Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds);
            if (downloaded > 0) DownloadBytes.Record(downloaded);
            if (expanded > 0) ExpandedBytes.Record(expanded);
            ArchiveEntries.Record(entries); SkippedFiles.Record(skipped);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken token) => await httpClient.SendAsync(request, completion, token);
    private static RepositoryAcquisitionException Failure(string code, string message, Exception? inner = null) => new(code, message, false, inner);
    private void RecordFailure(RepositoryAcquisitionException exception)
    {
        Failures.Add(1, new KeyValuePair<string, object?>("category", exception.Code));
        logger.LogWarning("Repository archive acquisition failed with category {FailureCategory}", exception.Code);
    }
    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;
        var transient = response.StatusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
        throw new RepositoryAcquisitionException(transient ? "ARCHIVE_PROVIDER_TRANSIENT" : "ARCHIVE_PROVIDER_REJECTED",
            transient ? $"GitHub could not complete the repository archive request. Retry later." : $"The repository snapshot could not be {operation} from GitHub. Check repository access.", transient);
    }
    private void ValidateArchiveHeaders(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength is { } length && length > indexing.Value.MaximumDownloadBytes) throw Failure("ARCHIVE_DOWNLOAD_LIMIT", "Repository archive exceeds the configured download limit.");
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null && !ArchiveMediaTypes.Contains(mediaType)) throw Failure("ARCHIVE_CONTENT_TYPE", "GitHub returned an unexpected repository archive content type.");
        if (response.Content.Headers.ContentEncoding.Count > 0 && !response.Content.Headers.ContentEncoding.All(x => x.Equals("identity", StringComparison.OrdinalIgnoreCase)))
            throw Failure("ARCHIVE_CONTENT_ENCODING", "GitHub returned an unsupported repository archive content encoding.");
    }
    private static void ValidateEntry(TarEntry entry)
    {
        var normalized = entry.Name.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Split('/').Any(segment => segment is "..")) throw Failure("ARCHIVE_PATH_UNSAFE", "Repository archive contains an unsafe path.");
        if (entry.EntryType is TarEntryType.SymbolicLink or TarEntryType.HardLink) throw Failure("ARCHIVE_LINK_UNSUPPORTED", "Repository archive contains an unsupported link entry.");
        if (entry.EntryType is TarEntryType.BlockDevice or TarEntryType.CharacterDevice or TarEntryType.Fifo) throw Failure("ARCHIVE_ENTRY_UNSUPPORTED", "Repository archive contains an unsupported special entry.");
    }
    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), token);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json")); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(github.Value.AccessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", github.Value.AccessToken);
        return request;
    }
    private static string NormalizePath(string name) { var value = name.Replace('\\', '/'); var slash = value.IndexOf('/'); return slash < 0 || slash == value.Length - 1 ? string.Empty : value[(slash + 1)..]; }
    private static MutableCoverage GetCoverage(Dictionary<string, MutableCoverage> coverage, string language)
    {
        if (!coverage.TryGetValue(language, out var value)) coverage[language] = value = new();
        return value;
    }
    private static void AddSkipped(Dictionary<string, MutableCoverage> coverage, string language, string reason)
    {
        var value = GetCoverage(coverage, language);
        if (reason == SourceLanguageRegistry.Unsupported) value.SkippedUnsupported++;
        else if (reason == SourceLanguageRegistry.Binary) value.SkippedBinary++;
        else value.SkippedExcluded++;
    }
    private sealed class MutableCoverage
    {
        public int Indexed; public int SkippedUnsupported; public int SkippedExcluded; public int SkippedBinary;
        public RepositoryLanguageCoverage ToContract(string language) => new(language, Indexed, SkippedUnsupported, SkippedExcluded, SkippedBinary);
    }
    private sealed record CommitResponse(string Sha);

    private sealed class BoundedReadStream(Stream inner, long limit, Func<Exception> limitException) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { var read = inner.Read(buffer, offset, count); Count(read); return read; }
        public override int Read(Span<byte> buffer) { var read = inner.Read(buffer); Count(read); return read; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { var read = await inner.ReadAsync(buffer, cancellationToken); Count(read); return read; }
        private void Count(int read) { BytesRead += read; if (BytesRead > limit) throw limitException(); }
        public override void Flush() { } public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); GC.SuppressFinalize(this); }
    }

    private sealed class PrefixReadStream(byte[] prefix, Stream inner) : Stream
    {
        private int offset;
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int bufferOffset, int count) => Read(buffer.AsSpan(bufferOffset, count));
        public override int Read(Span<byte> buffer) { var copied = CopyPrefix(buffer); return copied == 0 ? inner.Read(buffer) : copied; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { var copied = CopyPrefix(buffer.Span); return copied == 0 ? inner.ReadAsync(buffer, cancellationToken) : ValueTask.FromResult(copied); }
        private int CopyPrefix(Span<byte> buffer) { var count = Math.Min(buffer.Length, prefix.Length - offset); if (count <= 0) return 0; prefix.AsSpan(offset, count).CopyTo(buffer); offset += count; return count; }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); GC.SuppressFinalize(this); }
    }
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
    public sealed record Claim(RepositoryIndexingRequest Job, double? RecoveryDelaySeconds);
    public async Task AddAsync(RepositoryIndexingRequest request, CancellationToken cancellationToken) => await db.RepositoryIndexingRequests.AddAsync(request, cancellationToken);
    public Task<RepositoryIndexingRequest?> GetLatestAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) =>
        db.RepositoryIndexingRequests
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId, cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
    public Task RefreshAsync(RepositoryIndexingRequest job, CancellationToken cancellationToken) => db.Entry(job).ReloadAsync(cancellationToken);
    public async Task<Claim?> ClaimAsync(Guid leaseOwnerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken); var now = timeProvider.GetUtcNow();
        var job = await db.RepositoryIndexingRequests.FromSqlInterpolated($"SELECT * FROM reponav.\"RepositoryIndexingRequests\" WHERE (\"Status\" = 'Pending' OR (\"Status\" = 'Processing' AND \"LeaseExpiresAtUtc\" < {now})) ORDER BY \"CreatedAtUtc\" FOR UPDATE SKIP LOCKED LIMIT 1").SingleOrDefaultAsync(cancellationToken);
        if (job is null) { await transaction.CommitAsync(cancellationToken); return null; }
        double? recoveryDelay = job.Status == IndexingRequestStatus.Processing && job.LeaseExpiresAtUtc is { } expiredAt ? Math.Max(0, (now - expiredAt).TotalSeconds) : null;
        await db.Entry(job).Reference(x => x.Repository).LoadAsync(cancellationToken); job.Start(now, TimeSpan.FromSeconds(options.Value.LeaseSeconds), leaseOwnerId); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return new Claim(job, recoveryDelay);
    }
    public async Task<bool> RenewLeaseAsync(Guid jobId, Guid leaseOwnerId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddSeconds(options.Value.LeaseSeconds);
        var affected = await db.RepositoryIndexingRequests
            .Where(x => x.Id == jobId && x.Status == IndexingRequestStatus.Processing && x.LeaseOwnerId == leaseOwnerId && x.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LeaseExpiresAtUtc, expires), cancellationToken);
        return affected == 1;
    }
    public Task<bool> IsCancellationRequestedAsync(Guid jobId, CancellationToken cancellationToken) =>
        db.RepositoryIndexingRequests.AnyAsync(x => x.Id == jobId && x.CancellationRequestedAtUtc != null, cancellationToken);
    public async Task PersistAsync(RepositoryIndexingRequest job, RepositorySnapshotData data, ISourceSymbolParser parser, IRepositoryEndpointAnalyzer endpointAnalyzer, ISourceChunker chunker, IEmbeddingGenerator embeddingGenerator, IVectorStore vectorStore, CancellationToken cancellationToken)
    {
        var existing = await db.RepositorySnapshots.Include(x => x.Chunks).SingleOrDefaultAsync(x => x.RepositoryId == job.RepositoryId && x.CommitSha == data.CommitSha, cancellationToken);
        if (existing is not null)
        {
            ApplyCoverage(existing, data.Coverage);
            await EmbedAsync(job, existing, embeddingGenerator, vectorStore, cancellationToken);
            return;
        }
        var snapshot = new RepositorySnapshot(job.OrganizationId, job.RepositoryId, data.CommitSha);
        ApplyCoverage(snapshot, data.Coverage);
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
        job.Advance(IndexingCheckpoint.Persisting, timeProvider.GetUtcNow(), TimeSpan.FromSeconds(options.Value.LeaseSeconds));
        await db.RepositorySnapshots.AddAsync(snapshot, cancellationToken); await db.SaveChangesAsync(cancellationToken);
        await EmbedAsync(job, snapshot, embeddingGenerator, vectorStore, cancellationToken);
    }

    private static void ApplyCoverage(RepositorySnapshot snapshot, IReadOnlyCollection<RepositoryLanguageCoverage>? suppliedCoverage)
    {
        var coverage = suppliedCoverage ?? [];
        var executable = coverage.Where(item => SourceLanguageRegistry.IsExecutableLanguage(item.Language)).ToArray();
        var indexedExecutable = executable.Sum(item => item.Indexed);
        var skippedExecutable = executable.Sum(item => item.SkippedUnsupported + item.SkippedExcluded + item.SkippedBinary);
        snapshot.SetCoverage(indexedExecutable == 0 ? "none" : skippedExecutable > 0 ? "partial" : "full", System.Text.Json.JsonSerializer.Serialize(coverage));
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
    private static readonly Meter Meter = new("RepoNavAI.Indexing");
    private static readonly Counter<long> RenewalFailures = Meter.CreateCounter<long>("reponav.indexing.lease.renewal_failures");
    private static readonly Histogram<double> RecoveryLatency = Meter.CreateHistogram<double>("reponav.indexing.lease.recovery_latency", "s");

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
        using var scope = scopes.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<IndexingQueueStore>(); var leaseOwnerId = Guid.NewGuid(); var claim = await store.ClaimAsync(leaseOwnerId, cancellationToken); if (claim is null) return false; var job = claim.Job;
        if (claim.RecoveryDelaySeconds is { } recoverySeconds)
        {
            RecoveryLatency.Record(recoverySeconds);
            logger.LogInformation("Reclaimed indexing job {JobId} on attempt {AttemptCount} after {RecoverySeconds:F1} seconds", job.Id, job.AttemptCount, recoverySeconds);
        }
        var leaseLost = false; var cancellationRequested = false;
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = RunHeartbeatAsync(job.Id, leaseOwnerId, processing, () => leaseLost = true, cancellationToken);
        var cancellationMonitor = MonitorCancellationAsync(job.Id, processing, () => cancellationRequested = true, cancellationToken);
        try
        {
            if (job.IsCancellationRequested) { job.Cancel(timeProvider.GetUtcNow()); await store.SaveChangesAsync(processing.Token); return true; }
            var provider = scope.ServiceProvider.GetRequiredService<IRepositorySnapshotProvider>(); var parser = scope.ServiceProvider.GetRequiredService<ISourceSymbolParser>(); var endpointAnalyzer = scope.ServiceProvider.GetRequiredService<IRepositoryEndpointAnalyzer>(); var chunker = scope.ServiceProvider.GetRequiredService<ISourceChunker>(); var embeddingGenerator = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator>(); var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
            var data = await provider.FetchAsync(job.Repository.Owner, job.Repository.Name, job.Repository.DefaultBranch, processing.Token);
            await store.RefreshAsync(job, processing.Token);
            if (job.LeaseOwnerId != leaseOwnerId) throw new DbUpdateConcurrencyException("The indexing lease is no longer owned by this worker.");
            if (job.IsCancellationRequested) { job.Cancel(timeProvider.GetUtcNow()); await store.SaveChangesAsync(processing.Token); return true; }
            job.Advance(IndexingCheckpoint.Parsing, timeProvider.GetUtcNow(), TimeSpan.FromSeconds(options.Value.LeaseSeconds), data.CommitSha); await store.SaveChangesAsync(processing.Token);
            if (job.IsCancellationRequested) { job.Cancel(timeProvider.GetUtcNow()); await store.SaveChangesAsync(processing.Token); return true; }
            await store.PersistAsync(job, data, parser, endpointAnalyzer, chunker, embeddingGenerator, vectorStore, processing.Token); job.Complete(data.CommitSha, timeProvider.GetUtcNow()); await store.SaveChangesAsync(processing.Token);
        }
        catch (OperationCanceledException) when (cancellationRequested)
        {
            await store.RefreshAsync(job, CancellationToken.None);
            job.Cancel(timeProvider.GetUtcNow());
            await store.SaveChangesAsync(CancellationToken.None);
            logger.LogInformation("Indexing job {JobId} was cancelled", job.Id);
        }
        catch (OperationCanceledException) when (leaseLost) { logger.LogWarning("Indexing job {JobId} stopped because worker {LeaseOwnerId} lost its lease", job.Id, leaseOwnerId); }
        catch (DbUpdateConcurrencyException) { leaseLost = true; logger.LogWarning("Indexing job {JobId} stopped because worker {LeaseOwnerId} no longer owns the lease", job.Id, leaseOwnerId); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var acquisition = exception as RepositoryAcquisitionException;
            var code = acquisition?.Code ?? "INDEXING_FAILED";
            var message = acquisition?.SafeMessage ?? "Repository indexing failed. Retry the job or check provider access and repository limits.";
            var maximumAttempts = acquisition is { Retryable: false } ? job.AttemptCount : options.Value.MaxAttempts;
            logger.LogWarning("Indexing job {JobId} failed with {ErrorType} and category {FailureCategory}", job.Id, exception.GetType().Name, code);
            job.Fail(code, message, timeProvider.GetUtcNow(), maximumAttempts); await store.SaveChangesAsync(cancellationToken);
        }
        finally { await processing.CancelAsync(); await Task.WhenAll(heartbeat, cancellationMonitor); }
        return true;
    }

    private async Task MonitorCancellationAsync(Guid jobId, CancellationTokenSource processing, Action markCancellationRequested, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(processing.Token))
            {
                using var scope = scopes.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<IndexingQueueStore>();
                if (!await store.IsCancellationRequestedAsync(jobId, stoppingToken)) continue;
                markCancellationRequested();
                await processing.CancelAsync(); return;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || processing.IsCancellationRequested) { }
    }

    private async Task RunHeartbeatAsync(Guid jobId, Guid leaseOwnerId, CancellationTokenSource processing, Action markLeaseLost, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.HeartbeatSeconds), timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(processing.Token))
            {
                using var scope = scopes.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<IndexingQueueStore>();
                if (await store.RenewLeaseAsync(jobId, leaseOwnerId, stoppingToken)) continue;
                RenewalFailures.Add(1); markLeaseLost();
                logger.LogWarning("Lease renewal rejected for indexing job {JobId} and worker {LeaseOwnerId}", jobId, leaseOwnerId);
                await processing.CancelAsync(); return;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || processing.IsCancellationRequested) { }
        catch (Exception exception)
        {
            RenewalFailures.Add(1); markLeaseLost();
            logger.LogError(exception, "Lease renewal failed for indexing job {JobId} and worker {LeaseOwnerId}", jobId, leaseOwnerId);
            await processing.CancelAsync();
        }
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Infrastructure.Persistence;

namespace RepoNavAI.Infrastructure.Repositories;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";
    public string ApiKey { get; init; } = string.Empty;
    public string EmbeddingModel { get; init; } = "text-embedding-3-small";
    public int EmbeddingDimensions { get; init; } = 512;
    public string ChatModel { get; init; } = "gpt-4.1-mini";
    public int ChatMaxOutputTokens { get; init; } = 1200;
    public int ChatMaximumContextCharacters { get; init; } = 32_000;
}

public sealed class SourceChunker : ISourceChunker
{
    private const int LinesPerChunk = 120;
    private const int OverlapLines = 20;
    private const int MaximumCharacters = 12_000;

    public IReadOnlyCollection<TextChunk> Chunk(string path, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return [];
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var chunks = new List<TextChunk>(); var start = 0; var ordinal = 0;
        while (start < lines.Length)
        {
            var take = Math.Min(LinesPerChunk, lines.Length - start); var text = string.Join('\n', lines.Skip(start).Take(take));
            if (text.Length > MaximumCharacters) text = text[..MaximumCharacters];
            if (!string.IsNullOrWhiteSpace(text)) chunks.Add(new TextChunk(ordinal++, start + 1, start + take, $"File: {path}\n{text}"));
            if (start + take >= lines.Length) break;
            start += Math.Max(1, take - OverlapLines);
        }
        return chunks;
    }
}

public sealed class OpenAIEmbeddingGenerator(HttpClient httpClient, IOptions<OpenAIOptions> options) : IEmbeddingGenerator
{
    public string Model => options.Value.EmbeddingModel;
    public int Dimensions => options.Value.EmbeddingDimensions;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<IReadOnlyList<float[]>> GenerateAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new ExternalServiceException("Semantic search is not configured. Add an OpenAI API key and re-index the repository.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "embeddings") { Content = JsonContent.Create(new EmbeddingRequest(Model, inputs, Dimensions)) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new ExternalServiceException("The embedding provider could not process the request.");
        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken) ?? throw new ExternalServiceException("The embedding provider returned an invalid response.");
        var vectors = payload.Data.OrderBy(x => x.Index).Select(x => x.Embedding).ToArray();
        if (vectors.Length != inputs.Count || vectors.Any(x => x.Length != Dimensions)) throw new ExternalServiceException("The embedding provider returned an unexpected vector shape.");
        return vectors;
    }

    private sealed record EmbeddingRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("input")] IReadOnlyList<string> Input, [property: JsonPropertyName("dimensions")] int Dimensions, [property: JsonPropertyName("encoding_format")] string EncodingFormat = "float");
    private sealed record EmbeddingResponse([property: JsonPropertyName("data")] IReadOnlyList<EmbeddingData> Data);
    private sealed record EmbeddingData([property: JsonPropertyName("index")] int Index, [property: JsonPropertyName("embedding")] float[] Embedding);
}

public sealed class PgVectorStore(AppDbContext db) : IVectorStore
{
    public async Task UpsertAsync(Guid organizationId, Guid repositoryId, Guid snapshotId, IReadOnlyCollection<(Guid ChunkId, float[] Embedding)> embeddings, CancellationToken cancellationToken)
    {
        var ids = embeddings.Select(x => x.ChunkId).ToArray();
        var chunks = await db.RepositoryChunks.Where(x => x.OrganizationId == organizationId && x.SnapshotId == snapshotId && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var item in embeddings)
            if (chunks.TryGetValue(item.ChunkId, out var chunk)) db.Entry(chunk).Property<Vector>("Embedding").CurrentValue = new Vector(item.Embedding);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SemanticSearchResult>> SearchAsync(Guid organizationId, Guid repositoryId, float[] query, int limit, CancellationToken cancellationToken)
    {
        var snapshotId = await db.RepositorySnapshots.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId).OrderByDescending(x => x.CreatedAtUtc).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (snapshotId is null) return [];
        var vector = new Vector(query);
        var rows = await db.RepositoryChunks.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.SnapshotId == snapshotId && EF.Property<Vector?>(x, "Embedding") != null)
            .OrderBy(x => EF.Property<Vector>(x, "Embedding").CosineDistance(vector)).Take(limit)
            .Select(x => new { x.Id, x.Document.Path, x.StartLine, x.EndLine, x.Content, x.Snapshot.CommitSha, x.Snapshot.Repository.WebUrl, Distance = EF.Property<Vector>(x, "Embedding").CosineDistance(vector) }).ToArrayAsync(cancellationToken);
        return rows.Select(x => new SemanticSearchResult(x.Id, x.Path, x.StartLine, x.EndLine, x.Content, 1d - x.Distance, x.CommitSha, $"{x.WebUrl}/blob/{x.CommitSha}/{x.Path}#L{x.StartLine}")).ToArray();
    }
}

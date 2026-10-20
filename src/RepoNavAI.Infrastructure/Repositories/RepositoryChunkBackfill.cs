using System.Security.Cryptography;
using System.Text;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Infrastructure.Repositories;

/// <summary>Fills documents that predate semantic indexing without replacing existing chunk identities.</summary>
public static class RepositoryChunkBackfill
{
    // Call with the snapshot's Documents and Chunks loaded. Existing chunked documents are deliberately
    // not re-chunked: changing chunk boundaries requires a separately versioned indexing migration.
    public static void AddMissingChunks(RepositorySnapshot snapshot, ISourceChunker chunker, string embeddingModel,
        CancellationToken cancellationToken)
    {
        var chunkedDocuments = snapshot.Chunks.Select(chunk => chunk.DocumentId).ToHashSet();
        foreach (var document in snapshot.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (chunkedDocuments.Contains(document.Id)) continue;
            foreach (var chunk in chunker.Chunk(document.Path, document.Content))
            {
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chunk.Content)));
                var entity = new RepositoryChunk(snapshot.OrganizationId, snapshot.Id, document.Id, chunk.Ordinal,
                    chunk.StartLine, chunk.EndLine, chunk.Content, hash, embeddingModel);
                document.Chunks.Add(entity);
                snapshot.Chunks.Add(entity);
            }
        }
    }
}

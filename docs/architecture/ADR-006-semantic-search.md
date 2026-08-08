# ADR-006: Provider-neutral semantic retrieval with pgvector

## Status

Accepted

## Context

Repository chat and analysis need meaning-based retrieval with stable source citations. Embedding and storage providers must remain replaceable, while every query and stored chunk must preserve the organization, repository, snapshot, and commit boundaries.

## Decision

Application ports define source chunking, embedding generation, and vector storage. Infrastructure initially implements OpenAI `text-embedding-3-small` embeddings at 512 dimensions and PostgreSQL 17 with pgvector. The model and dimension are explicit configuration, and the database schema validates the fixed vector width.

Documents are split into deterministic 120-line chunks with 20-line overlap, path context, stable ordinals, and content hashes. Chunks are unique per snapshot, document, and ordinal. Embeddings are generated in bounded batches and stored through the vector-store port. Search uses cosine distance against only the latest tenant-scoped snapshot and returns commit-pinned GitHub citations.

If no OpenAI API key is configured, ordinary repository indexing still persists chunks and completes, but semantic search reports that embeddings are not configured. Credentials and source content are never logged.

## Consequences

- Domain and Application code do not depend on OpenAI or pgvector types.
- A 512-dimensional HNSW cosine index provides a practical storage/latency baseline.
- Existing snapshots are immutable and require an explicit re-index workflow to gain embeddings after configuration or analyzer changes.
- Retrieval quality must be evaluated with repeatable repository questions before use in RAG answers.

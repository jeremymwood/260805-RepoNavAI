# Indexing worker operations

Repository indexing runs in the dedicated `RepoNavAI.Worker` process. API replicas accept tenant-authorized commands and persist durable indexing requests, but they do not poll or execute those requests.

## Composition

- `RepoNavAI.Api` owns HTTP endpoints, authentication, authorization, and database migrations.
- `RepoNavAI.Worker` owns `RepositoryIndexingWorker` and uses the same PostgreSQL lease store, GitHub snapshot provider, parsers, and vector provider.
- PostgreSQL row locking and lease ownership continue to make multiple worker replicas safe. Scale the API and worker independently.
- Docker Compose starts PostgreSQL, waits for the API to apply migrations and become healthy, then starts the worker and web frontend.

## Configuration

The worker requires `ConnectionStrings__DefaultConnection`. Configure `GitHub__AccessToken` for private repositories and `OpenAI__ApiKey` when embeddings are enabled. Existing `Indexing__*` values control polling, leases, heartbeat, attempts, and repository limits.

Archive acquisition is streaming and has separate safety boundaries:

| Setting | Default | Purpose |
| --- | ---: | --- |
| `Indexing__MaximumDownloadBytes` | 262,144,000 | Maximum compressed response bytes, checked against `Content-Length` and while reading |
| `Indexing__MaximumExpandedBytes` | 1,073,741,824 | Maximum bytes produced by decompression, including files that are not indexed |
| `Indexing__MaximumArchiveEntries` | 100,000 | Maximum total tar entries before traversal stops |
| `Indexing__MaximumFiles` | 5,000 | Maximum retained supported-source files |
| `Indexing__MaximumFileBytes` | 1,048,576 | Maximum size of one supported-source file |
| `Indexing__MaximumSnapshotBytes` | 52,428,800 | Maximum combined retained supported-source bytes |
| `Indexing__AcquisitionTimeoutSeconds` | 120 | End-to-end limit for commit resolution, download, decompression, and traversal |

Tune these together. The expanded limit must remain above the retained snapshot limit and should reflect worker memory and CPU capacity. Start staging with production-equivalent values, observe acquisition duration, compressed and expanded bytes, entries, and skipped-file metrics, then change one boundary at a time. Do not raise expanded-byte or entry limits merely to make a malformed archive pass.

Deterministic failures such as malformed gzip or tar data, unsafe paths, links, special entries, and configured limit violations fail immediately without consuming every automatic attempt. HTTP 408, HTTP 429, HTTP 5xx, and transport failures remain retryable. Repository cards show the sanitized category message; logs and metrics contain category codes but never archive bodies, file paths, source content, or credentials.

RepoNavAI does not silently perform partial acquisition. Exceeding any safety boundary produces a failed indexing request, so a Completed request always represents the complete set of supported files within the configured policy.

The worker does not run migrations. Apply migrations through the API startup or a dedicated deployment migration job before enabling a new worker revision.

## Health and shutdown

- `GET /health/live` reports that the worker host is running.
- `GET /health/ready` checks PostgreSQL connectivity and should gate worker rollout readiness.
- SIGTERM triggers the .NET host cancellation token. In-flight provider, parsing, persistence, and embedding calls receive cancellation; durable leases allow another worker to recover unfinished processing after lease expiry.

Pause or scale the worker to zero before rollback when a new job format or write path may be incompatible. Never run the worker image with public ingress.

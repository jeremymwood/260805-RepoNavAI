# Indexing worker operations

Repository indexing runs in the dedicated `RepoNavAI.Worker` process. API replicas accept tenant-authorized commands and persist durable indexing requests, but they do not poll or execute those requests.

## Composition

- `RepoNavAI.Api` owns HTTP endpoints, authentication, authorization, and database migrations.
- `RepoNavAI.Worker` owns `RepositoryIndexingWorker` and uses the same PostgreSQL lease store, GitHub snapshot provider, parsers, and vector provider.
- PostgreSQL row locking and lease ownership continue to make multiple worker replicas safe. Scale the API and worker independently.
- Docker Compose starts PostgreSQL, waits for the API to apply migrations and become healthy, then starts the worker and web frontend.

## Configuration

The worker requires `ConnectionStrings__DefaultConnection`. Configure `GitHub__AccessToken` for private repositories and `OpenAI__ApiKey` when embeddings are enabled. Existing `Indexing__*` values control polling, leases, heartbeat, attempts, and repository limits.

The worker does not run migrations. Apply migrations through the API startup or a dedicated deployment migration job before enabling a new worker revision.

## Health and shutdown

- `GET /health/live` reports that the worker host is running.
- `GET /health/ready` checks PostgreSQL connectivity and should gate worker rollout readiness.
- SIGTERM triggers the .NET host cancellation token. In-flight provider, parsing, persistence, and embedding calls receive cancellation; durable leases allow another worker to recover unfinished processing after lease expiry.

Pause or scale the worker to zero before rollback when a new job format or write path may be incompatible. Never run the worker image with public ingress.

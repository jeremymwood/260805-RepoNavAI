# ADR-004: Durable repository indexing pipeline

Status: Accepted - 2026-08-07

## Context

Repository acquisition and parsing can outlive an HTTP request and must recover after process restarts. Multiple application replicas must not process the same request concurrently, and repeated processing of the same commit must not duplicate indexed data. Source content and provider credentials are sensitive and must not appear in logs.

## Decision

PostgreSQL is the durable job queue for the initial deployment. Workers claim pending jobs with `FOR UPDATE SKIP LOCKED`, record an attempt, and hold an expiring lease. A processing job whose lease expires becomes claimable again after a crash. Jobs advance through Queued, Acquiring, Parsing, Persisting, and a final state. Failures retry up to the configured maximum; exhausted jobs retain a safe actionable error and can be explicitly retried. Cancellation is persisted and checked between acquisition and persistence.

GitHub archives are resolved from the registered default branch to an immutable commit SHA, then streamed through bounded, per-job memory rather than extracted into shared filesystem paths. Supported source files have file-count, individual-size, and total-size limits. Credentials, archive response bodies, and source content are excluded from structured logs.

Each repository commit creates one unique snapshot. Documents are keyed by snapshot and normalized path; content hashes support future change detection. C# declarations are parsed with Roslyn behind `ISourceSymbolParser`. The parser and provider boundaries allow additional languages and source-control providers later.

## Consequences

- Registration immediately creates durable work without running analysis in the request.
- Expired leases and commit-level unique constraints make processing restart-safe and idempotent.
- PostgreSQL is sufficient for the initial workload; a dedicated queue can replace claim mechanics later without changing job/domain semantics.
- Source is persisted for later retrieval and embeddings, so production database encryption, access controls, retention, and backups must treat it as confidential customer data.
- Archive and parser limits intentionally reject unusually large repositories with an actionable failure instead of risking unbounded resource use.

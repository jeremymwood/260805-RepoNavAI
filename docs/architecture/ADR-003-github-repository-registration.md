# ADR-003: GitHub repository registration boundary

Status: Accepted - 2026-08-06

## Context

RepoNav AI must verify that a repository exists and is accessible before storing it or requesting indexing. Provider authentication changes over time: local development may use anonymous public access or a narrowly scoped token, while production should use short-lived GitHub App installation tokens. Provider credentials must not leak into domain entities, API responses, logs, or repository metadata.

## Decision

The Application layer depends on `IRepositoryProvider`, which accepts a normalized GitHub repository address and returns verified provider metadata. The initial Infrastructure adapter calls GitHub's REST API using a server-side credential injected through `GitHub:AccessToken`. No provider credential is persisted in PostgreSQL. Production deployments must source this setting from the selected platform's encrypted secret store; `.env` is only for ignored local configuration.

Only HTTPS URLs in the form `https://github.com/{owner}/{repository}` are accepted. SSH URLs, alternate hosts, extra path segments, query strings, and fragments are rejected. Provider ownership and repository names are normalized for tenant-scoped duplicate detection.

A verified registration stores the provider identifier, canonical owner/name, default branch, visibility, canonical web URL, organization, and registering user. In the same database transaction it creates a `Pending` indexing request. Registration does not clone or parse repository contents inside the HTTP request.

Every registration and catalog query first passes organization membership authorization. A database unique constraint on organization, provider, owner, and repository is the final concurrency boundary.

## Consequences

- Public repositories work anonymously for local development, subject to GitHub's lower unauthenticated rate limit.
- Private repositories require a server-configured credential with access to that repository.
- A GitHub App installation-token provider can replace the initial credential source without changing the domain or use cases.
- Provider failures are translated into safe problem details; credentials and GitHub response bodies are not logged.
- Durable indexing execution remains a separate concern; this phase only creates the pending request consumed by the next workflow.

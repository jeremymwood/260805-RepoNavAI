# ADR-001: Clean Architecture boundaries

Status: Accepted — 2026-08-05

## Context

RepoNav AI will integrate source-control providers, multiple vector stores, language models, background indexing, and tenant-specific authorization. Those dependencies will evolve at different rates and must not leak into core business rules.

## Decision

Dependencies point inward: `Api → Infrastructure → Application → Domain`, with API also composing Application. Domain contains entities and invariants without framework references. Application owns use cases, ports, CQRS handlers, and validation. Infrastructure implements persistence, Identity, and token issuance. API is the HTTP composition root.

ASP.NET Identity users remain an Infrastructure concern. Domain entities reference users by stable `Guid`, avoiding an Identity dependency while preserving relational integrity in EF configuration.

## Consequences

- Core rules can be tested without web, database, or identity infrastructure.
- Provider replacements are localized behind Application interfaces.
- Some mapping at boundaries is explicit; this small cost prevents persistence models from becoming the domain model.

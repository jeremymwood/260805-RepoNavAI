# ADR-005: Static ASP.NET endpoint catalog

## Status

Accepted

## Context

RepoNavAI must identify request entry points without executing untrusted repositories. Endpoint data must remain reproducible, tenant-scoped, and tied to the indexed commit.

## Decision

Indexing invokes an `IRepositoryEndpointAnalyzer` extension point. The initial Roslyn implementation recognizes ASP.NET controller attributes and literal minimal-API `Map*` calls. It records method, route, handler, authorization metadata, source location, and bounded downstream invocation candidates on the immutable repository snapshot.

Only statically supported patterns are returned. Dynamic routes and unresolved conventions are omitted rather than guessed. Queries select the latest snapshot inside the organization and support method, route/handler text, and authorization filters. Source links use the snapshot commit SHA.

## Consequences

- Analysis does not execute repository code and results are reproducible.
- Tenant and commit identifiers remain part of the persistence/query boundary.
- Roslyn syntax analysis provides useful coverage but cannot resolve every runtime convention or full call graph.
- Additional framework/language analyzers can be composed behind the same application abstraction.

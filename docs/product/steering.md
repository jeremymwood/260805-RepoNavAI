# RepoNavAI product steering

Last reviewed: 2026-08-08

## Purpose

RepoNavAI reduces the time it takes a software engineer to become productive in an unfamiliar codebase. It turns repository evidence into a guided, navigable model of what the system does, where behavior begins, how code and data flow, and what a proposed change may affect.

This document is the concise source of truth for product direction and current state. The [GitHub project](https://github.com/users/jeremymwood/projects/14) owns work status and sequencing. [Architecture decision records](../architecture/) own durable technical decisions. Detailed requirements remain in their linked GitHub issues.

## Target users and jobs

Primary users are developers onboarding to an existing system, engineers investigating an incident or change, technical leads reviewing architecture, and maintainers planning modernization.

Their core jobs are:

- establish what a repository or system is responsible for;
- find request, event, and scheduled-work entry points;
- trace control flow, data movement, dependencies, and tenant boundaries;
- locate the safest place to implement a feature or fix;
- assess change impact, technical debt, tests, and operational risk;
- build an evidence-backed learning plan instead of guessing what to read first.

## Product principles

- Explain before generating. RepoNavAI helps engineers reason about existing software rather than acting as an autonomous code generator.
- Evidence over confidence. Material claims link to immutable repository sources and insufficient evidence is explicit.
- Tenant isolation by construction. Organization authorization applies before repository retrieval or AI generation.
- Repository content is untrusted data. It cannot supply system instructions or executable UI content.
- Provider-neutral application boundaries. Git, vector, embedding, and model providers remain replaceable infrastructure concerns.
- Durable, observable workflows. Expensive indexing work survives restarts and exposes actionable state.
- Progressive disclosure. Start with useful orientation, then let engineers inspect raw evidence and deeper analysis.

## Current capability matrix

| Capability | Current state | Important limitation or source |
| --- | --- | --- |
| Authentication | ASP.NET Identity registration/login with JWT-protected routes | Session-storage access token is an interim local/MVP design |
| Organizations | Creation, invitations, roles, member removal, tenant-scoped authorization | Administration depth and enterprise identity are future work; see [ADR-002](../architecture/ADR-002-organization-tenancy.md) |
| Repository registration | Public and permitted GitHub repositories are verified, normalized, and registered per organization | GitHub App/webhook lifecycle is not implemented; see [ADR-003](../architecture/ADR-003-github-repository-registration.md) |
| Durable indexing | PostgreSQL-backed jobs acquire pinned snapshots, parse supported source, renew leases, recover after restart, cancel, and retry | API currently hosts the worker; extraction is tracked by [#24](https://github.com/jeremymwood/260805-RepoNavAI/issues/24) |
| Endpoint catalog | ASP.NET controller routes are searchable by method, route, and authorization | Dynamic routes and non-HTTP codebases need capability-aware exploration; see [#31](https://github.com/jeremymwood/260805-RepoNavAI/issues/31) |
| Semantic search | OpenAI embeddings and pgvector return ranked, commit-pinned code citations | Provider rate limits need production backoff/capacity policy; retrieval quality evaluation remains small |
| Repository chat | Authenticated SSE streams source-grounded answers with citations, cancellation, quota, and metadata-only audit | It is one-shot rather than durable multi-turn conversation; see [ADR-007](../architecture/ADR-007-streaming-repository-chat.md) |
| Public preview | GitHub Pages demonstrates the product with safe fixture data | It is read-only and has no API, database, authentication, ingestion, or AI calls |
| Production hosting | Azure Container Apps and PostgreSQL Flexible Server are selected | Resources and runtime CD are not provisioned; see [ADR-008](../architecture/ADR-008-production-hosting.md) |

## Differentiators

RepoNavAI combines repository onboarding, code discovery, and AI explanation in one tenant-aware workspace. Its differentiator is not chat alone: answers, maps, and plans remain inspectable through source citations, indexed commit identity, explicit capability limits, and raw retrieval evidence.

## Direction

The groups below describe investment horizons, not live execution status. Consult the [project board](https://github.com/users/jeremymwood/projects/14) for current priority and status.

### Now

- [#35](https://github.com/jeremymwood/260805-RepoNavAI/issues/35): generate tailored repository orientation plans.
- [#31](https://github.com/jeremymwood/260805-RepoNavAI/issues/31): make repository exploration capability-aware for non-API codebases.
- [#32](https://github.com/jeremymwood/260805-RepoNavAI/issues/32): support safe repository deregistration and derived-data cleanup.
- [#19](https://github.com/jeremymwood/260805-RepoNavAI/issues/19): produce prompted, cited code-flow summaries and visual maps.

### Next

- [#24](https://github.com/jeremymwood/260805-RepoNavAI/issues/24): separate the indexing worker from API scaling.
- [#25](https://github.com/jeremymwood/260805-RepoNavAI/issues/25): provision the selected Azure foundation with infrastructure as code.
- [#26](https://github.com/jeremymwood/260805-RepoNavAI/issues/26): promote immutable releases through staging and production.
- [#21](https://github.com/jeremymwood/260805-RepoNavAI/issues/21) and [#20](https://github.com/jeremymwood/260805-RepoNavAI/issues/20): consolidate the design system, then add light, dark, and system themes.

### Later

- [#2](https://github.com/jeremymwood/260805-RepoNavAI/issues/2): interactive whole-repository architecture maps after dependency extraction exists.
- Documentation generation, technical-debt analysis, test suggestions, refactoring guidance, repository health, and administration remain roadmap themes that require scoped issues before implementation.

## Non-goals for the current horizon

- Autonomous code changes, merges, or deployments based only on model output.
- Claiming runtime behavior that cannot be supported by static repository evidence.
- Replacing source control, IDE navigation, observability platforms, or human architecture review.
- Treating the GitHub Pages fixture preview as production application hosting.
- Supporting every language and framework before analyzer extension points and quality fixtures are proven.

## Risks and controls

| Risk | Current control | Owner or follow-up |
| --- | --- | --- |
| Cross-tenant data exposure | Membership authorization before repository access; organization metadata on indexed records and vectors | Application/platform owner; [ADR-002](../architecture/ADR-002-organization-tenancy.md) |
| Prompt injection or unsupported claims | Repository content delimited as untrusted evidence; plain-text output; application-created citations | AI feature owner; [ADR-007](../architecture/ADR-007-streaming-repository-chat.md) |
| Provider rate limits and spend | Bounded batches/context/output and organization chat quota | Application/platform owner; backoff and budget guardrails must be scoped before production traffic |
| Interrupted or duplicate indexing | Durable PostgreSQL jobs, renewable owner leases, concurrency fencing, commit uniqueness | Indexing owner; [ADR-004](../architecture/ADR-004-durable-repository-indexing.md) |
| Production data loss or unsafe release | Managed backups/PITR, expand-contract migrations, immutable revisions, staged promotion design | Platform owner; [production deployment strategy](../operations/production-deployment.md) |
| Documentation drift | Event-based review policy, PR checklist, and CI link/format validation | PR author and reviewer; [maintenance policy](#maintenance-policy) |

## Measures of success

Product evaluation should measure time to first accurate architectural explanation, time to locate a change point, citation correctness, code-flow accuracy, orientation-plan completion, insufficient-evidence correctness, indexing success/recovery, and cost per indexed repository or answer. Usage volume alone is not evidence that developers understand a system faster.

## Maintenance policy

The repository owner is accountable for this document; every contributor keeps affected sections accurate. Review it at least monthly while active development continues and whenever any of these events occurs:

- a user-facing capability or material limitation changes;
- a milestone completes or the Now / Next / Later horizon changes;
- an ADR is accepted or superseded;
- hosting, security, privacy, cost, or recovery posture changes;
- a material product or operational risk is discovered or closed.

README changes should explain shipped capabilities and how to evaluate or operate them. Steering changes should explain current state and direction. ADRs explain why durable architecture decisions were made. Runbooks explain repeatable operations. The project board remains authoritative for status.

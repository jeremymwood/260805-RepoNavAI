# RepoNavAI product steering

Last reviewed: 2026-10-21

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
| Authentication | ASP.NET Identity with an HttpOnly JWT session cookie and configured external sign-in | Apple and Microsoft live rollout remain [#90](https://github.com/jeremymwood/260805-RepoNavAI/issues/90) and [#91](https://github.com/jeremymwood/260805-RepoNavAI/issues/91); see the [operations guide](../operations/external-authentication.md) |
| Organizations | Creation, invitations, roles, member removal, tenant-scoped authorization | Administration depth and enterprise identity are future work; see [ADR-002](../architecture/ADR-002-organization-tenancy.md) |
| Repository registration | Public and permitted GitHub repositories are verified, normalized, and registered per organization | GitHub App/webhook lifecycle is not implemented; see [ADR-003](../architecture/ADR-003-github-repository-registration.md) |
| Durable indexing | Dedicated Worker uses PostgreSQL jobs, pinned snapshots, leases, heartbeats, recovery, cancellation, retry and bounded archive acquisition | Distributed trace correlation and provider diagnostics remain incomplete; see the [worker runbook](../operations/indexing-worker.md) |
| Source and endpoint discovery | Polyglot source ingestion, coverage reporting, capability-aware workspace and searchable ASP.NET endpoint catalog | Symbol extraction is C#; arbitrary language/framework semantic reference analysis is not implemented |
| Repository lifecycle | Favorites, pagination, re-indexing and confirmed removal with derived-data cleanup | GitHub App/webhook lifecycle remains separate work |
| Semantic search | OpenAI embeddings and pgvector return ranked sources with immutable links | No versioned quality baseline; snapshot consistency and context selection need regression cases |
| Repository chat | Authenticated SSE answers with citations, cancellation, session quota and private saved history | One-shot generation, not a multi-step agent; grounding and repeated-run reliability are not measured |
| Orientation and code flow | Tailored saved orientation/progress and prompted, validated flow steps with application-rendered diagrams | Valid citation indices and structured output do not establish semantic correctness |
| Architecture maps | Commit-pinned interactive Architecture, Flowchart and Tree layouts with evidence and text fallback | Static repository evidence is not proof of complete runtime behavior |
| Assistant history | Per-user results with stars, rename/delete, retention, schema compatibility and staleness | Saved results are not full execution traces; see [history boundaries](assistant-history.md) |
| Frontend | Responsive themes, shared styles, loading/error/recovery states and browser visual checks | Further visual work stays separate from AI reliability foundations |
| Public preview | GitHub Pages demonstrates the product with safe fixture data | It is read-only and has no API, database, authentication, ingestion, or AI calls |
| Production hosting | Azure target, Bicep, dedicated Migrator, immutable image publishing and promotion/rollback workflows exist | Live provisioning and operating readiness were not verified in this audit; see [deployment strategy](../operations/production-deployment.md) |
| AI engineering reliability | AI-01 / #95 shipped metadata-only run correlation, optional tracing, acquisition metrics, and provider-independent tests | AI-02 / #98 adds evidence/provider diagnostics; local automated and live acceptance passed, with review/CI still required before merge. Dataset/runner/baseline issues #99/#100/#101 are published. Tools and MCP follow the [roadmap](../ai-engineering-roadmap.md); see [AI observability](../ai-observability.md) |

## Differentiators

RepoNavAI combines repository onboarding, code discovery, and AI explanation in one tenant-aware workspace. Its differentiator is not chat alone: answers, maps, and plans remain inspectable through source citations, indexed commit identity, explicit capability limits, and raw retrieval evidence.

## Direction

The groups below describe proposed investment horizons, not live execution status. Consult the [project board](https://github.com/users/jeremymwood/projects/14) for current priority and status. The [AI engineering audit](../ai-engineering-gap-analysis.md) supersedes the outdated foundation-work list previously recorded here.

### Now

- Complete selected-evidence/provider diagnostics (#98), following the shipped safe tracing foundation (#95).
- Author a pinned 25-case evaluation dataset and record the first manually reviewed baseline before introducing agents.
- Track a supported .NET LTS upgrade before .NET 9 support ends on November 10, 2026. See [Microsoft's policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) and the [draft platform work item](../development/ai-engineering-work-items.md#plat-01-upgrade-the-supported-net-runtime-and-toolchain).
- Follow the [active delivery queue](../development/ai-engineering-work-items.md#active-delivery-queue): #98, #99, #102, #96, #100, #101. Keep Apple/Microsoft rollout and other older issues deferred; do not choose them ahead of this roadmap merely because they share P1.

### Next

- Turn observed failures into deterministic evaluators, regression fixtures and small reliability fixes.
- Make request snapshot identity, selected context, provider limits and cancellation behavior testable.
- Introduce a small read-only tool layer and compare repeated runs against the existing one-shot baseline.

### Later

- Expose one or two authorized tools through MCP, then introduce bounded multi-step investigation where evaluation demonstrates value.
- Calibrate semantic judges only where deterministic checks cannot establish correctness; publish a measured reliability case study.
- Assess an isolated Python evaluation experiment only if it fills a demonstrated gap.
- Preserve [#93](https://github.com/jeremymwood/260805-RepoNavAI/issues/93), [#85](https://github.com/jeremymwood/260805-RepoNavAI/issues/85) and [#77](https://github.com/jeremymwood/260805-RepoNavAI/issues/77) as separate visual investments after the AI foundations.
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
| Unmeasured or inconsistent evidence | Immutable source links, prompt bounds, structured citation validation and manual acceptance | AI feature owner; proposed context manifests, snapshot regression cases and evaluation baseline |
| Sensitive data entering diagnostics | Private history has separate user access and content restrictions | AI/platform owner; telemetry allowlists and redaction tests must cover SDKs, HTTP metadata and exceptions |
| Unsupported runtime | Current application targets .NET 9 | Platform owner; proposed PLAT-01 before 2026-11-10 |
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

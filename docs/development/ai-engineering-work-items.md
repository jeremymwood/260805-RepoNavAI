# AI engineering: first-increment work items

Prepared 2026-10-15 against `bc5efb6c52899776ccfdaf9dd59e58830f1fd36d`; publication/status review 2026-10-19. AI-01 / [#95](https://github.com/jeremymwood/260805-RepoNavAI/issues/95) is complete through merged PR #97. The first-increment drafts below are now published; GitHub owns live status. See the [roadmap](../ai-engineering-roadmap.md) for later increments and the [audit](../ai-engineering-gap-analysis.md) for code evidence.

Use the [existing intake process](work-management.md). On publication, replace proposal dependencies with GitHub issue links, add each issue to Project 14, and set **both** Priority and Size fields as well as matching labels. The intake workflow adds an item but does not assign those fields. Keep status Backlog until work starts; do not mark this entire initiative In progress. No duplicate tracing/eval issue was found in the open-issue query on the preparation date; recheck before publishing.

## Active delivery queue

These P1 / M items take precedence over the older deferred backlog. Only #98 is In progress. Priority is a bucket; this order and the dependencies determine execution. Wait for acceptance/review at each handoff. No paid evaluation is authorized by issue publication.

| Order | Issue | Dependency / scheduling constraint |
| --- | --- | --- |
| 1 | [#98: AI-02 evidence and provider diagnostics](https://github.com/jeremymwood/260805-RepoNavAI/issues/98) | #95 complete |
| 2 | [#99: AI-03 verified evaluation dataset](https://github.com/jeremymwood/260805-RepoNavAI/issues/99) | Audit; independent of #98 |
| 3 | [#102: PLAT-01 supported .NET toolchain](https://github.com/jeremymwood/260805-RepoNavAI/issues/102) | Target 2026-11-06, before support ends 2026-11-10; move earlier if the deadline is at risk |
| 4 | [#96: Tailwind build-tool security](https://github.com/jeremymwood/260805-RepoNavAI/issues/96) | Separate maintenance PR; no production npm advisories in the preceding verification |
| 5 | [#100: AI-04 bounded evaluation runner](https://github.com/jeremymwood/260805-RepoNavAI/issues/100) | #98 and #99; stabilize runtime before a comparable baseline batch |
| 6 | [#101: AI-05 observed baseline/failure taxonomy](https://github.com/jeremymwood/260805-RepoNavAI/issues/101) | #100; separate approval/budget for paid runs |

AI-06 through AI-16 remain planned inventory entries, not published issues. Refine and publish those at their evidence/dependency gates rather than treating unobserved failure hypotheses as implementation-ready fixes.

## AI-01: Trace AI requests and indexing attempts without exposing content

Implementation and local verification: [AI observability](../ai-observability.md). Manual acceptance completed on 2026-10-19; PR #97 merged with CI passing and #95 closed.

Priority: P1. Size: M. Suggested labels: `type:infrastructure`, `area:platform`, `priority:P1`, `size:M`.

**Problem and value:** Serilog and acquisition metrics exist, but a request cannot be inspected as one retrieval/model execution. Add the correlation and privacy boundary that later evaluation and failure analysis require.

**Approach:** Introduce application-owned operation names and run identifiers. Use `ActivitySource`/`Meter`, register instrumentation/export in the API and Worker composition roots, and preserve the acquisition meter. Use an optional OTLP destination with a documented local inspection path. Keep vendor-specific code out of Domain/Application. Correlate durable job IDs and attempt identities across enqueue/restart; do not hold an HTTP parent span open while a job waits.

**Acceptance criteria:**

- [ ] Search, Answer, Orientation and CodeFlow have a run identifier and terminal status; incoming trace context is not used as authorization.
- [ ] The initial Search/Answer trace covers authorization, embedding, vector search and stream lifetime. Worker attempts identify the enqueue/job and their own attempt boundaries. Detailed generation metadata follows in AI-02.
- [ ] API responses expose an opaque diagnostic identifier through a compatible response mechanism; existing SSE and JSON clients continue to work.
- [ ] Logs correlate to traces; missing listeners/sampling do not erase the run identifier. Existing acquisition metrics are exported rather than duplicated.
- [ ] Metrics use bounded dimensions. Organization/repository IDs occur only in restricted trace metadata; no user email, prompts, source text, raw argument values, credentials, query strings or provider bodies are captured.
- [ ] Automatic HTTP/SQL/SDK instrumentation and exception handling are tested against the same allowlist, including provider errors containing a secret-like canary.
- [ ] Export disabled/unavailable does not fail or stall normal user requests; export queues and shutdown flushing are bounded.
- [ ] `docs/ai-observability.md` documents fields, exclusions, retention/access, sampling, local setup and how to inspect one run.

**Automated tests:** In-memory listener/exporter checks for parent-child correlation, cancellation/failure completion, denied requests before downstream work, telemetry-disabled behavior, bounded attributes and canary exclusion. Keep tests independent of a paid provider.

**Manual verification:** In an isolated local environment, inspect one successful Answer, a Search, a cancelled stream, an unauthorized request and a restarted indexing attempt. Correlate the logs, then disable the collector and confirm the app still responds. Record the steps in the acceptance runbook.

**Dependencies:** Audit. AI-03 can proceed independently.

**Out of scope:** Prompt/result capture in production logs, metrics dashboards, eval scoring, model tool calling, replacing history or logging infrastructure.

**Rollout/rollback:** Export is opt-in; disable export to roll back operational collection. Document any additive durable job-correlation migration and its backward compatibility before merging.

## AI-02: Record selected evidence, provider usage and generation outcomes

Published as [#98](https://github.com/jeremymwood/260805-RepoNavAI/issues/98). In progress pending review/CI; local automated and live acceptance passed on 2026-10-21. Implementation, acceptance instructions and verified results are in [AI observability](../ai-observability.md#ai-02-evidence-and-provider-diagnostics-98).

Priority: P1. Size: M. Suggested labels: `type:infrastructure`, `area:platform`, `priority:P1`, `size:M`.

**Problem and value:** Retrieved sources are not necessarily the sources included in a truncated prompt. Provider usage and per-stage failures are lost at application boundaries. Evaluation needs the exact context selection and call outcome, not only the final answer.

**Approach:** Add a typed context manifest and provider-call diagnostics behind existing generation interfaces. Preserve behavior while making candidate order, actual included ranges/character counts, truncation and citation mapping observable. Avoid a separate retrieval implementation or capturing model reasoning. Extend tracing to structured generators and embedding batches.

**Acceptance criteria:**

- [ ] Search/Answer/Orientation/CodeFlow record snapshot, embedding model/configuration, prompt-template and retrieval versions, candidate IDs/scores, and selected evidence IDs/ranges in restricted metadata.
- [ ] Manifest entries describe the actual text supplied, including mid-chunk truncation. Candidate-only sources cannot be mistaken for model-visible sources. Existing mismatches are reported for AI-06 rather than silently labeled correct.
- [ ] Model/provider, actual execution parameters, context sizes, model-call count, total duration and first-delta latency are available where applicable.
- [ ] Provider-reported input/output/total usage is retained for generation and embeddings where the pinned SDK/API provides it. Unavailable or incomplete stream usage is explicitly unknown.
- [ ] Cancellation, timeout, empty evidence, invalid structure, provider failure, history-persistence failure and success have distinct outcomes. Pre-stream and post-stream failures remain distinguishable.
- [ ] Effective retry behavior is documented; observed SDK attempts are captured if available, with unknown values explicit when they are not observable.
- [ ] Diagnostics do not retain raw prompts, source, responses or unsafe exceptions. Exact evaluation content requires the separate allowlisted artifact mode in AI-04.
- [ ] `docs/ai-observability.md` describes which questions production metadata can answer and which require an approved evaluation bundle.

**Automated tests:** Fake completion streams with usage present/absent, cancellation before/after first delta, invalid JSON, partial source truncation, oversized headings, dropped chunks and failed history writes. Assert manifest content against the actual generated prompt and that no full content reaches telemetry.

**Manual verification:** Inspect one long-context Answer and one CodeFlow request. Match selected IDs/ranges to prompt construction using a public fixture; confirm candidates omitted by the budget are labeled omitted. Cancel a stream and verify incomplete usage remains unknown.

**Dependencies:** AI-01. Keep any source-selection fixes independently reviewable under AI-06.

**Out of scope:** New prompts or ranking, a pricing service, agent steps, a public trace viewer, transcript capture for ordinary users.

**Rollout/rollback:** Additive diagnostics and optional export. Maintain current public response contracts; version any internal artifact contract independently of assistant history.

## AI-03: Author 25 verified evaluation cases against immutable corpora

Published as [#99](https://github.com/jeremymwood/260805-RepoNavAI/issues/99), Backlog.

Priority: P1. Size: M. Suggested labels: `type:infrastructure`, `area:platform`, `priority:P1`, `size:M`.

**Problem and value:** Manual examples and mocked tests cannot measure whether retrieval and generated claims remain correct across model or prompt changes. Establish reviewed expected evidence before building graders.

**Approach:** Add a small versioned JSON/JSONL dataset under `evals/` and a corpus manifest. Use 15 cases from RepoNavAI at the audited commit and 10 from a project-owned non-web fixture with a fixed content hash. Keep expected answers outside the indexed source set. Reuse current schemas where applicable without making them provider-specific.

**Acceptance criteria:**

- [ ] At least 25 cases identify corpus revision, mode/profile, prompt, expected files/symbols where meaningful, required evidence, allowed alternatives, expected completion/refusal behavior and tags.
- [ ] Cases cover authorization, endpoints, persistence, configuration, implementation lookup, code/worker flow, orientation, non-web behavior, insufficient evidence, injection attempts and citation correctness.
- [ ] Expected evidence is reviewed against real source and valid line ranges; summaries distinguish direct observations from architectural inference.
- [ ] Corpus licensing/provenance, setup, fixed revisions and source inclusion rules are documented. No private/customer code or secrets enter fixtures or public CI artifacts.
- [ ] An offline validator rejects duplicate IDs, invalid case schemas, missing corpus revisions, stale hashes, absent files/symbol evidence and invalid ranges.
- [ ] The test corpus excludes the case questions/expected answers to prevent retrieval of the evaluation dataset itself.
- [ ] A small held-out subset is marked and kept out of prompt-tuning feedback. Dataset versions and evidence-review notes are recorded.

**Automated tests:** Validate the real dataset and deliberately broken examples for each rejection condition. Run without model credentials or network access once the documented corpus checkout is available.

**Manual verification:** A reviewer checks five representative cases and every negative/adversarial case against source. Reproduce the checkout from its full commit/hash and explain why each expected result is justified.

**Dependencies:** Audit only; safe to author alongside AI-01.

**Out of scope:** Generated ground truth, live scoring, additional language analyzers, scraped private repositories, large benchmark infrastructure.

**Rollout/rollback:** Versioned data and validation only. Change an incorrect expectation through review and invalidate affected baseline comparisons explicitly.

## AI-04: Build a bounded .NET evaluation runner with reproducible artifacts

Published as [#100](https://github.com/jeremymwood/260805-RepoNavAI/issues/100), Backlog; blocked by #98 and #99.

Priority: P1. Size: M. Suggested labels: `type:infrastructure`, `area:platform`, `priority:P1`, `size:M`.

**Problem and value:** Browser-only testing is difficult to replay or compare. A small runner should execute the real use cases and retain evidence for manual review without duplicating the product's RAG implementation.

**Approach:** Add an isolated .NET CLI/test host that calls the existing application services. Use a disposable, explicitly selected evaluation database and test identities, with tenant authorization exercised. Preserve the current API/browser tests for transport/SSE coverage. Resolve and verify corpus/index manifests before a run; the runner must never implicitly use the developer's ordinary database or re-index a moving branch.

**Acceptance criteria:**

- [ ] Select cases/modes and an explicit repeat count. Dry-run validates manifests and reports planned calls without contacting providers.
- [ ] Live provider access is opt-in and requires configured call/token limits, timeout and concurrency limits. Apply safe pre-call maximums and stop scheduling new work when limits are reached.
- [ ] Optional monetary limits use explicit versioned rates; when rates are unknown, report unknown cost and enforce call/token limits instead of inventing prices.
- [ ] A run manifest records application revision, corpus/snapshot identity, prompt/retrieval/index settings, model/SDK versions, repeat index and artifact schema version.
- [ ] Every attempt retains trace/run IDs, retrieval and selected-context metadata, output/citations, safe outcome, latency and usage availability. Failed/cancelled/blocked runs are retained with reasons.
- [ ] Exact prompt/context/output capture is restricted to explicitly allowlisted public/synthetic fixtures and bounded artifacts. A denied private corpus fails before any upload or provider call.
- [ ] Artifacts have an access/retention policy and are ignored by default in Git. Only reviewed, sanitized case-study examples may be committed later.
- [ ] The runner defaults to local/manual invocation. A documented command executes one offline fixture and a separate explicit command executes one paid case.

**Automated tests:** Fake providers verify dry-run, budget exhaustion, concurrent reservation, cancellation, usage absence, interrupted writes, disallowed corpora and stable manifests. Add disposable PostgreSQL/pgvector tests with fixed vectors for actual tenant/snapshot filtering and ordering; keep them separate from paid inference.

**Manual verification:** Run one allowlisted case, inspect its trace/artifact, repeat it, and confirm separate attempts. Exhaust a tiny call budget and verify the runner stops. Use only an isolated database and record that isolation in the run manifest.

**Dependencies:** AI-02 and AI-03. Use a fixed verified index for the initial baseline; record the snapshot race risk until AI-06 is complete.

**Out of scope:** Python, a web benchmark portal, autonomous ingestion, paid scheduled CI, semantic grading, production transcript export.

**Rollout/rollback:** Separate opt-in executable and data directory; removing or disabling it does not affect production requests. Recreate only the explicitly scoped disposable evaluation store.

## AI-05: Review baseline runs and derive an observed failure taxonomy

Published as [#101](https://github.com/jeremymwood/260805-RepoNavAI/issues/101), Backlog; blocked by #100.

Priority: P1. Size: M. Suggested labels: `type:infrastructure`, `area:platform`, `priority:P1`, `size:M`.

**Problem and value:** Evaluators should reflect actual failures rather than a speculative list. Establish the first honest reliability baseline and use it to select improvements.

**Approach:** Start with ten varied runs and inspect their traces/evidence manually. Then target 25 cases repeated twice, with up to 100 total runs if the configured budget permits. Record any shortfall and its cause; paid runs are a later implementation activity, not part of this planning pass.

**Acceptance criteria:**

- [ ] All runs identify corpus/model/application revisions and planned vs completed attempt counts; no result is fabricated or silently dropped.
- [ ] At least the first ten varied real runs, including failures in that sample, have human labels, reviewer notes and evidence references. Review subsequent failures before reporting improvements.
- [ ] `docs/ai-failure-taxonomy.md` distinguishes observed categories, unconfirmed hypotheses, infrastructure failures and unreviewed cases, with run IDs for observed examples.
- [ ] Baseline reporting includes per-case results, repeated-run instability, latency and token availability. An average never hides an individual failure or a missing run.
- [ ] The highest-impact reproduced problems become focused regression/fix issues, including AI-06 when its static risks are reproduced.
- [ ] Scoring rubrics and proposed acceptance thresholds are grounded in these labels; unsupported semantic conclusions are not graded by substring alone.
- [ ] Published examples are sanitized and explicitly distinguish manual judgment from automated scoring.

**Automated tests:** Reuse the runner/schema checks; test any report aggregation with known mixed outcomes so failed/cancelled runs cannot disappear. No test is needed merely to assert that a Markdown taxonomy exists.

**Manual verification:** Follow a failed case from run manifest to selected context to output, state the cause and competing explanation, and verify a second repeat. Have disputed labels adjudicated before using them as judge calibration data.

**Dependencies:** AI-04. Supplies evidence for AI-06/AI-07 and later repeated-run comparisons.

**Out of scope:** Claiming production-wide reliability from a small sample, LLM-only ground truth, a case study without measured improvement, automatic tuning.

**Rollout/rollback:** Findings and artifacts only. Correct labels through versioned review; keep original run data and distinguish reclassification from a new execution.

## PLAT-01: Upgrade the supported .NET runtime and toolchain

Published as [#102](https://github.com/jeremymwood/260805-RepoNavAI/issues/102), Backlog; project target date 2026-11-06.

Priority: P1. Size: M. Suggested labels: `type:infrastructure`, `area:platform`, `priority:P1`, `size:M`. Target: before 2026-11-10.

**Problem and value:** The app targets .NET 9, whose support ends November 10, 2026. A supported LTS baseline keeps the reliability initiative deployable. [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

**Approach:** Review .NET 10 LTS and compatible EF Core/Npgsql/pgvector, authentication, Semantic Kernel and test dependencies. Update shared target settings, SDK selection, Docker images, CI and operations docs in a separate PR. Keep application behavior and AI model selection stable where possible.

**Acceptance criteria:**

- [ ] API, Worker, Migrator and test projects build on the selected supported toolchain; compatible dependencies are centrally pinned.
- [ ] SDK/runtime containers and Actions use the same supported major version; no service is accidentally left on .NET 9.
- [ ] Database mappings and migration behavior are reviewed on a disposable PostgreSQL instance; no unrelated data/schema redesign is introduced.
- [ ] Authentication, indexing/recovery, SSE cancellation, search, structured AI responses and frontend compatibility pass relevant automated/manual checks.
- [ ] SDK retry/usage changes affecting AI-02 are recorded, and baseline manifests distinguish runtime/SDK versions.
- [ ] Operations docs describe rollout and rollback compatibility. Previous container artifacts are retained according to the existing deployment policy.

**Automated tests:** Solution restore/build/tests, container builds, migration smoke test, frontend build and affected integration checks.

**Manual verification:** Start the full local stack; authenticate; index a small fixture; check search/stream cancellation and worker recovery. Confirm runtime versions in the built images without printing environment secrets.

**Dependencies:** Compatibility review. Can proceed alongside AI-01/AI-03; avoid changing the runtime halfway through a comparable baseline batch.

**Out of scope:** Rewriting in Python, changing the architecture, upgrading unrelated UI libraries, or selecting a new AI model as part of runtime maintenance.

**Rollout/rollback:** Promote immutable artifacts using the existing deployment workflow. If migration compatibility changes, document and test the compatible rollback boundary before deployment.

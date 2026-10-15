# AI engineering maturity roadmap

Date: 2026-10-15. Status: proposed delivery plan; runtime implementation has not started under this initiative.

RepoNavAI should help developers understand unfamiliar codebases and make the reliability of its answers inspectable. This roadmap adds evidence about model behavior, retrieval quality, failure handling, cost and repeatability to the existing product. Its portfolio value should come from recorded results, not extra frameworks or unsupported claims.

The [gap analysis](ai-engineering-gap-analysis.md) establishes the source baseline. The [project board](https://github.com/users/jeremymwood/projects/14) continues to own delivery status. `AI-xx` identifiers below are proposal IDs, not GitHub issue numbers. The [issue drafts](development/ai-engineering-work-items.md) are ready for intake using the existing Priority and Size conventions.

## Delivery sequence and gates

| Increment | Proposed work | Completion evidence |
| --- | --- | --- |
| 0. Audit | Compare code, tests, docs and backlog | This audit and plan; static risks separated from observed failures |
| 1. Trace existing behavior | AI-01 tracing/export and AI-02 evidence/model diagnostics | A request can be followed through authorization, retrieval, context, provider and terminal state without leaking prompts or source |
| 2. Establish a baseline | AI-03 dataset, AI-04 runner, AI-05 manual analysis | 25 reviewed cases, approximately 50-100 recorded runs within a configured budget, first meaningful sample manually labeled, and an observed failure taxonomy |
| 3. Make reliability measurable | AI-06 snapshot/context regression fixes, AI-07 deterministic evaluators, AI-08 resilience | Reproducible failing cases, enforced tenant/source/schema limits, documented deadline/retry behavior and offline CI checks |
| 4. Add controlled tools | AI-09 tool dispatcher; AI-10 repeated-run comparison | Validated read-only tools; per-case comparisons against the one-shot baseline; cost and reliability changes remain visible |
| 5. Expose and orchestrate | AI-11 small MCP surface; AI-12 bounded multi-step workflow | Same authorization and tool services through both entry points; agent adoption justified on cases the baseline handles poorly |
| 6. Refine with evidence | AI-13 calibrated semantic judge, AI-14 cost analysis, AI-16 case study | Human/judge agreement report where needed; one measured improvement with tradeoffs and remaining failures |
| Optional | AI-15 Python evaluation experiment | Adopt only if a specific evaluation capability outweighs a second runtime and duplicated maintenance |

The dataset can be authored while tracing is implemented. Export latency, call counts and available token usage in increment 1; defer pricing and optimization decisions until measurements exist. Reliability protections apply to every increment, even though dedicated fault testing is listed in increment 3. MCP does not have to block the internal agent experiment; both depend on the same safe dispatcher and evaluation gates.

The supplied proposal's phases 1/11 become tracing plus later cost analysis; phases 4-8 become the baseline and regression increments; phases 2/3/9 become tools, MCP and bounded orchestration; phases 10/12/13 become resilience, optional Python and the case study. This puts evaluation ahead of new agent behavior while retaining the full proposal.

## First architectural choices

### Trace existing application boundaries

Use .NET `ActivitySource` and `Meter` at application/provider boundaries, with OpenTelemetry configured in the composition roots and an optional OTLP destination. Keep Serilog and the existing acquisition meter. This follows [.NET instrumentation guidance](https://opentelemetry.io/docs/languages/dotnet/instrumentation/) and avoids coupling use cases to a particular tracing vendor. Select and pin compatible package/exporter versions when implementing AI-01; no tracing dependency is introduced by this plan.

Create a run ID even when no trace listener is active. Correlate the API request, assistant-history/session ID where present, and model/retrieval stages. Indexing jobs need durable enqueue-to-attempt correlation, including restarts. Log failures safely, and do not let exporter failure break an answer. Eval runs should have complete local records independent of production trace sampling.

Record timestamps, elapsed time, operation/mode, application revision, prompt/retrieval versions, snapshot/configuration identity, provider/model, actual parameters, context sizes, candidate/selected counts, outcome and safe error categories. Record time to first delta separately from total stream duration. Capture provider-reported input/output/total tokens and per-call attempts when available; represent unavailable values as unknown, not zero or an inferred token count. There are no tool steps yet; add tool name, validated argument metadata, duration and step counts when tools exist.

### Separate operational telemetry from evaluation evidence

| Data | Default destination and restriction |
| --- | --- |
| Counts, duration, model, mode, failure category | Metrics with bounded label cardinality |
| Run/trace ID, authorized opaque organization/repository IDs, snapshot and evidence IDs | Restricted traces, not metric labels; no user email or repository URL by default |
| Candidate ranking, selected excerpt ranges, truncation and citation mapping | Bounded metadata; raw paths/source remain restricted because names can be sensitive |
| Prompts, supplied excerpts, outputs and human labels for evaluation | Explicitly enabled, allowlisted public/synthetic corpus artifacts with access and retention controls; never general-purpose production logs |
| Secrets, credentials, cookies, authorization headers, arbitrary tool argument values | Never captured; sanitize exception data and automatic HTTP/SQL/SDK instrumentation too |

Production telemetry can explain selection and timing without retaining unrestricted repository contents. It cannot reconstruct every historical model input when content retention is disabled; state that limitation. An opt-in evaluation bundle can record the exact supplied context for approved fixtures. Existing private user history remains separate from operator telemetry and must retain its current access boundary. Do not store hidden model reasoning; inspect observable tool choices, evidence and outputs.

### Reuse the current services for tools

Start with `search_code` and `find_api_endpoint`, reusing retrieval and catalog services through authorized application handlers. Avoid routing a model tool through a handler that creates a new user-visible history entry on every internal call; factor only the shared read operation when necessary.

Bind user, tenant, repository and snapshot from trusted server context. The model supplies a search query or route filter, not authority. Use strict schemas, input/output limits, read-only allowlists, deadlines, cancellation, safe structured errors and tracing. `read_file` can follow as bounded line-range reads from the indexed snapshot, not host paths or arbitrary URLs. C# symbol lookup is plausible; general `find_references` requires additional semantic analysis and must not be advertised as already available.

MCP should initially expose those same one or two tools behind an explicit authenticated identity mapping. Validate its transport and authorization design in a short ADR using the current protocol/SDK guidance before implementation. An MCP client cannot choose arbitrary tenant credentials or bypass normal membership checks. Preserve .NET and Semantic Kernel unless evals demonstrate a specific reason to change them.

### Make agent behavior earn its complexity

The current deterministic router and three-query CodeFlow retrieval remain the baseline. A later agent may choose among approved reads with an initial ceiling of five tool invocations, three model calls, and a total deadline no greater than 90 seconds. These are proposed limits to validate, not shipped defaults. Bound aggregate tool output/context, stop on sufficient evidence or no progress, and never autonomously write to a repository.

Do not automatically replay a streamed response after text has reached the client. Define an explicit user retry and attempt identity; apply bounded transient retries only where replay is safe. Preserve cancellation across provider, tool, history and telemetry cleanup. Avoid retry amplification between SDK, application and job layers.

## Evaluation design

Start with 25 cases: 15 against RepoNavAI at the audited commit and 10 against a small project-owned non-web fixture. The fixture should contain a CLI/event flow, configuration, interfaces, tests, an unsupported capability and adversarial repository text. This avoids licensing ambiguity and moving third-party HEADs while testing more than ASP.NET. Add another pinned real repository after the initial harness is useful.

Each case records an ID, corpus and immutable commit/content hash, explicit mode, question/profile, expected files or symbols where applicable, required evidence and allowed alternatives, expected success/refusal behavior, and tags. Keep the dataset outside its indexed corpus so expected answers cannot be retrieved as source. An example seed to verify during dataset authoring is: "Where is organization membership checked before repository chat?" Its evidence should include `StreamRepositoryChatHandler` and `OrganizationAccess.RequireAsync`, not merely a mention in documentation.

Cover search/authorization, endpoint mapping, persistence, configuration, implementation lookup, request/worker flows, orientation, source grounding, non-web behavior and insufficient evidence. Include adversarial and fault scenarios. For reference/call-path questions, record the actual supported static evidence and known gaps; never use a generated answer as its own ground truth.

Separate three forms of validation:

- Deterministic PR checks: schema validation, fixture hashes, source/range/commit integrity, authorization, selected-evidence IDs, structured output, tool schema/allowlist and limits. Run with fake providers and controlled vectors; they do not measure model quality.
- Real retrieval checks: a disposable PostgreSQL/pgvector database and known vectors to verify filtering, ranking and snapshot consistency. Live embedding retrieval belongs to the paid evaluation track.
- Model-backed evaluation: explicit opt-in dispatch, limited concurrency and calls/tokens, protected secrets, approved corpora, stored run manifest and results. Do not enable a paid nightly schedule until its budget and value are agreed. Fork PRs receive no model credentials.

Before designing semantic graders, review at least ten varied real runs, including all failures in that sample, and then classify the initial batch. Suggested categories are hypotheses until linked to observed run IDs. Separate retrieval misses from context omission, unsupported claims, invalid citations, parsing failures, cancellation and infrastructure faults. Do not overwrite failed attempts with successful retries.

Every run records code/configuration hashes, corpus revision, model identifier, embedding/index version, context manifest, trace ID, outcome, usage availability, latency and evaluator version. Use repeat count two initially to reach 50 runs for 25 cases; use five repeats on unstable cases later. Compare per-case pass counts and failure categories, plus latency distributions and tokens. Small samples are descriptive, not a guarantee. Keep failed/cancelled/blocked runs visible and report excluded runs with reasons.

Set quality thresholds after the first labeled baseline. Security and deterministic invariants must pass from the start. A valid citation target does not prove semantic support; a later LLM judge needs a human-labeled calibration set, disagreement review, a versioned rubric and explicit limitations. Maintain a held-out set when tuning prompts or retrieval to reduce evaluation overfitting.

## Prioritized work inventory

Sizes describe relative uncertainty/effort, not promised durations. All entries below are proposed Backlog items. Within a priority, follow dependencies and the order above rather than numeric ID alone.

| ID | Work item | Priority / size | Depends on |
| --- | --- | --- | --- |
| AI-01 | Trace AI requests and indexing attempts with safe correlation and export | P1 / M | Audit |
| AI-02 | Capture selected context, generation usage and terminal outcomes | P1 / M | AI-01 |
| AI-03 | Author and validate 25 pinned evaluation cases | P1 / M | Audit |
| AI-04 | Build a bounded .NET evaluation runner and artifact manifest | P1 / M | AI-02, AI-03 |
| AI-05 | Review the first baseline and publish an observed failure taxonomy | P1 / M | AI-04 |
| AI-06 | Fix reproduced snapshot/context/citation inconsistencies | P1 / M | AI-02, AI-03; capture a baseline before each fix |
| AI-07 | Add deterministic evaluators and economical CI gates | P1 / M | AI-05 |
| AI-08 | Verify deadlines, cancellation, retries and shared usage budgets | P1 / M | AI-02; finish before tool/agent rollout |
| AI-09 | Add validated, authorized read-only tool calling | P1 / L | AI-06, AI-07, AI-08; split dispatcher and tool adapters |
| AI-10 | Compare repeated runs and detect per-case regressions | P1 / M | AI-04, AI-07; baseline before AI-09 rollout |
| AI-11 | Expose two existing tools through authenticated MCP | P2 / M | AI-09 and transport/security ADR |
| AI-12 | Add an evaluated, bounded multi-step repository investigation | P2 / L | AI-09, AI-10; split orchestration and rollout |
| AI-13 | Calibrate a semantic evidence-support judge where needed | P2 / M | AI-05, AI-10 and labeled examples |
| AI-14 | Estimate costs through versioned rate configuration and investigate waste | P2 / S | AI-02, AI-10; unknown rates remain unknown |
| AI-15 | Decide whether an isolated Python eval experiment adds value | P2 / S | Demonstrated gap in the .NET harness; optional |
| AI-16 | Publish a measured before/after reliability case study | P1 / S | A reproduced failure, implemented fix and comparable repeated runs |
| PLAT-01 | Move the .NET 9 application/toolchain to a supported LTS baseline | P1 / M | Package compatibility review; time-bound before 2026-11-10 |

The platform deadline can proceed alongside the first AI increment; record the runtime change in subsequent baselines. UI redesign #93, diagram styling #85 and guide artwork #77 stay separate P2 work. Apple/Microsoft rollout #90/#91 stays separate P1 work without blocking local AI evaluation. Do not reopen completed foundational issues.

## Reviewable releases and scope limits

Each issue ships through small compiling PRs with automated checks, manual steps, documented limitations and its board priority/size. Publish `docs/ai-observability.md` with working inspection commands when tracing ships, `docs/ai-failure-taxonomy.md` after observed runs, and `docs/ai-evaluation-strategy.md` with the actual runner and CI. Create `docs/case-study-ai-reliability.md` only when measured evidence exists, then link it prominently from the README.

This planning pass contains no runtime changes, paid evaluations, tool/MCP endpoints, scheduled jobs, new GitHub issues or production deployment. The next implementation slice is **AI-01**, with **AI-03** eligible for independent dataset authoring. No rewrite, autonomous repository writes, large multi-agent system, or Python service is required by this plan.

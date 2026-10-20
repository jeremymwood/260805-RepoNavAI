# AI request and indexing diagnostics

AI-01 / [#95](https://github.com/jeremymwood/260805-RepoNavAI/issues/95) adds metadata-only diagnostics. It does not capture prompts, retrieved content, answers, or model reasoning. Private assistant history remains a separate, authorized product feature.

## Run boundaries and correlation

Search, Answer, Orientation, CodeFlow, repository registration, retry, and re-index requests receive a new opaque `X-RepoNav-Run-Id` response header. It is exposed through CORS and is never accepted as authentication or authorization. Clients cannot choose it. JSON and SSE payloads are unchanged. A streamed response keeps its run open until generation/history handling and response writing finish; an SSE error can mean `Failed` even with HTTP 200.

Every claimed indexing attempt receives its own run ID and trace ID. The enqueue request and subsequent attempts share the existing durable indexing job GUID. Use `JobId` to join these records across processes/restarts. Attempts have an increasing `Attempt` number; the original HTTP span is not kept open and is not the worker's parent. There is no schema change. A graceful worker shutdown records `Deferred`; a killed process cannot emit a terminal record, so the next attempt and durable job state are the recovery evidence.

Application instrumentation uses BCL `ActivitySource`/`Meter`. OpenTelemetry registration/export belongs to Infrastructure and the API/Worker composition roots. Only `RepoNavAI.Repository` activities are subscribed. Existing acquisition and lease meters are preserved and exported, not recreated.

| Field | Allowed value / location |
| --- | --- |
| `reponav.run.id` / log `RunId` | New random GUID per operation; available even without listeners or when unsampled |
| Trace ID / log `RepositoryTraceId` | W3C opaque identifier; incoming trace context is diagnostic only |
| `reponav.operation` / log `Operation` | Search, Answer, Orientation, CodeFlow, Register, Reindex, Retry, Indexing |
| `reponav.outcome` / log `Outcome` | Completed, Failed, Cancelled, Rejected, Deferred, LeaseLost; stage outcomes use lowercase |
| `error.type` / log `FailureCategory` | Bounded category: cancelled, timeout, validation, access, not_found, conflict, rate_limit, provider, internal |
| `reponav.job.id`, `reponav.job.attempt` / `JobId`, `Attempt` | Existing job GUID and attempt number; nullable for interactive requests |
| Duration / log `ElapsedMilliseconds` | Stage/run elapsed time, never provider token or cost estimates |
| Resource | Service name (`RepoNavAI.Api` or `RepoNavAI.Worker`) and generated instance ID |

Search/Answer stages cover authorization, embedding, retrieval, and generation/stream lifetime. Orientation/CodeFlow have root runs and existing provider stage coverage, without changing prompts. Indexing has acquisition and embedding stages. Detailed context selection, token usage, model configuration, provider retry counts, and first-delta latency belong to AI-02; their absence must not be interpreted as zero.

New metrics: `reponav.repository.runs`, `reponav.repository.duration` (seconds), and `reponav.repository.stage.duration` (seconds). Dimensions are only operation, outcome, and stage where applicable. IDs, file names, tenant names, and arbitrary exception types are not metric labels.

## Privacy boundary

Do not add automatic HTTP, SQL, ASP.NET, or Semantic Kernel tracing, exception events, HTTP body logging, EF sensitive-data logging, or wildcard ActivitySource subscriptions. The current exporter intentionally excludes these sources. Traces contain only the fields above; no query strings, URLs, source paths/bodies, messages, credentials, email addresses, user IDs, or tenant/repository names are added.

During an instrumented run, Serilog accepts only the typed run-summary logger and strips other ambient properties. Raw exceptions and third-party/request logs are suppressed, including when tracing export is disabled. Framework hosting/routing logs are also excluded because they can run outside the middleware boundary and contain URL/query/route values. Indexing polling failures log a category, not an exception body. Normal logs outside these boundaries are not a general-purpose data-loss-prevention system.

The web proxy's API access records contain only method, status, duration, upstream status, and returned run ID. API-location Nginx error logs are disabled because their raw request details can contain search text; use access status plus the API run summary. Keep the same rule at any production ingress/APM layer. That external configuration is not changed by this work item.

## Local startup and inspection

Prerequisites: Docker Desktop's engine running; existing `.env` configured; an indexed public fixture such as this repository. No additional secret or account is required. Commands run from the repository root in PowerShell. Do not overwrite an existing `.env`.

Normal startup (export disabled by default):

```powershell
docker compose up --build -d
docker compose ps
```

Open `http://localhost:5173` and check `http://localhost:5173/health`. To inspect traces and metrics, use the opt-in override:

```powershell
docker compose -f docker-compose.yml -f docker-compose.observability.yml up --build -d
docker compose -f docker-compose.yml -f docker-compose.observability.yml ps
docker compose -f docker-compose.yml -f docker-compose.observability.yml logs --since 5m -f otel-collector
```

The pinned local collector receives OTLP/HTTP inside the Compose network, with no published ports. Its debug exporter writes readable spans and metrics to Docker logs, not an external vendor. Trace export normally takes a few seconds; metrics export every ten seconds. Press Ctrl+C to leave log following; this does not stop containers.

To inspect one request:

1. Open browser developer tools, Network, and submit a new Search or Answer. Use an explicit mode, not a reopened history entry.
2. Select the `semantic-search` or `chat` request. Copy `X-RepoNav-Run-Id` from response headers.
3. Set `$runId` to that opaque value, then run:

```powershell
$runId = 'paste-the-run-id-here'
docker compose logs --since 10m api worker | Select-String -SimpleMatch $runId
docker compose -f docker-compose.yml -f docker-compose.observability.yml logs --since 10m otel-collector | Select-String -SimpleMatch $runId -Context 12,12
```

The API summary and spans must agree on the run ID. Child spans share the root's trace ID; root outcome is `Completed` for success. For indexing, the `Register`/`Reindex` summary's `JobId` matches the worker summary's `JobId`, while run IDs differ. A response header does not imply that a trace was exported.

Collector outage check (keep using both Compose files):

```powershell
docker compose -f docker-compose.yml -f docker-compose.observability.yml stop otel-collector
# Run a new Search and Answer; both must still work.
docker compose -f docker-compose.yml -f docker-compose.observability.yml start otel-collector
```

To disable export again, recreate the API/worker with the base configuration, then restart the web proxy. The proxy resolves its API address at startup; recreating containers can change that address and otherwise leave the proxy returning 404/502 responses even when the API itself is healthy. Restarting `web` refreshes that address without rebuilding or re-indexing. The final command removes only the stopped collector container and its local diagnostic logs; it does not remove database volumes:

```powershell
docker compose -f docker-compose.yml -f docker-compose.observability.yml stop otel-collector
docker compose -f docker-compose.yml up -d --force-recreate api worker
docker compose -f docker-compose.yml restart web
docker compose -f docker-compose.yml -f docker-compose.observability.yml rm -f otel-collector
```

## Export policy and operations

`Observability:OtlpEndpoint` is empty by default. For a non-Compose host, set `Observability__OtlpEndpoint` to a trusted collector HTTP(S) origin, without credentials, query, fragment, or path. The application appends `/v1/traces` and `/v1/metrics`. No generic OTLP authorization headers are used. Use a private collector and TLS outside the isolated local Compose network; arrange outbound authentication downstream, not in a URL.

Opt-in export currently samples all application-owned spans. It deliberately ignores an incoming unsampled flag for these spans; run IDs exist independently of sampling. Do not enable high-volume production export until the operator has reviewed capacity and collection policy. This change does not deploy a collector or modify Azure retention.

Trace export is asynchronous with a 512-span queue, batches of at most 64, a one-second schedule, a two-second processor deadline, and a 1.5-second exporter timeout. Metrics use a ten-second interval and two-second reader timeout. Export failures/queue overflow can drop diagnostics, but do not fail or retry an application request. Shutdown is best effort and bounded; there is no durable telemetry queue.

The local collector has a 256 MiB container limit, a memory limiter, and rotating Docker logs (three files of up to 10 MB). It is a local debugging tool, not a production backend. Do not share its network with untrusted containers. On shared/production collectors restrict read access to operators, encrypt transport/storage, and apply a maximum seven-day diagnostic retention unless a reviewed incident policy requires less/more. Those retention/access policies must be configured in the chosen backend; they are not enforced by application code. Run IDs and job IDs are pseudonymous, not public audit data.

## Verification

No paid provider is used by the automated checks:

```powershell
dotnet test RepoNavAI.sln
node --test scripts/validate-prose.test.mjs
node scripts/validate-prose.mjs
node scripts/validate-docs.mjs
```

`RepositoryTelemetryTests` covers disabled/unsampled export, nested scopes, parent-child correlation, cancelled streams, worker-attempt correlation metadata, bounded metric labels, provider canaries, log/ambient-scope sanitization, rejected tenant access, HTTP-200 stream failures, and an unavailable exporter. Integration providers are fakes; these tests do not prove real model quality or PostgreSQL worker restart behavior. Complete the [AI-01 manual checklist](testing/manual-acceptance.md#ai-01-privacy-safe-request-and-indexing-diagnostics-95) before merge.

Implementation verification (2026-10-15): solution build passed with zero warnings; 155 backend tests passed, including 19 telemetry cases; documentation checks passed; API, worker, and web container builds passed; collector configuration validation and `nginx -t` passed. An isolated Docker API returned 401 with a run ID, exported a matching `Rejected` root span without the fake query canary, and still responded with the collector stopped. Temporary smoke-test containers were removed without touching database volumes. Live authenticated provider calls, browser cancellation, and durable worker restart acceptance remain manual checks.

Acceptance follow-up: a legacy snapshot with documents but no semantic chunks exposed an unchanged-commit re-indexing bug. The [backfill repair and regression procedure](testing/indexing-backfill.md) covers that case without removing existing snapshots. With the isolated PostgreSQL fixture enabled, the expanded backend suite passes 164 tests; the three database cases require that fixture locally and run automatically in CI.

### Completed acceptance, 2026-10-19

The earlier implementation-only verification above is superseded by these local acceptance results. The user initiated live provider requests; automated checks used fakes and an isolated test database. Run/job identifiers and raw logs are not published here.

| Check | Verified result |
| --- | --- |
| Search, Answer, Orientation and CodeFlow | UI accepted; matching API completion summaries and correctly correlated successful root/child spans |
| Stop during Answer | Controls restored, partial output retained, no automatic restart; API run recorded Cancelled |
| Signed-out requests | All four modes returned 401 with opaque run IDs and Rejected root spans; no provider-stage spans |
| Privacy canary | The authenticated Search marker was absent from API, worker, web and collector logs; this is a scoped check, not a general privacy audit |
| Worker restart during indexing | Attempt 1 recorded Deferred; attempt 2 reclaimed the same job after approximately 45 seconds, used a new run ID and completed |
| Collector outage and restoration | Search and Answer completed while the collector was stopped; a fresh Answer exported all expected spans after restart |
| Export disabled | API/worker endpoint settings were empty, collector stopped, health returned 200 and a fresh Search completed with a run ID and safe log summary |
| Legacy snapshot and status refresh | Search returned source matches after re-index; automatic workspace refresh and related UI checks were accepted |

Automated verification: 164 backend tests passed with the isolated PostgreSQL fixture, including 19 telemetry cases. After compatible dependency security updates, 77 frontend unit tests, nine workspace browser tests, five responsive/accessibility/navigation checks and six synthetic assistant checks passed. Lint, production and Docker frontend builds, workflow validation and documentation checks passed. Generated CSS was unchanged. Linux screenshot comparisons remain a CI check and were not run on the Windows host. No model-quality benchmark or production deployment is claimed.

The production npm audit reports zero advisories. The full audit still reports five high and two moderate development-tool findings; the separately approved [Tailwind migration (#96)](https://github.com/jeremymwood/260805-RepoNavAI/issues/96) is P1 / M / Backlog. The user accepted the final frontend smoke check. Pull-request review and CI are still required before merging #95.

Implementation references: [OpenTelemetry .NET instrumentation](https://opentelemetry.io/docs/languages/dotnet/instrumentation/), [exporters](https://opentelemetry.io/docs/languages/dotnet/exporters/), [collector configuration](https://opentelemetry.io/docs/collector/configuration/), and the [pinned collector release](https://github.com/open-telemetry/opentelemetry-collector-releases/releases/tag/v0.162.0).

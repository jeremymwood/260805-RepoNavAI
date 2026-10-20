# Manual acceptance runbook

Use these checks after the automated suite passes and the local Docker stack is healthy. Never paste credentials, API keys, private source, or complete model prompts into test reports.

## Prerequisites

- Run `docker compose up --build -d` when images or configuration changed.
- Confirm `http://localhost:5173/health` returns `Healthy`.
- Sign in and select an organization containing a completed, embedded repository index.

## Frontend dependency security maintenance

From `src/RepoNavAI.Web`, run these commands individually and check each exit status:

```powershell
npm ci
npm run audit:production
npm audit --audit-level=high
npm run lint
npm test
npm run build
npm run test:e2e:workspace
```

Pass criteria: a reproducible install, no production dependency advisories, and passing lint, unit tests, build and workspace browser tests. The workspace tests intercept API requests and do not use live AI credits. A registry/network error is an incomplete audit, not a pass.

Status on 2026-10-19: Axios was updated to 1.20.0, Vitest to 4.1.12, and compatible transitive fixes were applied for brace-expansion, js-yaml, nanoid and source-map-js. The production audit reports zero vulnerabilities and is now a CI gate. The full audit still fails with seven development dependency findings (five high, two moderate) in the Tailwind 3 dependency tree; this is not a clean full audit. The separately approved [Tailwind migration, P1 / M (#96)](https://github.com/jeremymwood/260805-RepoNavAI/issues/96) tracks removing those chains. Do not use force upgrades, mismatched dependency overrides, or audit suppression to hide them. Keep build inputs reviewed and development servers private while that work remains open.

Run `npm run test:e2e` in the CI Linux Playwright environment for screenshot comparisons. On Windows, `npm run test:e2e -- --ignore-snapshots` checks responsive containment, accessibility and navigation only; it does not validate the Linux screenshot baselines. Do not replace baselines merely to make a dependency update pass.

For a local smoke check, return to the repository root. With the API and worker already healthy, rebuild only the frontend:

```powershell
docker compose up -d --build --no-deps web
```

1. Open `http://localhost:5173/health`. Pass: `Healthy` is returned.
2. Reload `http://localhost:5173`, sign in if needed, and open an existing repository. Pass: organizations, repository details and permitted analysis tools load without new console or failed network errors.
3. Check light/dark themes and desktop/mobile widths. Pass: cards, controls, source previews and navigation retain their layout with no page-wide horizontal overflow.
4. Open an existing saved assistant result if available. Pass: saved content and citations load without submitting a new provider request. No re-index or live AI call is required for this dependency smoke check.

## Semantic repository search

1. Search for a repository-specific concept such as `How are repository indexing jobs processed?`.
2. Confirm ranked results relate to the meaning of the question rather than only exact wording.
3. Confirm every result includes a file path, line range, relevance score, and source link pinned to the indexed commit.
4. Confirm method and authorization filters continue to work in the adjacent API endpoint catalog.
5. Use a nonsensical or unsupported query and confirm the interface returns no/weak evidence without failing.
6. Confirm long paths and code remain inside the result card; code scrolls within its own preview.

## Indexing lease recovery

1. Register or retry a repository large enough to remain in **Acquiring**, **Parsing**, or **Persisting** for more than 60 seconds.
2. Confirm the request remains **Processing** for more than 45 seconds without returning to **Pending**; this verifies active heartbeats keep extending ownership.
3. While it is still processing, restart only the indexing worker with `docker compose restart worker` and start a timer. Restarting the API does not exercise worker recovery.
4. Keep the workspace open and confirm the worker reclaims the request after its lease expires (normally about 45 seconds, plus startup/polling time). Full indexing completion takes additional time.
5. Confirm the request reaches **Completed** with one snapshot for the displayed commit SHA and without a competing-worker or duplicate-write failure.
6. Repeat with **Cancel** during processing and confirm cancellation remains responsive and the request is not reclaimed.
7. Review sanitized worker run summaries for the same `JobId`, a new `RunId`, and an increased `Attempt`. Confirm no credential or source content appears.

## Streaming repository chat

1. Ask `How does repository indexing process source files?`.
2. Confirm text appears progressively and the interface remains responsive.
3. Confirm material claims use numbered references and the Sources list links to the relevant commit and line range.
4. Ask `How is organization authorization enforced?` and confirm the answer is repository-specific rather than generic framework advice.
5. Ask about functionality absent from the repository and confirm the answer identifies insufficient evidence instead of inventing behavior.
6. Start a longer answer, select **Stop**, and confirm:
   - text stops promptly;
   - the response does not restart;
   - a stopped confirmation appears;
   - the partial answer remains visible;
   - another question can be submitted immediately.
7. Confirm long citations and answer text remain contained at supported desktop and mobile widths.
8. Restart the Docker stack, reload the application, and confirm chat still works after migrations and service recovery.

## Repository workspace automatic status refresh

Rebuild with `docker compose up --build -d` and reload the browser once to load the updated frontend. If testing AI-01 telemetry as well, use `docker compose -f docker-compose.yml -f docker-compose.observability.yml up --build -d` instead to retain the collector configuration. Re-indexing uses the configured embedding provider and may incur charges; polling itself only reads repository metadata.

1. Open a completed repository, choose **Analysis summary**, and select **Re-index this repository** once. Leave the workspace open and visible; do not reload or return to the overview.
2. While indexing is Pending/Processing, confirm the message says status refreshes automatically. Pass: the workspace returns to its tools when indexing completes, normally within three seconds of the server reporting completion. A very fast job may complete before the intermediate statuses appear.
3. Confirm **Source search**, **Architecture map**, and **API endpoints** reflect the new capabilities where supported. Existing endpoint/architecture views must show fresh data after re-indexing even when the commit did not change. Run a new Search and confirm source results appear.
4. In DevTools Network, filter requests to the repository collection (`/organizations/{id}/repositories?page=1&pageSize=50`). Pass: requests repeat about every three seconds while the selected repository is active, then stop after Completed, Failed, or Cancelled. Separate `/health` requests still run for connection monitoring.
5. If a job fails or is cancelled, keep its workspace open. Pass: polling stops, no tools are unlocked, and the message offers a return to the overview to retry. Do not deliberately exhaust provider credit to test failure; mocked browser regressions cover these transitions.
6. While a repository is processing, navigate to **Profile settings**. Pass: workspace polling stops. The overview has its own polling and is not a suitable destination for this check. Background tabs pause polling; returning to a visible workspace resumes it.

Automated checks: from `src/RepoNavAI.Web`, run `npm test`, `npm run lint`, `npm run build`, and `npm run test:e2e:workspace`. Install the test browser with `npx playwright install chromium` if needed. The workspace browser suite uses intercepted API responses, not the public Pages demo or a live API, and makes no provider calls. It also covers unchanged-commit cache refresh, jobs that finish before Pending is observed, unrelated repositories, and recovery from a temporary status API outage.

## Repository removal

1. As an organization member, confirm repository cards do not expose a removal action and a direct DELETE request returns forbidden.
2. As an owner or administrator, open removal for a disposable test repository and confirm focus moves into the labelled dialog.
3. Press Escape and click outside the dialog. Confirm neither action dismisses the warning unexpectedly.
4. Enter a different repository name and confirm **Remove repository** remains disabled.
5. Enter the displayed `owner/name`, submit, and confirm controls remain disabled while removal is pending.
6. Confirm the repository disappears, success is announced, and focus returns safely to the repository area.
7. Confirm the source GitHub repository remains unchanged.
8. Inspect the local database using the read-only workflow. Confirm indexing requests, snapshots, documents, symbols, chunks, endpoints, chat metadata, orientation plans, and favorites for the repository are absent, while one metadata-only removal audit remains.
9. Register the same GitHub URL again and confirm a new pending indexing request is created.
10. Repeat while indexing is active. Confirm the worker stops and no snapshot or other derived row appears after removal commits.

## Repository assistant history

1. Run one Search, Answer, Orientation, and Code flow request against a completed repository index. Refresh and sign in again; confirm each completed result appears under **Your recent results**.
2. Reopen each mode while monitoring provider calls. Confirm no embedding, retrieval, or chat-provider request occurs and the stored result retains its original commit-pinned citations.
3. Confirm reopened Search and Code flow history do not expose stored retrieved source bodies. Live results may show evidence previews, while saved contracts retain only validated result fields and citation metadata.
4. Star two entries and confirm starred results precede ordinary history with newest-first deterministic ordering. Unstar, rename, and paginate; refresh and confirm each change persists.
5. Re-index the repository at a new commit. Confirm older results display an older-index warning while their original source links remain unchanged.
6. Sign in as another member and confirm the first member's history is absent. Remove membership and confirm the former member cannot list or reopen history.
7. Delete one entry, then clear the remaining history using the exact `CLEAR` confirmation. Confirm neither operation changes orientation plans or source repositories and that deleted history cannot be recovered.
8. Lower retention, entry-count, or result-size limits in a disposable local environment. Confirm expired and oldest entries are pruned, oversized results are not stored, and the live assistant result remains usable.
9. Remove the repository and use the read-only database workflow to confirm its assistant history rows are absent.

## AI-01: Privacy-safe request and indexing diagnostics (#95)

Start with the two-file Compose command in [AI observability](../ai-observability.md#local-startup-and-inspection). Use a public indexed fixture; live assistant/re-index calls use your configured provider balance. There is no new UI to enable.

If a completed legacy repository returns `[]` from a new Search, follow the [legacy snapshot backfill check](indexing-backfill.md#run-the-app-and-test-the-repair) first. A run header on an empty result proves header propagation, not successful source retrieval.

1. **Startup:** Open `/health` and the application. Pass: healthy response, login works, existing repository list is intact. No database reset or new secret is necessary.
2. **Search/Answer:** Explicitly run Search and Answer with `Where is organization authorization enforced?`. Pass: results/citations stream as before; each network response has a different `X-RepoNav-Run-Id`; its API completion summary and collector root span agree. Find authorization, embedding, retrieval, and Answer generation children on the same trace.
3. **Other modes:** Run Orientation and Code flow with known working fixture prompts. Pass: existing result/diagram behavior is unchanged, each request has a run ID and `Completed` summary. Failed providers must produce `Failed`, not `Completed`.
4. **Stop:** Start a sufficiently long Answer and click Stop before it finishes. Pass: input becomes available, output does not restart, the API run becomes `Cancelled` and the generation span ends. A response that already finished may legitimately remain `Completed`.
5. **Denied request:** Copy the Search request URL from Network, sign out, and navigate to that URL. Pass: 401 (or the existing login challenge), a diagnostic header, `Rejected` summary, and no embedding/retrieval/generation children. Automated tests separately cover cross-tenant 404. Sign back in afterward.
6. **Privacy:** Submit a Search containing the harmless fake marker `REPO-OBS-CANARY-95`. Pass: neither API/worker/web logs nor collector output contain the marker, prompts, source bodies, email, Authorization header, or credentials. Search each local log with `Select-String -SimpleMatch 'REPO-OBS-CANARY-95'`; expect no match. Never use a real secret for this check. The marker may appear in the browser request or private assistant history; those are not telemetry.
7. **Indexing recovery:** Re-index a disposable public repository. Capture `JobId` from the API completion summary. While Processing, run `docker compose restart worker` (not just API). Pass: a later worker attempt has the same `JobId`, a new run ID and increased attempt number, and eventually completes within existing retry/lease limits. The previous graceful attempt is `Deferred`; an abrupt kill may have no final span. If the job finishes before restart, repeat with a larger fixture within configured limits. Do not increase limits or reset data for this test.
8. **Collector outage:** Stop only `otel-collector` using the two-file command. Run another Search and Answer. Pass: both work without waiting on telemetry; headers and safe API summaries remain available. Restart the collector and run a fresh request; new spans arrive. Exported records lost during the outage need not be replayed.
9. **Default mode:** Follow the guide's disable-export commands, then run Search again. Pass: normal application behavior and run header remain, with no collector required.

Record test numbers and outcomes. Trace inspection is an operator workflow through local logs, not a new customer-facing trace viewer.

## Recording results

Record the commit SHA, browser, test date, and pass/fail outcome in the pull request. Include only sanitized error messages or screenshots. Any failed check blocks merge until resolved or explicitly moved to a follow-up issue with an accepted risk.

# Legacy snapshot search backfill

This repair was found during AI-01 / #95 acceptance: a completed legacy index contained documents but no semantic chunks. Search returned HTTP 200 with `[]` even though the tracing header worked. Re-indexing the same commit previously reused that snapshot without generating the missing chunks.

## Repair behavior

Re-indexing now loads the existing snapshot's documents and chunks within the organization boundary. It generates chunks from the stored document content for documents with no chunks, saves them, and runs the existing embedding pipeline. New snapshots use the same chunk builder. No database migration, direct data patch, deletion, or re-registration is required.

Existing snapshots, documents, endpoints, and chunk identities are retained. Already-chunked documents are not re-chunked, so historical chunk boundaries are not mixed with a newer algorithm. A future versioned re-chunking migration must handle corrupt/partially chunked documents and algorithm changes separately. Missing vectors can be filled by retrying the existing embedding stage; normal re-index behavior still embeds all chunks and can incur provider charges.

Saving chunks before provider calls ensures vector upserts can find them. If a later embedding batch fails, the next attempt reuses persisted chunk IDs. The existing unique snapshot/chunk constraints remain in force. An absent OpenAI configuration still means no embeddings are generated; this fix does not change that configuration policy.

## Run the app and test the repair

From the repository root, with Docker Desktop running and the existing `.env` unchanged:

```powershell
docker compose -f docker-compose.yml -f docker-compose.observability.yml up --build -d
```

1. Open `http://localhost:5173` and confirm `/health` is healthy. Reload once after rebuilding to load the latest frontend.
2. Open the affected completed repository with **Explore repository**. Select **Analysis summary**, record the commit SHA, and click **Re-index this repository**. This uses the configured embedding provider and balance; do not remove/re-register the repository.
3. Leave the workspace open without refreshing or repeatedly re-indexing. Pending/Processing status refreshes automatically every three seconds while the page is visible. Pass: the workspace reopens when status reaches Completed, analysis capabilities refresh, and the repository still exists under the same registration without a provider/indexing error. See the [automatic refresh checks](manual-acceptance.md#repository-workspace-automatic-status-refresh) for the complete UI regression procedure.
4. Open **Repository assistant**, choose **Search** in the Mode dropdown, enter a repository-specific question, and click **Ask**. For a kanban application, try `How are tasks displayed and updated on the board?`.
5. Pass: the new `semantic-search` response is a nonempty array, source cards contain file/line citations, and `X-RepoNav-Run-Id` is present. Reopening the old saved empty result is not a new search.
6. If the commit SHA stayed the same, this verifies the legacy backfill path. If upstream advanced to a new commit, it verifies normal indexing/search instead; the automated PostgreSQL regression checks cover the unchanged-commit case.

After this passes, continue with [AI-01 acceptance checks 2-9](manual-acceptance.md#ai-01-privacy-safe-request-and-indexing-diagnostics-95). The original screenshot already verified the response header, but not successful retrieval, streaming cancellation, or worker recovery. Use a repository with working source matches for those checks.

## Automated regression checks

Six provider-independent tests cover chunk generation/hashes/line ranges, stable IDs on repeat, mixed legacy/current documents, blank documents, cancellation, and EF insert tracking. Three real PostgreSQL tests cover persisted vectors and search, interrupted embedding batches followed by retries, and the new-snapshot path. All provider calls in these tests are deterministic fakes.

CI starts an isolated pgvector service and supplies `REPONAV_TEST_POSTGRES` for the backend suite. Without that variable, the three database tests explicitly report Skipped rather than silently substituting an in-memory database.

To run all checks locally, use a disposable test container, not the application's PostgreSQL service or `.env`. The credentials below are intentionally test-only. The fixture rejects non-loopback hosts and database names other than `reponav_backfill_tests`.

```powershell
docker run -d --name reponav-backfill-tests --label reponav.purpose=backfill-regression --tmpfs /var/lib/postgresql/data -p 127.0.0.1:55495:5432 -e POSTGRES_DB=reponav_backfill_tests -e POSTGRES_USER=reponav_test -e POSTGRES_PASSWORD=test-only-password --health-cmd "pg_isready -U reponav_test -d reponav_backfill_tests" --health-interval 1s --health-timeout 3s --health-retries 20 pgvector/pgvector:pg17
docker inspect reponav-backfill-tests --format '{{.State.Health.Status}}'
```

Wait until the health status is `healthy`. If that name or port is already in use, inspect the existing resource before taking action; never remove an unknown container to make this command work.

```powershell
$previousTestConnection = $env:REPONAV_TEST_POSTGRES
try {
    $env:REPONAV_TEST_POSTGRES = 'Host=127.0.0.1;Port=55495;Database=reponav_backfill_tests;Username=reponav_test;Password=test-only-password'
    dotnet test RepoNavAI.sln
} finally {
    $env:REPONAV_TEST_POSTGRES = $previousTestConnection
}
```

Fixture rows are transactionally rolled back. After verifying that the following inspection shows `reponav.purpose` set to `backfill-regression`, stop and remove only the test container created above. Its disposable database is not retained; the application database is untouched.

```powershell
docker inspect reponav-backfill-tests --format '{{json .Config.Labels}}'
docker stop reponav-backfill-tests
docker rm reponav-backfill-tests
```

Implementation verification (2026-10-15): 164 backend tests passed with the isolated PostgreSQL fixture enabled, including all nine new backfill cases. No live repository was re-indexed and no paid embedding call was made during automated verification.

# Manual acceptance runbook

Use these checks after the automated suite passes and the local Docker stack is healthy. Never paste credentials, API keys, private source, or complete model prompts into test reports.

## Prerequisites

- Run `docker compose up --build -d` when images or configuration changed.
- Confirm `http://localhost:5173/health` returns `Healthy`.
- Sign in and select an organization containing a completed, embedded repository index.

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
3. While it is still processing, restart only the API with `docker compose restart api` and start a timer.
4. Refresh the repository status periodically and confirm another worker reclaims the request and processing resumes within 60 seconds.
5. Confirm the request reaches **Completed** with one snapshot for the displayed commit SHA and without a competing-worker or duplicate-write failure.
6. Repeat with **Cancel** during processing and confirm cancellation remains responsive and the request is not reclaimed.
7. Review sanitized API logs for a reclaimed-job entry and confirm no credential or source content appears.

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

## Recording results

Record the commit SHA, browser, test date, and pass/fail outcome in the pull request. Include only sanitized error messages or screenshots. Any failed check blocks merge until resolved or explicitly moved to a follow-up issue with an accepted risk.

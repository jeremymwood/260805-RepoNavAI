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

## Recording results

Record the commit SHA, browser, test date, and pass/fail outcome in the pull request. Include only sanitized error messages or screenshots. Any failed check blocks merge until resolved or explicitly moved to a follow-up issue with an accepted risk.

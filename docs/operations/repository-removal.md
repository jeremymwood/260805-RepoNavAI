# Repository removal

Repository removal deregisters a repository and permanently deletes its RepoNavAI-managed analysis data. It never sends a delete request to GitHub and does not modify the source repository.

## Authorization and confirmation

Only organization owners and administrators may remove a repository. Members receive a forbidden response, while users outside the organization receive the same not-found response used for other tenant-scoped resources.

The UI requires the actor to type the repository's complete `owner/name` identity. The API independently validates that confirmation after locking the tenant-scoped repository row. A stale or incorrect identity fails before any data changes.

## Transaction and worker fencing

Removal uses one database transaction:

1. Lock the repository row by organization and repository identifier.
2. Request cancellation for pending and processing indexing jobs.
3. Insert a metadata-only removal audit record.
4. Delete the registered repository and commit its configured cascades.

The repository foreign key is the final write fence. An active worker that already holds a lease cannot insert a new snapshot after the repository row is deleted. Lease renewal also fails after the indexing request is cascade-deleted, which cancels worker processing. A failed or repeated removal cannot affect another tenant because both the lock and delete are scoped by organization.

## Deleted data

The existing cascade model removes repository favorites, indexing requests, snapshots, documents, symbols, chunks and vectors, endpoints, chat-session metadata, orientation plans, and private assistant history. The audit row deliberately has no foreign key to the removed repository, organization, or actor so later lifecycle cleanup cannot erase the record accidentally.

The audit retains only repository and actor identifiers, provider, owner, name, and removal time. It does not retain source content, prompts, answers, tokens, credentials, local paths, or provider diagnostics.

## Recovery and re-registration

Confirmed removal is not recoverable inside RepoNavAI. Recovery means registering the source GitHub repository again and producing a new index. Re-registration is available as soon as the removal transaction commits because the organization-level provider/name uniqueness row is gone.

If removal fails, the transaction rolls back the audit and deletion together. The UI keeps the dialog open, preserves the confirmation, and displays an actionable error so the administrator can retry safely.

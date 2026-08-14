# Repository assistant history

Repository assistant history is a private, per-user record of validated Search, Answer, Orientation, and Code flow results. It lets a member reopen a completed result without retrieval, embedding, or chat-provider calls.

## Stored envelope

Each entry stores the organization, repository, user, server-resolved mode, original user prompt, bounded display title, indexed commit, status, timestamps, star state, and result schema version. Result contracts are created only by application handlers after validating live results.

Search history stores trusted citation metadata without retrieved source text. Answer history stores the completed answer and trusted citation metadata. Code flow history stores the validated trace, steps, and citations without the retrieved evidence bodies supplied to the model. Orientation history links to the existing orientation plan instead of copying its content.

History never stores embedding inputs or vectors, raw retrieved chunks, provider request payloads, system prompts, secrets, credentials, local paths, or provider diagnostics. Repository evidence and user prompts remain untrusted data and cannot set ownership, mode, commit, schema version, citation metadata, retention, or star state.

## Privacy and authorization

Every read and write is scoped by organization, repository, and the current user. Membership in the organization and access to the repository are checked before listing or reopening history. Organization administrators do not receive an endpoint for viewing another member's prompts or saved results.

Losing organization membership immediately removes API access without exposing or reassigning private history. Deleting a user, organization, or repository cascades its history. Removing a repository therefore deletes assistant history in the same transaction as other RepoNavAI-managed repository data. Deleting an orientation plan clears the optional link while preserving the history envelope's metadata.

## Ordering, retention, and limits

Starred entries appear before ordinary recent history. Each group is ordered by creation time and identifier in descending order for deterministic pagination.

Configuration controls retention days, maximum entries per user and repository, maximum serialized result bytes, and maximum organization stored characters. Expired entries are pruned during history reads and writes. When the per-user repository count is full, the oldest unstarred entry is removed first, followed by the oldest starred entry only when necessary. Results that exceed storage limits are marked failed and are not persisted, while the live assistant result remains usable.

Operators manage retention settings without inspecting prompt content. Ordinary logs must include only entry identifiers, counts, modes, statuses, and schema versions.

## Compatibility and staleness

Each mode has an independent result schema version. A supported historical contract renders without provider calls. An unknown version remains visible as metadata with an unsupported-format label and does not deserialize into a live result contract.

History records the commit used for the original request. A result becomes stale when the latest indexed commit changes. The UI keeps its original commit-pinned source links and clearly labels the result as using an older index.

## Deletion and recovery

Individual deletion and clear history are permanent. Clear history requires the exact confirmation `CLEAR`. Deleted entries are not recoverable inside RepoNavAI; users can run the assistant request again to create a new result against the current index.

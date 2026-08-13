# PostgreSQL database inspection

Use this workflow to inspect RepoNavAI data without exposing PostgreSQL on a host port, copying a password into shell history, or accidentally connecting the helper to production.

## Supported tools

`psql` inside the existing `pgvector/pgvector:pg17` Compose container is the supported default for Windows and macOS. It requires only the repository's existing Docker and Node.js prerequisites, uses the container's `POSTGRES_USER` and `POSTGRES_DB` environment variables, and understands PostgreSQL and pgvector types.

pgAdmin, DBeaver, JetBrains database tools, and the PostgreSQL extension for Visual Studio Code are optional graphical clients. They require a separately reviewed local port mapping and must be configured read-only. Do not add a PostgreSQL port to committed Compose configuration merely for inspection.

SSMS 20 is not selected. Microsoft's SSMS feature documentation targets SQL Server and Azure SQL rather than PostgreSQL. A familiar query editor does not compensate for missing native PostgreSQL schema, extension, and pgvector support.

## Local read-only access

Start the local stack, then run this command from the repository root in PowerShell, Terminal, or any other shell with Node.js and Docker available:

```text
node scripts/db-inspect.mjs
```

The helper has no host, connection-string, username, or password arguments. It always targets the `postgres` service in this repository's `docker-compose.yml` and sets `default_transaction_read_only=on`. It therefore cannot be redirected to staging or production. The local Compose database user owns the development database, but PostgreSQL rejects writes in this inspection session.

Useful `psql` commands:

```text
\conninfo
SHOW default_transaction_read_only;
\dn
\dt reponav.*
\d+ reponav."RepositoryChunks"
\dx vector
\q
```

Verify the connection, read-only setting, and pgvector extension non-interactively:

```text
node scripts/db-inspect.mjs --check
```

If Compose is not running, use `docker compose up -d postgres migrator`. If the helper cannot find Docker, confirm `docker version` and `docker compose version`. On macOS, ensure Docker Desktop is running. On Windows, use Linux containers and run the command from the repository directory containing `.env`.

## Tenant-aware diagnostic queries

RepoNavAI's authorization boundary is the organization. Select an organization deliberately, copy only its ID, and apply that value to every diagnostic query. Never run broad content exports or include query results in tickets or logs.

```sql
SELECT "Id", "Name", "Slug"
FROM reponav."Organizations"
ORDER BY "Name";

\set organization_id '00000000-0000-0000-0000-000000000000'

SELECT "Id", "Owner", "Name", "DefaultBranch", "Visibility", "CreatedAtUtc"
FROM reponav."RegisteredRepositories"
WHERE "OrganizationId" = :'organization_id'
ORDER BY "CreatedAtUtc" DESC;

SELECT r."Owner", r."Name", i."Status", i."Checkpoint", i."AttemptCount",
       i."CreatedAtUtc", i."CompletedAtUtc", i."ErrorCode"
FROM reponav."RepositoryIndexingRequests" i
JOIN reponav."RegisteredRepositories" r ON r."Id" = i."RepositoryId"
WHERE i."OrganizationId" = :'organization_id'
ORDER BY i."CreatedAtUtc" DESC
LIMIT 50;

SELECT r."Owner", r."Name", s."CommitSha",
       COUNT(DISTINCT d."Id") AS documents,
       COUNT(DISTINCT e."Id") AS endpoints,
       COUNT(DISTINCT c."Id") AS semantic_chunks
FROM reponav."RegisteredRepositories" r
JOIN reponav."RepositorySnapshots" s ON s."RepositoryId" = r."Id"
LEFT JOIN reponav."RepositoryDocuments" d ON d."SnapshotId" = s."Id"
LEFT JOIN reponav."RepositoryEndpoints" e ON e."SnapshotId" = s."Id"
LEFT JOIN reponav."RepositoryChunks" c ON c."SnapshotId" = s."Id"
WHERE r."OrganizationId" = :'organization_id'
GROUP BY r."Owner", r."Name", s."CommitSha"
ORDER BY r."Owner", r."Name";

SELECT r."Owner", r."Name", c."Status", c."Model", c."CreatedAtUtc"
FROM reponav."RepositoryChatSessions" c
JOIN reponav."RegisteredRepositories" r ON r."Id" = c."RepositoryId"
WHERE c."OrganizationId" = :'organization_id'
ORDER BY c."CreatedAtUtc" DESC
LIMIT 50;
```

The queries intentionally omit passwords, token hashes, source document content, semantic chunk content, embeddings, prompts, and generated answers. Inspect those only under an approved incident or debugging need and never paste them into ordinary telemetry.

## Azure staging and production

The local helper must never be adapted for Azure access. Azure Database for PostgreSQL uses private networking, so an operator connects from an approved private-network workstation, bastion, or time-bounded administrative job. Production requires a change or incident record, named approver, ticketed time window, and a dedicated database role with only `CONNECT`, schema `USAGE`, and required `SELECT` grants. Do not use the application, migration, or server-administrator identity.

Source the username and endpoint from approved deployment outputs and obtain the password from Key Vault through an audited, time-bounded process. Prompt for the password with `psql`; do not put it in a command, connection URI, environment file, shell history, transcript, or ticket. Require TLS certificate and hostname validation with `sslmode=verify-full` and the approved Azure root CA bundle. Confirm `\conninfo`, `SHOW transaction_read_only;`, the server name, database, and ticket scope before querying.

Azure access must remain least privilege, encrypted, attributable, and revocable. Close the session when the approved window ends and retain the access audit, not query results containing tenant data.

## References

- [PostgreSQL client connection defaults](https://www.postgresql.org/docs/17/runtime-config-client.html)
- [Azure Database for PostgreSQL TLS connections](https://learn.microsoft.com/azure/postgresql/security/security-tls-how-to-connect)
- [SQL Server Management Studio components and features](https://learn.microsoft.com/ssms/components-features)

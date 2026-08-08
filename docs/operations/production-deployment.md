# Production deployment strategy

This runbook turns [ADR-008](../architecture/ADR-008-production-hosting.md) into environment, promotion, recovery, and GitHub policy. It is the contract for the follow-up infrastructure and continuous-delivery work; it does not imply that Azure resources currently exist.

## Environment boundaries

| Environment | Purpose | Promotion | Data and access |
| --- | --- | --- | --- |
| Local | Developer feedback through Docker Compose | Developer-selected source | Synthetic/local data and `.env`; never production credentials |
| Staging | Production-shaped integration and smoke testing | Automatic after protected `main` CI and image publication | Separate Azure resources, database, identities, Key Vault, and non-production data |
| Production | Customer workload | Manual approval of the exact staging-tested digests | Production-only resources, identities, secrets, database, alerts, and access review |

Resource names, locations, tags, identities, network rules, alert definitions, backup settings, and Container Apps configuration must be declared in versioned IaC. Portal edits are for emergency diagnosis only and must be reconciled back into IaC.

## GitHub configuration

Create GitHub environments named `staging` and `production`:

- `staging`: restrict deployments to `main`; use an Azure OIDC subject scoped to this environment.
- `production`: restrict deployments to `main` or signed release tags; require at least one reviewer, prevent self-review when the plan supports it, and use a separate production-scoped OIDC subject.
- Put non-sensitive resource identifiers in environment variables. Keep third-party secrets in Azure Key Vault. GitHub should contain no long-lived Azure credential.
- Keep `main` protected with pull requests and the backend, frontend, and container status checks. Block force pushes and branch deletion; dismiss stale approvals after material changes.
- Pin third-party GitHub Actions to full commit SHAs and grant each workflow only the permissions it needs. Deployment workflows require `id-token: write` and `contents: read`; package publication separately requires `packages: write`.

## Promotion pipeline

1. Pull request CI restores, builds, tests, lints, and builds both containers.
2. Merge to `main` publishes API, web, and worker images tagged with the commit SHA and records their immutable digests.
3. Staging deployment authenticates through OIDC, applies IaC drift-safe changes, runs the migration job, creates new revisions, and executes smoke tests.
4. Production waits for environment approval and promotes the exact staging-tested digests—never a mutable `main` tag.
5. The production migration job runs once. New revisions receive no traffic until readiness checks pass.
6. Shift a small percentage of traffic to the new web/API revisions, observe health and error budgets, then complete the rollout. The worker changes only after API compatibility is established.
7. Record commit, digests, migration, approver, revisions, and smoke-test result in the GitHub deployment record.

The pipeline must be concurrency-locked per environment so two database migrations or production promotions cannot overlap.

## Migration rules

- Disable application-startup migration in hosted environments.
- Build a dedicated migration command/container and execute it as a Container Apps Job using a migration identity distinct from the runtime identity.
- Use expand/migrate/contract changes. Add nullable fields or parallel structures first, backfill separately, switch readers/writers, and remove old schema only in a later deployment.
- Review locks and expected duration before production. Large backfills are resumable jobs, not schema-migration transactions.
- A failed migration stops promotion. Never automatically roll a database backward with down migrations.

## Secrets and access

Key Vault stores JWT signing material, PostgreSQL credentials if identity authentication is not yet available, OpenAI credentials, and source-provider credentials. Container Apps resolves Key Vault references with environment-specific managed identities. Operators receive time-bounded least-privilege roles; production access is reviewed quarterly. Logs and deployment output must never print secret values, repository contents, prompts, or model responses.

## Health, monitoring, and alerting

Readiness verifies that a process can accept traffic; liveness only detects a stuck process. Database and external-provider outages should remove readiness when serving requests would be unsafe without causing a rapid restart loop.

Dashboards and alerts cover:

- web/API availability, p50/p95/p99 latency, 4xx/5xx rate, restarts, CPU, and memory;
- indexing backlog age, lease expiry/renewal, completion/failure rate, and processing duration;
- chat/search rate, latency, cancellation, provider errors, token/cost guardrails, and quota rejection;
- PostgreSQL CPU, storage, connections, slow queries, replication/HA state, and backup failures;
- deployment and migration failures, certificate expiry, Key Vault access failures, and budget thresholds.

Alerts need an owner, severity, actionable threshold, and linked runbook. High-cardinality organization or repository IDs are structured log fields, not unbounded metric labels.

## Rollback and recovery

For an application regression, stop traffic promotion and restore 100 percent traffic to the previous healthy revision. Because database migrations are backward-compatible, the previous revision remains usable. Pause the worker if a new job format or write path is implicated.

For data loss or corruption:

1. Stop affected writers and preserve logs/evidence.
2. Restore PostgreSQL to a new server at the selected point in time.
3. Validate schema version, `pgvector`, tenant isolation, row counts, authentication, indexing, search, and chat in isolation.
4. Rebind application secrets/networking and deliberately cut traffic over.
5. Document actual RPO/RTO and corrective actions.

Retain automated PostgreSQL backups for at least 14 days in production. Test an isolated restore quarterly and before relying on a material database configuration change. Export IaC state securely and document how its backend is recovered.

## Domain and TLS

Use a product-owned DNS zone. Map the customer-facing hostname to the web Container App and use a managed certificate with automatic renewal. The API remains internal. Document DNS ownership, validation records, certificate status, and an emergency renewal owner. Add Azure Front Door and WAF only when edge security, global routing, or availability requirements justify their cost and complexity.

## Release readiness checklist

- IaC plan reviewed; cost estimate and budget alerts updated.
- CI checks green; images identified by digest; vulnerability results reviewed.
- Migration is backward-compatible and rehearsed against a staging-sized database.
- Staging smoke tests pass for login, tenant authorization, repository registration/indexing, endpoint catalog, semantic search, and repository chat.
- Dashboards, alerts, on-call owner, previous revision, and rollback command are known.
- Production approval recorded; post-deployment smoke test and observation window completed.

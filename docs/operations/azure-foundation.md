# Azure foundation operations

The Bicep entry point in `infra/bicep/main.bicep` provisions the environment-isolated foundation selected in ADR-008. It creates infrastructure only; application revisions and database migrations belong to issue #26.

## Prerequisites and bootstrap

- Azure CLI 2.76 or later with Bicep 0.45.15 or later.
- An Azure subscription where the required resource providers are registered.
- A product-owned operator email and an Azure pricing-calculator review.
- A bootstrap operator with rights to create resource groups, managed identities, federated credentials, and role assignments.

Register `Microsoft.App`, `Microsoft.ContainerRegistry`, `Microsoft.DBforPostgreSQL`, `Microsoft.Insights`, `Microsoft.KeyVault`, `Microsoft.ManagedIdentity`, `Microsoft.Network`, and `Microsoft.OperationalInsights`. The first deployment creates the environment-scoped GitHub identity and its resource-group-scoped Contributor and Role Based Access Control Administrator assignments. These allow the identity to maintain the foundation and its declared runtime assignments without subscription-wide access. Do not give the GitHub identity subscription Owner or broaden either assignment beyond its environment resource group.

Set the environment's real operator email and `applicationUrl` in its `.bicepparam` file before review. Supply `POSTGRES_ADMINISTRATOR_PASSWORD`, `JWT_SIGNING_KEY`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `GITHUB_ACCESS_TOKEN`, and `OPENAI_API_KEY` through the invoking process environment. The values are written to Key Vault and referenced by managed identity; they must never be committed, printed in Actions output, or passed as plain command arguments. Rotate bootstrap credentials after initial deployment and before their declared expiration.

## Validate and plan

From an authenticated, non-production shell:

```powershell
az bicep build --file infra/bicep/main.bicep
$env:POSTGRES_ADMINISTRATOR_PASSWORD = Read-Host -MaskInput
# Set the remaining secure inputs listed above in the same protected session.
az deployment sub validate --location centralus --template-file infra/bicep/main.bicep --parameters infra/bicep/environments/staging.bicepparam
az deployment sub what-if --location centralus --template-file infra/bicep/main.bicep --parameters infra/bicep/environments/staging.bicepparam --validation-level Provider
```

`what-if` requires permissions comparable to deployment. Save the redacted plan with the release record, confirm the target subscription, resource group, deletions, role changes, SKUs, region availability, and estimated monthly cost. Never approve a plan containing an unexpected resource-group replacement or secret value.

Run the same commands with `production.bicepparam` only from the protected production GitHub environment or an approved break-glass session. Pull-request CI performs offline compilation and Checkov scanning only.

## Apply

```powershell
az deployment sub create --name reponav-staging-foundation --location centralus --template-file infra/bicep/main.bicep --parameters infra/bicep/environments/staging.bicepparam
```

Apply staging first. The initial runtime definitions create bootstrap revisions for the web, API, and worker plus a manual migration job; the release workflow replaces those public bootstrap images with immutable ACR digests. Verify private PostgreSQL DNS from the Container Apps network, TLS 1.2, the `VECTOR` allow-list, backup retention, Key Vault references, per-service managed identities, ACR pull access, internal API discovery, telemetry ingestion, budget recipients, and alert action-group delivery. Production requires a separate reviewed plan and GitHub environment approval.

Copy the deployment outputs into the matching GitHub environment variables:

| Output | GitHub environment variable |
| --- | --- |
| `resourceGroupName` | `AZURE_RESOURCE_GROUP` |
| `registryName` | `AZURE_CONTAINER_REGISTRY` |
| `webAppName` | `AZURE_WEB_APP` |
| `apiAppName` | `AZURE_API_APP` |
| `workerAppName` | `AZURE_WORKER_APP` |
| `migrationJobName` | `AZURE_MIGRATION_JOB` |
| `deploymentIdentityClientId` | `AZURE_CLIENT_ID` |

Also configure `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `APPLICATION_URL`. The environment's `applicationUrl` Bicep parameter and GitHub `APPLICATION_URL` must describe the same externally reachable HTTPS origin. Until a custom domain is approved, use the generated web Container App hostname from `applicationHostname`.

## Drift

Run subscription `what-if` monthly and before every application platform release. Portal changes are diagnostic exceptions, not a second source of truth. Reconcile an approved emergency change into Bicep immediately; otherwise restore the declared state through deployment. Exporting templates from the portal is evidence only and must not overwrite the authored modules.

## Cost review

The checked-in estimates are planning envelopes, not Azure quotes:

| Environment | Bicep budget | Expected baseline | Primary cost drivers |
| --- | ---: | ---: | --- |
| Staging | $100/month | $45-$100/month | Burstable PostgreSQL, logs, registry, intermittent Container Apps |
| Production | $250/month | $120-$250/month | General Purpose PostgreSQL, storage/backups, logs, always-ready worker |

Before apply, capture an Azure Pricing Calculator estimate using the exact region and SKUs. OpenAI, GitHub, domain, egress spikes, and production database HA are excluded. Forecast-at-80-percent and actual-at-100-percent budget notifications are declared. Do not enable zone-redundant HA until its revised estimate and availability requirement are reviewed.

## Teardown

Production teardown requires an incident/change record, backup and restore validation, data-retention approval, and two-person confirmation. Disable workloads and preserve PostgreSQL first. Run a final `what-if`, then delete the environment resource group explicitly. Key Vault purge protection prevents immediate permanent deletion; do not attempt to bypass it. Remove the matching GitHub federated credential and environment access after Azure deletion completes.

Staging teardown follows the same inventory and backup checks but may use a single authorized approver. Never script deletion by a computed or wildcard resource-group name.

## Break glass

Use a named, phishing-resistant administrator account eligible through PIM, with a time-limited activation and an incident/change ticket. Confirm the subscription and exact resource IDs before changes. Preserve activity logs, avoid reading secret values, and prefer traffic isolation or identity disablement over deletion. When service is stable, revoke elevation, rotate any accessed credential, reconcile the change into Bicep, run `what-if`, and document the timeline and corrective action.

## Recovery ownership

The application/platform owner reviews plans, cost, drift, alerts, and quarterly restore tests. The database owner validates PITR and `pgvector`. Security reviews OIDC subjects and privileged role assignments quarterly. The later deployment workflow owns immutable image promotion, the migration job, smoke tests, revision traffic shifting, and rollback.

# ADR-009: Bicep for Azure infrastructure

## Status

Accepted

## Context

ADR-008 selected Azure Container Apps and Azure managed services. The foundation needs reusable, reviewable infrastructure definitions for staging and production without adding credentials or applying changes from pull requests.

## Decision

Use Bicep, pinned Azure resource API versions, and subscription-scoped entry points. Bicep is appropriate because the selected platform is Azure-only, ARM remains the control plane, and the language provides native type checking and module composition without a separate provider or state backend.

Each environment has its own resource group, virtual network, delegated PostgreSQL subnet, Container Apps environment, database, registry, vault, identities, telemetry, budget, alerts, web/API/worker apps, and manual migration job. Environment parameter files contain capacity and retention choices but obtain bootstrap and provider credentials from the process environment. They never contain credentials. Runtime containers reference Key Vault by managed identity, and each service pulls immutable ACR images through its own identity.

Pull-request CI compiles the entry point and scans all Bicep with Checkov. It does not authenticate to Azure or apply resources. Four named Checkov checks are deliberately excluded: CKV_AZURE_109 and CKV_AZURE_189 for Key Vault network access, CKV_AZURE_139 for ACR network access, and CKV_AZURE_166 for ACR quarantine. Private endpoints require materially more networking and Premium ACR cost than the approved low-traffic baseline. The initial foundation instead requires RBAC, managed identities, disabled registry admin/anonymous access, purge protection, secret expiry, and no public PostgreSQL access. Revisit the exclusions before regulated data or policy requires private endpoints.

GitHub deployments use separate user-assigned identities and federated credentials whose subjects are restricted to the `staging` and `production` GitHub environments. The deployment identity is Contributor and Role Based Access Control Administrator only within its environment resource group; the latter is required to maintain the explicitly declared runtime assignments without subscription-wide authorization. Initial role-assignment bootstrap requires a privileged operator. Runtime API, worker, and migration identities receive only ACR pull and Key Vault secret-read roles in this foundation.

## Consequences

- Azure is the only supported production target and the templates intentionally use native Azure concepts.
- An operator must bootstrap authorization and review `what-if`; pull requests cannot mutate Azure.
- PostgreSQL application and migration database roles must be created by the later migration/deployment work item because ARM cannot declare database-level grants safely.
- ACR and Key Vault use authenticated public service endpoints initially, while workloads and PostgreSQL are VNet integrated. Private endpoints for those services are a production-hardening follow-up if policy requires all platform traffic to remain private.
- Environment destruction is deliberate and protected by the runbook because Key Vault purge protection and PostgreSQL backups make full teardown asynchronous.

## References

- [ADR-008 production hosting](ADR-008-production-hosting.md)
- [Azure foundation operations](../operations/azure-foundation.md)

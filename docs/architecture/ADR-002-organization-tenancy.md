# ADR-002: Organization tenancy and authorization

Status: Accepted - 2026-08-05

## Context

RepoNav AI stores source code, derived repository knowledge, and AI conversations that belong to an organization. A tenant-bound resource must never be disclosed or mutated by a user outside that organization. The authorization model must remain consistent as projects, repositories, indexing jobs, and AI features are added.

## Decision

Organizations are the tenant boundary. Every tenant-owned aggregate will carry an `OrganizationId`, and application use cases must authorize the current user before loading or mutating tenant data.

Membership has three roles:

- `Owner` manages the organization, invitations, membership, and owner assignments.
- `Admin` manages organization settings and can invite or remove non-owner members.
- `Member` can access tenant resources but cannot administer the organization.

Authorization is centralized in the Application layer through `IOrganizationAccess`. Queries are scoped by both organization and current user. A user who is not a member receives `404 Not Found` instead of `403 Forbidden` so the API does not reveal whether another tenant exists. Authenticated members without the required role receive `403 Forbidden`.

The domain prevents removing or demoting the final owner. Owner access cannot be granted through an invitation; an existing owner must explicitly promote a member. Invitation tokens use cryptographically secure random bytes, are returned only when created, and only a SHA-256 hash is persisted. Invitations expire and can only be accepted by a signed-in user whose normalized email matches the invitation.

## Consequences

- Tenant authorization is enforced independently of controllers and remains testable without HTTP or EF Core.
- New tenant-owned features must include `OrganizationId` in their data model and use the same access service or tenant-filtered query pattern.
- Non-member `404` responses reduce resource-enumeration risk but require separate audit telemetry to diagnose denied access.
- Invitation delivery is currently represented by a copyable acceptance link. An email provider can be added later without changing token storage or acceptance semantics.
- Membership and role transitions preserve at least one owner at all times.

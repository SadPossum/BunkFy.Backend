# Workspace Access Profiles Task

Status: backend security foundation complete; product UX remains planned
Date: 2026-07-20

## Goal

Let workspace owners configure named access profiles and assign them to members without exposing GMA Admin APIs or allowing one workspace to affect another workspace's role definitions.

## Current Baseline

- Workspace owners receive the protected owner wildcard role.
- Ordinary members receive a front-desk baseline: property and inventory visibility, inventory blocking, reservation operations, guest profile operations, and Staff directory visibility.
- Ordinary members cannot configure properties or inventory sales, manage integrations, administer Staff lifecycle, archive guests, or manage workspace membership.
- Staff self-service profile reads and writes are identity-bound and do not require broad `staff.manage` access.

## Delivered GMA Capability

GMA AccessControl retains global compatibility roles and now separately owns tenant-scoped access profiles. Profiles have an immutable id and owner scope, a tenant-local key, display metadata, bounded permission definitions, optimistic versions, immutable change history, archive behavior, and scoped assignments.

The reusable module now provides:

- immutable profile id plus owning scope;
- display name and description separated from the internal role key;
- explicit permission allowlist supplied by the composing product;
- actor anti-escalation for every delegated permission;
- a Contracts policy seam for product-owned assignment eligibility;
- exact-scope, transactional assignment cleanup with immutable history;
- scoped assignment, update concurrency, and history facts;
- list/create/update/archive/assign APIs authorized in the owning scope;
- no dependency on Organizations, BunkFy modules, or product role names.

BunkFy supplies the explicit front-desk permission allowlist. A profile target must be a user with an active owner or member compatibility assignment in the same exact workspace scope. Suspension and removal first revoke those compatibility assignments and then remove every custom profile assignment through AccessControl Contracts, so a concurrent new assignment cannot survive offboarding.

## BunkFy Responsibility

- define seed profiles such as Owner, Manager, Front desk, Housekeeping, and Viewer;
- decide which BunkFy permission codes each profile may contain;
- prevent delegation above the caller's authority;
- present role visibility and action summaries in Workspace settings;
- coordinate invitation profile selection and later access changes;
- preserve access denial while assignments are provisioning or being removed.

The backend allowlist, exact-workspace assignment policy, and ordered offboarding cleanup are complete. Seed-profile management, invitation-time selection, summaries, and workspace-admin UI remain product work.

## Completion Proof

- two workspaces may use the same display name with different permissions and no cross-workspace reads or updates;
- a workspace owner can configure and assign allowed profiles without platform-admin access;
- ordinary members cannot grant owner or integration/security administration privileges;
- role changes update permission-filtered navigation and API authorization consistently;
- PostgreSQL, API contract, concurrency, isolation, and browser workflow tests pass.

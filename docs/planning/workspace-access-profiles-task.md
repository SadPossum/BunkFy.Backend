# Workspace Access Profiles Task

Status: planned after onboarding UX stabilization
Date: 2026-07-14

## Goal

Let workspace owners configure named access profiles and assign them to members without exposing GMA Admin APIs or allowing one workspace to affect another workspace's role definitions.

## Current Baseline

- Workspace owners receive the protected owner wildcard role.
- Ordinary members receive a front-desk baseline: property and inventory visibility, inventory blocking, reservation operations, guest profile operations, and Staff directory visibility.
- Ordinary members cannot configure properties or inventory sales, manage integrations, administer Staff lifecycle, archive guests, or manage workspace membership.
- Staff self-service profile reads and writes are identity-bound and do not require broad `staff.manage` access.

## Required GMA Capability

GMA AccessControl role definitions are currently global while assignments are scoped. A workspace role editor must not create unqualified global role names from tenant input.

Add a reusable scoped role-template capability to AccessControl, or an equivalent namespace-owning application seam, with:

- immutable template id plus owning scope;
- display name and description separated from the internal role key;
- explicit permission allowlist supplied by the composing product;
- scoped assignment and last-owner protection;
- rename/update concurrency and audit facts;
- list/create/update/archive/assign APIs authorized in the owning scope;
- no dependency on Organizations, BunkFy modules, or product role names.

## BunkFy Responsibility

- define seed profiles such as Owner, Manager, Front desk, Housekeeping, and Viewer;
- decide which BunkFy permission codes each profile may contain;
- prevent delegation above the caller's authority;
- present role visibility and action summaries in Workspace settings;
- coordinate invitation profile selection and later access changes;
- preserve access denial while assignments are provisioning or being removed.

## Completion Proof

- two workspaces may use the same display name with different permissions and no cross-workspace reads or updates;
- a workspace owner can configure and assign allowed profiles without platform-admin access;
- ordinary members cannot grant owner or integration/security administration privileges;
- role changes update permission-filtered navigation and API authorization consistently;
- PostgreSQL, API contract, concurrency, isolation, and browser workflow tests pass.

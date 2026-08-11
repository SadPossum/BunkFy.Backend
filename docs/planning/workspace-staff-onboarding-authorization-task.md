# Workspace Staff-Onboarding Authorization Task

Status: completed
Date: 2026-08-11

## Goal

Give Workspaces-owned Staff onboarding an explicit product capability instead
of inheriting Staff employment-profile edit authority.

## Confirmed Boundary

- Workspaces owns access plans, applicant staging, source lifecycle,
  onboarding review, and recoverable provisioning. Staff owns durable
  employment profiles after provisioning.
- GMA Organizations owns generic invitations, enrollment links, claims, and
  memberships. Its product-policy extension point already lets BunkFy
  authorize delegated non-owner operations without adding BunkFy concepts to
  GMA.
- `staff.manage` authorizes Staff profile edits. It must not also authorize
  issuing a future access grant, reviewing a join claim, or retrying Staff and
  access provisioning.
- GMA already provides scoped permissions, owner wildcards, policy extension
  points, and configurable authentication assurance. No framework or reusable
  module change is required.

## Invariants

1. Workspaces operator source, actionable-application, and retry routes require
   `workspaces.staff-onboarding.manage`, not `staff.manage`.
2. The Workspaces Organizations join-source policy requires the same capability
   for delegated invitation, enrollment-link, and claim-decision operations.
3. Operator access also requires `access-control.profiles.read`, because access
   plans select and disclose a server-owned role profile. Direct grants with an
   incomplete dependency set fail closed at runtime.
4. Applicant submission and own-status routes remain authenticated self-service
   flows bound to the exact subject and source; they do not require an operator
   capability.
5. Product-owned issue, revoke, disable, replace, and retry mutations require
   the host-configured privileged-operation assurance. Generic Organizations
   mutations retain their existing Organizations governance assurance.
6. The Manager seed retains onboarding management through a seed-version bump.
   Front desk, Housekeeping, Viewer, legacy-member, and company-support defaults
   do not gain it. The owner wildcard remains valid.
7. `workspaces.staff-access.manage` remains an Administration-only recovery
   permission for inspecting and retrying access-lifecycle processes; it is not
   reused as a customer role capability.
8. Source plans, applicant records, replay behavior, schemas, integration
   events, and cross-module ownership remain unchanged.

## Delivery

1. Add the Workspaces permission descriptor and role-catalogue entry.
2. Rebind public endpoint metadata and the Organizations authorization policy,
   including explicit access-profile read checks.
3. Apply configured assurance to Workspaces-owned mutations and wire it from
   the public host.
4. Add the capability to the Manager seed and delegable allowlist, increment
   the seed version, and exclude it from company-support and ordinary defaults.
5. Gate the web Invites workspace-settings surface on the new capability plus
   role visibility.
6. Update the Workspaces personal-data access policies and regenerate the
   deterministic inventory.
7. Add focused metadata, route, policy, seed, host-composition, and UI guards,
   then run one consolidated non-Docker backend/web gate at the slice boundary.

## Verification Cadence

No Docker scenario is required because this slice changes no schema,
transaction boundary, broker behavior, or provider-specific code. Focused
tests may run while editing; full backend and web gates run once after the
permission, assurance, seed, data-governance, and UI contracts are coherent.

Completed evidence on 2026-08-11:

- `eng/verify.ps1 -SkipRestore` passed deterministic solution and
  source-package checks, a zero-warning full solution build, every migration
  drift check, and all non-Docker suites. The affected Workspaces,
  Architecture, and integration suites passed 358, 102, and 60 tests
  respectively.
- `pnpm contracts:check` confirmed the OpenAPI snapshot and generated
  TypeScript client are current. `pnpm verify` passed type checking, lint, all
  51 web test files with 262 tests, and the production Vite build.
- Focused authorization checks additionally passed all 358 Workspaces tests,
  both workspace-settings access files with 7 tests, and TypeScript type
  checking before the consolidated gate.
- The GMA framework and reusable module checkouts remained clean. No Docker
  scenario was run because this slice changes no schema, transaction boundary,
  broker behavior, or provider-specific implementation.

## Deferred

- A future read-only onboarding/audit workflow may introduce a separate
  `workspaces.staff-onboarding.read` capability. The current product surface is
  an operational management workflow, so exposing its applicant and source
  data without action authority is not part of this slice.

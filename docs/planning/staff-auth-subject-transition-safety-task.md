# Staff Auth-Subject Transition Safety Task

Status: completed
Date: 2026-08-11

## Goal

Prevent a Staff account-link change from leaving an old Auth subject with live
workspace access or silently transferring privileges to a new subject.

## Boundary Decision

- Staff owns the employment record and its optional Auth-subject correlation.
- Workspaces owns organization membership and coordinates Staff lifecycle with
  workspace access. GMA AccessControl owns roles, grants, and authorization.
- Changing a Staff correlation must never grant, copy, or restore access. A
  caller with `staff.manage` is not thereby authorized to administer access.
- This workflow remains BunkFy-specific. It does not add account-link or
  employment policy to GMA.

## Safe Transition Contract

Exact no-change requests remain valid after expected-version validation and
retain the existing durable replay behavior. Real changes follow this matrix:

| Current Staff state | Current link | Requested link | Result |
| --- | --- | --- | --- |
| Active | none | subject B | Allowed; access is still managed separately |
| Active | subject A | none | Denied; suspend first so A is denied safely |
| Active | subject A | subject B | Denied; replacement is never atomic |
| Suspended | subject A | none | Allowed after lifecycle access denial |
| Suspended | subject A | subject B | Denied; clear first |
| Suspended | none | subject B | Denied; resume unlinked first |

Departed, anonymised, restricted, stale, invalid, hidden, and conflicting
requests keep their existing fail-closed behavior.

The supported relink sequence is:

1. Suspend the linked Staff member. The existing Staff lifecycle policy asks
   Workspaces to snapshot and deny the old subject's access before Staff
   commits the suspension.
2. Clear the Auth-subject link while Staff is suspended.
3. Resume the unlinked Staff member. No old-subject access is restored because
   the authoritative Staff correlation is now empty.
4. Link the new subject while Staff is active. Provision any intended
   membership, role, or grant through the Workspaces/AccessControl surface.

## Implementation

- Enforce the state matrix inside the `StaffMember` aggregate after identity,
  expected-version, and exact-no-op validation.
- Evaluate the aggregate transition before querying uniqueness. Rejected,
  exact-no-op, and clear operations perform no uniqueness lookup; a real link
  checks only Auth-subject uniqueness and not unrelated profile fields.
- Return explicit conflict errors for unsafe unlink, replacement, and linking
  while suspended. Public and Admin APIs map them to HTTP 409; CLI surfaces
  retain the same domain messages.
- Keep failed operations out of the Staff mutation journal and outbox.
- Make the Staff account panel state-aware: linked active records explain and
  offer suspension as the next lifecycle action, linked suspended records may
  clear only, and active unlinked records may link. It never suggests that
  linking grants permissions.
- Keep the wire contract, persistence schema, integration events, and GMA
  source unchanged.

## Verification

- Domain tests cover every real transition in the matrix, exact no-op behavior,
  version/event preservation on denial, and existing departed/anonymised
  protections.
- Application tests prove rejected commands write no mutation receipt or event,
  while the supported suspend-clear-resume-link sequence remains replay-safe.
- API error-map tests and focused web tests cover the explicit conflict
  responses and state-aware account-panel behavior.
- Run focused non-Docker checks while editing, then one consolidated Staff,
  architecture, generated-contract, web, and solution-build gate at the slice
  boundary. No PostgreSQL gate is required because the schema is unchanged.

Completed evidence on 2026-08-11:

- `eng/verify.ps1 -SkipRestore` passed deterministic solution and source-package
  checks, a zero-warning full solution build, migration drift checks, and all
  non-Docker suites. The affected Staff, Workspaces, Operations Notifications,
  Architecture, and integration suites passed 264, 354, 100, 102, and 60 tests
  respectively.
- `pnpm contracts:check` confirmed the OpenAPI snapshot and generated
  TypeScript client are current. `pnpm verify` passed type checking, lint, all
  51 web test files with 262 tests, and the production Vite build.
- The GMA framework checkout remained clean. No Docker scenario was run because
  the slice changes no persistence schema or provider-specific behavior.

## Deferred Follow-Up

- A future first-class account-transfer process may coordinate Staff,
  Workspaces, and AccessControl only if product requirements define explicit
  old-account denial, new-account provisioning, privilege selection, owner
  protection, recovery, and audit semantics. It must not infer permission
  transfer from an Auth-subject replacement.

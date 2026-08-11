# Staff Account-Link Authorization Task

Status: completed
Date: 2026-08-11

## Goal

Make manual Staff-to-Auth account correlation an explicit high-impact
capability instead of inheriting generic profile-edit authority.

## Confirmed Boundary

- Staff owns the optional Auth-subject correlation and its safe transition
  rules. Auth owns accounts; Workspaces and AccessControl own membership,
  roles, and grants.
- `staff.manage` describes employment-profile edits, but public, Admin API, and
  Admin CLI account-link operations currently reuse it. That lets any custom
  role with profile-edit authority also bind account identity.
- The public operation has no configurable recent-authentication requirement,
  despite changing which account is correlated with an employment identity.
- Normal joining already uses the trusted Workspaces onboarding contract.
  Manual account linking is a recovery/administration capability and does not
  need to be present in an ordinary operational seed.
- GMA already provides scoped permission descriptors, owner wildcards,
  Administration permissions, and authentication-assurance filters. No
  framework change is required.

## Invariants

1. Manual account-link changes require `staff.account-links.manage` on every
   public, Admin API, and Admin CLI surface.
2. Public account-link changes also require sensitive-profile read authority
   and the host-configured privileged-operation assurance.
3. `staff.manage` continues to authorize profile edits but no longer authorizes
   account correlation.
4. The new permission is delegable and visible in the workspace role catalogue,
   but absent from Manager, Front desk, Housekeeping, Viewer, legacy-member,
   and company-support defaults. The workspace-owner wildcard remains valid.
5. Trusted onboarding and owner bootstrap correlation are unchanged; they do
   not route through the manual operator endpoint.
6. Linking never grants, copies, restores, or infers workspace access.
7. Aggregate transition safety, expected-version checks, replay receipts,
   persistence schema, and integration-event shapes remain unchanged.

## Delivery

1. Add the Staff permission descriptor and Administration permission.
2. Rebind public, Admin API, and CLI account-link operations to the dedicated
   capability.
3. Apply the configured privileged-operation assurance to the public route.
4. Add the permission to the Workspaces role catalogue and delegable allowlist,
   with explicit seed and company-support exclusions.
5. Gate web account-link controls on the new permission while keeping permitted
   sensitive-profile views read-only when it is absent.
6. Add focused metadata, route, seed, and UI-state guards, then run one
   consolidated non-Docker backend/web gate at the slice boundary.

## Verification Cadence

No Docker scenario is required because this slice changes no schema,
transaction boundary, broker behavior, or provider-specific code. Focused
tests may run while editing; full backend and web gates run once after the
permission catalogue and UI are coherent.

Completed evidence on 2026-08-11:

- `eng/verify.ps1 -SkipRestore` passed deterministic solution and source-package
  checks, a zero-warning full solution build, migration drift checks, and all
  non-Docker suites. The affected Staff, Workspaces, Architecture, and
  integration suites passed 271, 355, 102, and 60 tests respectively.
- `pnpm contracts:check` confirmed the OpenAPI snapshot and generated
  TypeScript client are current. `pnpm verify` passed type checking, lint, all
  51 web test files with 262 tests, and the production Vite build.
- The GMA framework checkout remained clean. No Docker scenario was run because
  the slice changes no persistence schema or provider-specific behavior.

## Deferred

- Workspaces staff invitation/source/review operations currently reuse
  `staff.manage`. Their ownership and dedicated permission should be handled as
  a separate Workspaces onboarding-authority slice rather than folded into this
  Staff account-link change.

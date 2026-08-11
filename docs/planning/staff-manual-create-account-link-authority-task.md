# Staff Manual-Create Account-Link Authority Task

Status: completed
Date: 2026-08-11

## Goal

Prevent ordinary Staff profile creation from attaching an Auth subject through
the weaker `staff.create` authority or bypassing the dedicated account-link
workflow.

## Boundary Decision

- Staff owns employment profiles and their optional Auth-subject correlation.
- GMA Auth owns accounts and authentication. GMA AccessControl owns roles,
  grants, and effective authorization.
- Manual profile creation and account correlation are separate operator
  capabilities. Creating an employment record does not imply authority to
  inspect or bind a sign-in account.
- Trusted workspace onboarding and owner identity bootstrap retain their
  explicit versioned correlation contracts. They are not manual management
  create operations.
- This is BunkFy Staff policy. It changes no GMA source or generic contract.

## Invariants

- Public API, Admin API, Admin CLI, and the management
  `CreateStaffMemberCommand` cannot accept an Auth subject.
- Every manual create operation produces an active, unlinked Staff profile and
  performs no Auth-subject uniqueness lookup.
- A manager links an account only after creation through the dedicated
  expected-version account-link command and its transition-safety rules.
- Onboarding provisioning and owner identity bootstrap may still create or
  reconcile their already-authorized subject correlation.
- Existing Staff links are not rewritten. Persistence schema, integration
  event shapes, and replay storage remain unchanged.

## Delivery

1. Remove Auth-subject input from the management create command and all three
   operator surfaces.
2. Remove the misleading create/update CLI option and the advanced create-form
   field in the web application.
3. Add application, API, CLI, and web guards that keep manual creation
   unlinked while preserving trusted onboarding/bootstrap contracts.
4. Update the executable Staff personal-data catalogue, generated inventory,
   OpenAPI snapshot, and TypeScript contracts.
5. Run focused checks while editing, then one coherent non-Docker backend and
   web gate at the slice boundary.

## Verification Cadence

No Docker scenario is required because the slice changes no schema,
transaction boundary, broker behavior, or provider-specific code. Run the
complete backend and web gates once after the boundary and generated artifacts
are coherent.

Completed evidence on 2026-08-11:

- `eng/verify.ps1 -SkipRestore` passed deterministic solution and source-package
  checks, a zero-warning full solution build, migration drift checks, and all
  non-Docker suites. The affected Staff, Architecture, and integration suites
  passed 271, 102, and 60 tests respectively.
- `pnpm contracts:check` confirmed the OpenAPI snapshot and generated
  TypeScript client are current. `pnpm verify` passed type checking, lint, all
  51 web test files with 262 tests, and the production Vite build.
- The GMA framework checkout remained clean. No Docker scenario was run because
  the slice changes no persistence schema or provider-specific behavior.

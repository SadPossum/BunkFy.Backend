# Ingestion Sensitive-Control Assurance Task

Status: completed
Date: 2026-08-11

## Goal

Protect the public Ingestion operations that destroy replay position or release
a tenant-wide ingress stop, while preserving fast emergency shutdown and normal
day-to-day connection management.

## Confirmed Boundary

- Ingestion owns checkpoint-reset confirmation, operation classification,
  permissions, request contracts, and exact idempotent retries.
- GMA Security owns the reusable authentication-assurance requirement,
  evaluation, challenge, and endpoint metadata.
- The BunkFy public host owns deployment policy and maps its configured
  privileged-operation requirement to each protected Ingestion operation.
- The browser owns an understandable recent-authentication recovery flow and
  retries the exact failed operation after successful step-up.
- No GMA framework or reusable module change is required.

## Invariants

1. Checkpoint reset requires explicit confirmation at the Ingestion application
   command boundary before lifecycle admission, repository access, journal
   lookup, mutation, or event publication.
2. A rejected unconfirmed request does not reserve its operation id. Confirmation
   is an admission fact rather than mutation intent and is not included in the
   existing checkpoint-reset fingerprint.
3. The public checkpoint-reset endpoint requires the existing connection-manage
   permission, property scope, explicit confirmation, and configured reset
   assurance.
4. Resuming tenant-wide adapter ingress requires the dedicated ingress-control
   permission and configured resume assurance. Suspending ingress remains
   available without step-up so incident containment is never delayed.
5. Credential issuance and revocation retain their configured assurance.
   Connection enable, disable, settings, polling-schedule controls, reads, and
   ingress suspension do not gain assurance in this slice.
6. Browser recovery preserves the operation id and normalized request while
   confirming the current password, then retries exactly once through the same
   mutation path. Changing or cancelling intent still rotates the operation id.
7. Module assurance options remain optional for embedding hosts. BunkFy's public
   host explicitly configures every protected operation class.
8. Product-specific operation names, confirmation rules, and UX do not enter
   GMA.

## Delivery

1. Add application-level checkpoint-reset confirmation and a dedicated public
   request contract with stable HTTP 400 behavior.
2. Add separate API assurance options for checkpoint reset and ingress resume,
   configure them in the public host, and attach exact endpoint metadata.
3. Add focused application, route-metadata, request-contract, and host-composition
   guards proving both the protected and intentionally unprotected boundaries.
4. Align generated browser contracts, submit confirmation only for checkpoint
   reset, and add reusable recent-authentication recovery for reset and
   credential issue/revoke.
5. Update concise module documentation and run focused checks followed by one
   consolidated non-Docker backend and web gate.

## Verification Cadence

No Docker scenario is required because this slice changes no schema,
transaction boundary, broker behavior, provider behavior, or persistence
contract. Focused Ingestion, architecture, and browser tests may run while
editing; the complete non-Docker backend and web gates run once when the slice
is coherent.

## Completed Evidence

- Focused backend verification passed: 337 Ingestion tests and 32 host
  composition guards, with the public host contract build completing with zero
  warnings and zero errors.
- `eng/verify.ps1 -SkipRestore` passed: solution synchronization, source/package
  guards, a zero-warning build, migration-drift checks, and every non-Docker
  framework, extension, module, architecture, migration-host, service-default,
  and integration test project. This includes 100 Operations Notifications
  extension tests, 337 Ingestion tests, 102 architecture tests, and 60 host
  integration tests.
- Focused browser verification passed: five connection-control and
  authentication-assurance tests, TypeScript checking, and targeted linting.
- `pnpm verify` passed: TypeScript checking, linting, 52 test files with 264
  tests, and the production build.
- `pnpm contracts:check` passed against the generated public-host OpenAPI
  snapshot.
- Docker verification was intentionally omitted under the cadence above. GMA
  framework and reusable-module worktrees remained unchanged.

## Deferred

- A staff-facing tenant ingress stop/recovery screen; the authenticated API and
  operational runbook remain the current control surface.
- External-provider or MFA step-up adapters for accounts without a password;
  those belong to the reusable authentication product flow, not Ingestion.
- Stronger per-operation assurance can be selected later through host policy
  without changing the module contracts.

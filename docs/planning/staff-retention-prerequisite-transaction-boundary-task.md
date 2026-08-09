# Staff Retention Prerequisite Transaction Boundary Task

Status: completed
Date: 2026-08-07

## Goal

Remove the Staff-retention shared-to-exclusive tenant-lock upgrade while
preserving the durable Workspaces proof required before irreversible Staff
anonymisation.

## Finding

`ApplyStaffRetentionCommand` currently holds the Staff transaction's shared
tenant-admission lock and Staff operation row lock while invoking the
Workspaces prerequisite. A first-time prerequisite dispatches the Workspaces
correlation scrub, which correctly requests the same tenant-admission
coordinate exclusively. PostgreSQL therefore waits on the caller's own shared
lock until the task handler times out.

The exclusive Workspaces lock is not optional: it prevents onboarding,
staff-access, export, and tenant-termination writers from racing the
subject-wide correlation scrub.

## Ownership

- Staff owns retention selection, prerequisite requirements, final
  eligibility revalidation, Staff mutation, and retention proof.
- Workspaces owns access closure, correlation mutation, and the durable
  prerequisite receipt.
- Retention owns only scheduling and owner-result visibility.
- GMA owns the provider-neutral transaction, task, and advisory-lock
  primitives. No GMA change is required.

## Decision

1. Version the Staff prerequisite contract around two explicit phases:
   `PrepareAsync` and `VerifyAsync`.
2. The Staff contributor prepares every prerequisite before dispatching the
   transactional Staff mutation.
3. First-time Workspaces preparation acquires the tenant coordinate
   exclusively, then the narrower Staff coordinate, before reading mutable
   Staff/Workspaces state, re-denying access, scrubbing correlations, and
   writing its immutable receipt.
4. The Staff mutation acquires its normal shared tenant admission and Staff
   operation lock, re-evaluates all Staff-owned eligibility, and invokes only
   read-only prerequisite verification.
5. Missing, malformed, stale, blocked, duplicate, or unavailable proof fails
   closed with the existing stable Staff retention outcomes.

## Invariants

- No transaction upgrades the common tenant coordinate from shared to
  exclusive.
- Workspaces never asks Staff to mutate and Staff never references a
  Workspaces implementation project.
- Preparation may safely narrow access before the Staff commit; retries are
  idempotent through the immutable Workspaces receipt.
- Verification performs no cross-module mutation and cannot create proof.
- The final Staff transaction still rejects a changed Staff version,
  operation-lock revision, governance binding, restriction, hold, or policy
  decision.
- The Retention task payload and result remain free of Staff and Auth subject
  identifiers.

## Delivery

1. [x] Add the versioned two-phase Staff prerequisite contract and evaluator.
2. [x] Prepare prerequisites in the Staff contributor and verify proof in the
   transactional mutation handler.
3. [x] Move first-time Workspaces state validation, access denial, and scrub
   behind the exclusive Workspaces lock.
4. [x] Add focused contract, handler, contributor, and lock-order coverage.
5. [x] Run focused fast tests, the exact PostgreSQL retention scenario, then
   one consolidated slice gate.

## Evidence

- Staff module tests: 242 passed.
- Workspaces module tests: 338 passed.
- Focused Workspaces retention and lock-order tests: 5 passed.
- PostgreSQL retention scheduler regression: 1 passed in 29 seconds, without
  the former task timeout or shared-to-exclusive lock upgrade.
- Consolidated Docker slice gate exercised all 112 scenarios; 111 passed on
  the aggregate run, and the sole stale Inventory destruction-count fixture
  passed after its focused correction.
- `eng/verify.ps1 -SkipRestore` passed solution/package guards, a zero-warning
  build, every migration drift check, all fast suites, 94 architecture tests,
  and 54 non-Docker integration tests.

## Deferred

- Distributed-service choreography outside the modular-monolith process.
- Generalising BunkFy retention ownership policy into GMA.
- Changing generic task timeouts or weakening tenant-termination admission.

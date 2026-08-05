# Workspaces Tenant Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Complete the final product-owner destruction step without moving BunkFy
workspace policy, table knowledge, or coordinator semantics into GMA.
Workspaces must close its last termination-only path, remove every remaining
tenant-owned operational and projection copy in bounded resumable batches, and
retain only exact PII-free terminal proof.

## Boundary

- The existing Application contributor remains the `workspaces` owner for
  `Freeze`, `Export`, `Restore`, and `Destroy` routing.
- A Workspaces Application port exposes only the product-local destruction
  operation. Its Persistence implementation owns table discovery, ordering,
  batching, transaction admission, and provider proof.
- Data Rights owns approved process coordinates, dependency scheduling, and
  bounded owner results. It never receives workspace row identifiers or
  removed values.
- GMA Organizations, Access Control, Notifications, and Task Runtime keep
  authority over their generic stores. Their BunkFy adapters complete before
  final Workspaces closure.
- GMA supplies only the reusable shared/exclusive transaction-key lock and
  messaging hooks. No Workspaces owner key, table, stage, or hospitality policy
  belongs in GMA.

## Ordering

`Destroy` depends on `properties` and `task-runtime`.

- `properties` transitively covers Inventory, Reservations, Operations
  Notifications, Retention, Ingestion, Staff, and Guests.
- `task-runtime` transitively covers Access Control, Organizations, Operations
  Notifications, Retention, Ingestion, Staff, Guests, and Reservations.

Together those two terminal branches cover every current mandatory owner while
keeping the dependency graph minimal. Export remains dependency-free because
Workspaces supplies the frozen root coordinate.

## Lifecycle And Replay

- The existing workspace termination fence is the local lifecycle authority.
  Destruction accepts only the exact frozen process, case, approval, epoch,
  policy digest, ambient tenant, and operation request.
- First acceptance transitions the fence from `Frozen` to
  `DestructionStarted`, creates its immutable transition receipt, and persists
  a separate bounded destruction operation.
- One scope and one operation id may identify only one canonical request.
  Equivalent retries resume; a completed retry replays exactly; changed
  coordinates conflict.
- Completion verifies owner-record absence, closes the fence, writes the final
  fence receipt and an immutable destruction receipt, and removes operation
  state in one transaction.
- The retained minimum is the closed fence, its final close receipt, and one
  PII-free destruction receipt. Prior transition receipts and operational
  history are removed.

## Admission And Batching

- Ordinary Workspaces mutations and admitted inbox work use a shared tenant
  lock. Freeze, restore, export, and destruction use the exclusive counterpart.
- A frozen or destruction-started fence rejects ordinary domain mutation,
  scoped inbox admission, outbox creation, and new outbox claims. Existing
  publication leases drain before their rows may be removed.
- Each invocation removes at most one non-empty batch of 500 physical rows.
  Empty stages advance in the same transaction.
- Owned access-profile snapshots and access-plan property rows are removed
  explicitly before their parents. Restore receipts precede anonymisation
  tombstones; all immutable receipt families are deleted only under the exact
  transaction-local live destruction operation.
- Every committed batch extends a versioned SHA-256 chain over its stage and
  stable owner-local row keys. No removed key enters Data Rights state.

## Acceptance

- existing freeze, restore, and export behavior remains unchanged;
- phase-specific topology is complete and acyclic;
- active outbox leases block progress and closing scopes yield no new claims;
- one invocation removes no more than 500 physical rows;
- child rows are explicit and foreign-key-safe;
- every Workspaces-owned tenant table is empty at completion except the three
  terminal proof records;
- immutable receipt guards reject ordinary mutation and authorize only the
  exact live destruction operation;
- exact replay, changed-request conflict, closed admission, and tenant
  isolation are proved;
- the Workspaces personal-data catalogue classifies every new persisted proof
  member; and
- the full fast Workspaces suite and one exact PostgreSQL 16 scenario pass.

## Verification

- focused destruction and contributor tests: 15 passed;
- full Workspaces suite: 272 passed;
- integration test project build: zero warnings and errors;
- EF Core pending-model check: no pending changes;
- exact PostgreSQL 16 destruction scenario: 1 passed; and
- scoped `git diff --check`: clean.

## Deferred Production Work

- cross-owner preflight and terminal coordinator execution;
- protected replay and restore-readiness convergence;
- operator API, Admin API, Admin CLI, assurance, retry, and recovery controls;
  and
- production admission tied to the exact final mandatory-owner catalogue.

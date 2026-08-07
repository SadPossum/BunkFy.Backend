# Staff Employment Lifecycle Idempotency Task

Status: completed
Date: 2026-08-07

## Goal

Make Staff suspension, resumption, and departure safe to retry after an
uncertain response without repeating Staff lifecycle events, property-assignment
closures, or Workspace access coordination.

## Audit Finding

The lifecycle flow already serializes each Staff mutation and coordinates
Workspace access through a durable, target-version process. Its callers still
identify an attempt only by the Staff aggregate version, while every handler
generates a new transition id. If the Staff transaction commits and its response
is lost, an otherwise identical retry reports an already-state or version error
instead of proving the first attempt succeeded.

Profile edits and Auth-subject changes now use the Staff-owned immutable member
mutation journal. Employment lifecycle changes have the same retry boundary and
can extend that journal without introducing another persistence abstraction.

## Ownership

- Staff owns lifecycle request equivalence, successful-operation receipts,
  aggregate events, assignment history, retention, and replay visibility.
- Workspaces continues to own access denial/restoration and its durable process
  keyed by Staff member and target Staff version. Staff does not copy that
  process or report it as complete before its own transaction commits.
- Organizations and AccessControl continue to own membership and permission
  state behind the existing Workspaces coordinator.
- Public API, Admin API, Admin CLI, and web callers supply one non-empty
  operation id for each logical lifecycle attempt.
- GMA continues to own transactional command execution, optimistic concurrency,
  outbox/inbox delivery, and generic exception mapping. Staff lifecycle
  vocabulary and receipts remain in BunkFy.

## Invariants

1. The idempotency coordinate is `(ScopeId, StaffMemberId, OperationId)`.
2. Every request passes current tenant authorization and the action's current
   Staff visibility path before a replay is disclosed: operational visibility
   for resume, and the existing fail-closed safety path for suspend/depart.
3. The existing per-member mutation lock is acquired before reading the
   operation journal, aggregate state, or coordinating Workspaces.
4. Exact equivalence includes action kind, expected Staff version, normalized
   reason, and departure effective date where applicable.
5. An exact completed replay returns the immutable minimal Staff mutation
   receipt and does not rerun policy coordination, mutate assignments, or emit a
   second lifecycle event.
6. Reusing an operation id for another action or payload returns one stable
   conflict mapped to HTTP 409.
7. Invalid, denied, stale, owner-protected, or coordination-pending attempts do
   not bind an operation id because no Staff lifecycle mutation committed.
8. A new request against an already suspended, active, or departed Staff member
   keeps the existing domain result. Only a matching successful operation may
   replay as success.
9. The successful Staff mutation, assignment changes, lifecycle outbox event,
   and journal record commit atomically in the Staff transaction.
10. Workspaces may safely resume a pre-commit access process by target Staff
    version. The Staff operation id is not a replacement for that module-owned
    correlation or an authority over Workspace state.
11. The journal stores only operation id, Staff coordinate, action kind,
    expected version, a canonical SHA-256 request fingerprint, result
    status/version, and completion time. It stores neither reason nor actor.
12. Operation records remain append-only, tenant-filtered, cascade with their
    Staff member, participate in Staff export and tenant destruction, and obey
    the existing Staff retention policy.

## Surfaces

- Add `OperationId` to suspend, resume, and depart application commands.
- Require it in public and Admin API request contracts.
- Require `--operation-id` in Admin CLI lifecycle commands.
- Return `StaffMemberMutationReceiptDto` from lifecycle commands and endpoints;
  callers refetch current Staff detail after success.
- Keep one web operation id and the attempt's original expected version while an
  unchanged lifecycle submission is retried. Allocate a new id when the action,
  normalized reason, effective date, selected member, or successful attempt
  changes.

## Persistence

Extend `StaffMemberMutationKind` with suspend, resume, and depart values and
widen the PostgreSQL kind constraint. No new table or duplicated personal data
is required. Existing profile and Auth-subject operation rows remain unchanged.

## Verification

- Focused application tests cover exact replay, changed/cross-kind conflict,
  failed-attempt reuse, visibility before replay, and no repeated policy/event
  or assignment effect.
- API, Admin API, CLI, contract, and web tests cover the required operation id,
  receipt response, and stable browser attempt behavior.
- Persistence tests cover all lifecycle kinds, append-only behavior, cascade,
  export, tenant destruction, and migration drift.
- One focused PostgreSQL integration scenario proves concurrent exact retries
  commit one journal row and one lifecycle event, and that a failed transaction
  persists neither.
- Use cheap focused checks while editing. Run one consolidated non-Docker slice
  gate and one targeted Docker scenario at completion.

## Not In This Slice

- scheduled future departure execution;
- ownership-transfer UX;
- Auth account disabling or global session revocation;
- changing Workspaces lifecycle process semantics;
- generic GMA idempotency or response-cache infrastructure.

## Completion Evidence

- Solution synchronization, source-package checks, the warning-free backend
  build, all provider migration-drift checks, and all 94 architecture tests
  passed.
- All 228 Staff tests and the complete 4,701-test non-Docker backend matrix
  passed. The migrated Operations Notifications contributor remained green at
  99/99 tests.
- The targeted PostgreSQL scenario passed 1/1 and proved legacy migration,
  concurrent exact replay for suspend, resume, and depart, one lifecycle event
  per committed transition, atomic rollback, tenant isolation, immutable
  receipts, and cascade cleanup.
- Generated OpenAPI and TypeScript contracts are current. Web type checking,
  lint, all 195 tests across 34 files, and the production build passed.
- GMA source remained unchanged. Staff owns lifecycle equivalence and receipts;
  Workspaces retains its target-version access process and GMA retains the
  generic transaction, retry, lock, and outbox infrastructure.

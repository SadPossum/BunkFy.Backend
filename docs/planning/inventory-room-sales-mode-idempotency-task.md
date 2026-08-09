# Inventory Room Sales Mode Idempotency Task

Status: completed
Date: 2026-08-07

## Goal

Make room sales-mode configuration safe to retry after an uncertain response.
One logical request must commit at most one configuration version, availability
mutation version, event, outbox message, and immutable receipt. Reusing an
operation id for a changed request must fail closed.

## Audit Finding

The current command has optimistic concurrency but no caller-owned operation
identity. A committed mode change retried with its original expected version
fails as stale, and two concurrent copies can race. A no-op succeeds only while
the caller still has the current version, so it cannot prove an earlier result
after later edits.

Manual block and retirement commands have related retry gaps, but they are not
part of this slice. Blocks carry free-text operational reasons and fan out over
multiple units. Retirement is a durable draining process with its own natural
identity and retry states. Each needs a separate contract and audit.

## Ownership

- Inventory owns sales-mode invariants, room-scoped serialization, operation
  equivalence, immutable receipts, event cardinality, and journal lifecycle.
- Public API, Admin API, Admin CLI, and web callers supply one non-empty
  operation id per logical configuration attempt.
- Workspaces remains the operational-admission authority through Inventory's
  existing tenant mutation fence.
- Properties remains the source of physical Room and Bed topology through
  contracts and events only.
- Reservations and Inventory allocations remain the authorities for active
  claims that prevent a material mode change.
- GMA continues to own transactional command dispatch, transaction-key locks,
  optimistic concurrency, and outbox delivery. No framework change is needed.

## Invariants

1. A non-empty operation id, valid ids, supported mode, and positive expected
   version are required before locking or journal lookup.
2. Every attempt acquires workspace operational admission and an exclusive
   Room management lock before loading a receipt or mutable state.
3. The journal key is `(ScopeId, RoomId, OperationId)`. Operation identity is
   Room-resource scoped, matching the lock and version authority. The stored
   request fingerprint binds property, room, expected version, and target mode.
4. Exact replay returns the immutable stored receipt before current topology,
   lifecycle, claim, or version checks and emits no additional event.
5. Reusing an operation id in the same Room for another property, expected
   version, mutation kind, or target mode returns a management-operation
   conflict mapped to HTTP 409. Another Room is a separate resource coordinate.
6. Missing, retired, bedless, blocked-by-claims, stale, invalid, unauthorized,
   or unavailable attempts do not bind the operation id or consume an event id.
7. A no-op records a successful receipt at the unchanged version without
   advancing availability state or publishing an event.
8. A material change advances configuration and availability versions exactly
   once and stores its receipt in the same transaction as its event and outbox
   message.
9. The journal stores only operation and resource coordinates, mutation kind,
   expected version, a canonical SHA-256 fingerprint, result mode and version,
   and completion time. It stores no actor identifier or topology labels.
10. Journal rows are append-only, tenant-filtered, exported, removed in bounded
    tenant destruction, and denied to a closing workspace even on exact replay.

## Persistence

Add an Inventory-owned management operation journal rather than coupling this
module to another domain's receipts. Its first kind is room sales-mode
configuration, while the schema leaves room for later Inventory management
operations without pretending they already share semantics.

The migration must constrain non-empty coordinates, valid operation/resource
kinds, lowercase SHA-256 fingerprints, supported result modes, positive
versions, and a result version equal to either the expected version or expected
version plus one. Downgrade must refuse to discard committed journal rows.

The Inventory tenant export schema and contributor catalog version advance to
include the journal record. The personal-data catalog does not change because
the receipt and fingerprint contain no actor, reason, label, or guest data.

## Surfaces

- Add `OperationId` to `ConfigureRoomSalesModeCommand` and public/Admin request
  contracts.
- Require `--operation-id` in the Admin CLI configure command.
- Keep one browser operation id and the attempt's original Room version while
  the same property, room, and target mode are retried. Clear it after success,
  cancellation, target change, property change, or room refresh that begins a
  new user attempt.
- Keep `RoomInventoryMutationReceiptDto`; callers refetch authoritative
  inventory after success.

## Verification

- Application tests cover exact immutable replay, no-op receipt behavior,
  changed reuse, failed-attempt reuse, admission-before-replay, and event/id
  cardinality.
- API, Admin API, Admin CLI, generated contracts, and web tests cover required
  operation ids and stable browser retries.
- Persistence tests cover constraints, tenant filtering, append-only behavior,
  export, and bounded destruction.
- One PostgreSQL scenario covers concurrent exact replay, no-op/material event
  cardinality, migration compatibility, and closing-workspace admission.
- Use focused cheap checks while editing, then one Docker scenario and one
  consolidated backend/web gate at slice completion.

Completed evidence:

- Inventory module tests: 117 passed.
- Web tests: 212 passed, including stable operation identity across retries.
- PostgreSQL room sales-mode operation scenario: 1 passed.
- Consolidated Docker slice gate exercised all 112 scenarios; the only stale
  assertion was the bounded-destruction proof total advancing from 523 to 524
  for the new management-operation receipt, and its focused rerun passed.
- `eng/verify.ps1 -SkipRestore` passed solution/package guards, a zero-warning
  build, every migration drift check, all fast suites, 94 architecture tests,
  and 54 non-Docker integration tests.

## Not In This Slice

- manual block creation, group fan-out, release, or reason-data handling;
- bed or room retirement request/retry semantics;
- allocation request/operation replay, which already has its own durable locks;
- generic GMA idempotency middleware, response caching, or a shared journal;
- changes to Properties, Reservations, or Workspaces domain ownership.

# Reservations Stay Amendment Convergence Task

Status: active
Date: 2026-08-12

## Goal

Let authorized staff change the complete desired pre-arrival stay of a confirmed
reservation: arrival, departure, expected arrival/departure times, and concrete
Inventory units. Keep the existing booking and allocation authoritative until
Inventory atomically confirms the replacement. Make the asynchronous outcome,
exact retry, operator status, and lost-delivery recovery truthful and durable.

This slice belongs to Reservations. Inventory's existing amendment contract and
decision ledger already accept the desired dates and units and remain the sole
no-overbooking authority. No GMA change or generic process abstraction is
required.

## Ownership

- Reservations owns the desired stay, amendment operation coordinate, request
  equivalence, pending candidate, operator receipt, outcome, and recovery
  decision.
- The operator-facing operation identifier is reservation-local. Reservations
  also owns a separate, system-generated globally unique Inventory request
  coordinate because Inventory's durable decision ledger is global by request
  identifier. That internal coordinate is never accepted from the caller.
- Inventory owns allocation validity, conflicts, atomic replacement, and exact
  decision replay for an existing amendment coordinate.
- The current Reservation and current Inventory allocation remain authoritative
  until a correlated confirmation commits.
- GMA owns the existing command transaction, operation lock, inbox/outbox, and
  delivery substrate. It does not own hostel stay-amendment policy.

## Supported Workflow

The new desired-state command carries:

- property and reservation identifiers;
- one stable amendment operation identifier;
- target arrival/departure dates using half-open `[arrival, departure)`
  semantics;
- optional target expected arrival/departure times at minute precision;
- the complete target Inventory-unit set;
- expected Reservation details revision;
- authenticated actor provenance.

Only a `Confirmed` reservation with an active allocation may start an
amendment. Date-only, unit-only, expected-time-only, and combined changes use
the same protocol. The existing unit-reassignment surface remains compatible.

The slice deliberately excludes:

- checked-in room moves;
- no-show, checkout, cancellation, or lifecycle reversal;
- guest/contact/notes changes;
- partial reservation or per-guest stays;
- group reservations, pooled capacity, rates, billing, and housekeeping.

## Durable Outcome

Each material staff amendment has a Reservations-owned operation record with a
privacy-minimal target and one of these outcomes:

- `Pending`: Inventory has not yet produced a committed correlated outcome;
- `Applied`: Inventory confirmed and Reservations applied the target once;
- `Rejected`: Inventory rejected and Reservations retained the old booking;
- `OutcomeUnknown`: historical evidence cannot prove applied versus rejected.

The record stores the target dates/times/unit identifiers, request-schema
version and fingerprint, expected details revision, operation version,
requested/completed timestamps, resulting Reservation revisions, rejection
code, and reconciliation audit coordinates. It stores no guest/contact data.

Existing historical inventory-amendment journal entries are migrated honestly:
the currently pending candidate can be reconstructed as `Pending`; older
terminal entries become `OutcomeUnknown` rather than being guessed.

## Idempotency And Concurrency

1. Authentication, property scope, processing restrictions, and the Reservation
   operation lock are evaluated before any replay is disclosed.
2. Reusing an operation identifier with different target data or expected
   revision returns a stable conflict.
3. Exact retry returns the stored amendment receipt while pending and after
   applied/rejected completion; it never returns an unrelated generic
   Reservation receipt.
4. A material first request writes the pending Reservation candidate, generic
   management coordinate, amendment operation, and outbox request atomically.
5. A desired state that is already current records an immediate `Applied`
   operation, reserving the idempotency coordinate without contacting
   Inventory.
6. Confirmation updates the Reservation, local Inventory projection, details
   history, and amendment operation in one inbox transaction.
7. Rejection updates only the pending candidate and amendment operation; every
   current stay field and allocation remains unchanged.
8. Cancellation, check-in, no-show, guest updates, and another amendment
   serialize on the existing Reservation coordinate and fail closed while an
   amendment is pending.

## Recovery And Operator Usability

- Authorized readers can fetch one amendment receipt by exact property,
  reservation, and operation identifiers.
- A bounded, keyset-paged recovery query exposes `Pending` and
  `OutcomeUnknown` operations without guest PII.
- A separately permissioned reconcile command accepts the observed operation
  version. It works only for `Pending`, enforces a minimum retry interval, and
  republishes the exact durable Reservation candidate under the same internal
  Inventory request identifier while preserving the caller's local operation
  identifier for status and replay.
- Inventory then replays its existing committed decision or evaluates the
  still-new request exactly once. Reconcile never creates a different
  amendment and never mutates a terminal operation.
- Receipts expose whether recovery is currently eligible and the next eligible
  timestamp. Operators are not told that asynchronous work is complete while
  it is pending.

## Persistence

Add a Reservations-owned stay-amendment operation table keyed by tenant,
reservation, and operation identifier, with a one-to-one relationship to the
existing management journal. Add:

- exact target/outcome/timestamp/revision check constraints;
- immutable request and monotonic outcome transition protection;
- an indexed `(ScopeId, PropertyId, Outcome, UpdatedAtUtc, OperationId,
  ReservationId)`
  recovery path;
- optimistic operation versioning;
- Data Rights and tenant-termination export/catalog bindings;
- tenant destruction through the existing Reservation-owned cascade/lifecycle;
- downgrade guards where exact outcome evidence cannot be represented.

Provider-neutral domain and application code remains free of PostgreSQL SQL.

## Contracts And Surfaces

- Add a dedicated stay-amendment request and receipt contract.
- Add public management amend/status/reconcile surfaces under the existing
  property-scoped Reservations `Manage`/`Read` permissions. Finer-grained
  recovery delegation remains a later policy slice so this Reservations-only
  branch does not change Workspaces access-profile composition.
- Add equivalent Admin API and Admin CLI commands; destructive-looking or
  recovery actions require explicit Admin confirmation.
- Preserve the current inventory-only reassignment route and command behavior,
  but route new material operations through the same durable outcome model.
- Keep list responses bounded and cursor based. Never return guest/contact data
  from the recovery queue.

## Verification

- Domain tests cover date-only, unit-only, time-only, combined, no-op, invalid
  range, stale revision, pending conflict, exact replay, changed reuse, and every
  forbidden lifecycle state.
- Confirmation changes dates/times/units and advances details revision once;
  rejection changes none of them.
- Fingerprints bind reservation, operation, expected revision, dates, times,
  and sorted units.
- Outcome handlers reject stale/mismatched facts and atomically update the
  aggregate, projection, history, and amendment operation.
- Reconcile is version checked, rate bounded, and emits one exact replay request
  per accepted attempt.
- PostgreSQL migration/backfill tests cover pending and historical operations,
  malformed shapes, transition attacks, cross-tenant ids, indexes, and
  downgrade behavior.
- A real PostgreSQL/JetStream scenario proves extension and unit change through
  HTTP, old-truth-while-pending, confirmation, rejection, exact retry, worker
  restart, lost-outcome reconciliation, and no second Inventory mutation.
- Public/Admin authorization, API error mapping, Admin CLI confirmation,
  exports/catalogs, migration drift, architecture boundaries, and the complete
  repository gate pass.

## Completion Boundary

Implementation and local/provider proof do not establish hosted readiness.
The draft PR must distinguish code evidence from deployed migration, worker
health, queue/backlog, backup/restore, and operator rehearsal evidence.

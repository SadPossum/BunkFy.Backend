# Properties Room Mutation Idempotency Task

Status: complete
Date: 2026-08-07

## Goal

Make direct room creation and details updates safe to retry after an uncertain
response. One logical request must commit at most one room/version/event and
return the original immutable receipt on an exact replay, while changed reuse
of its operation id fails closed.

## Audit Finding

Properties already serializes room creation on the Property aggregate and room
updates on the Room aggregate. Both commands still lack caller-owned operation
identity. A committed room creation whose response is lost is retried as a
duplicate name or stale Property version. A committed room update is retried as
a stale Room version. Re-submitting unchanged room details also creates a new
version and event.

Room retirement is intentionally different. The direct Properties command is a
routing guard and never changes topology; the durable drain/finalization
process is owned by Inventory and identified by its topology change id. This
slice must not duplicate that workflow inside Properties.

## Ownership

- Properties owns normalized room definitions, operation equivalence,
  immutable room receipts, topology events, and operation export/destruction.
- Public API, Admin API, Admin CLI, and web callers supply one non-empty
  operation id for each logical create or update attempt.
- Workspaces remains the authority for operational admission through the
  existing Properties aggregate-lock path.
- Inventory remains the authority for room retirement, draining, retry, and
  finalization. Its topology change id remains that workflow's durable identity.
- GMA continues to own transactional dispatch, transaction-key locking,
  optimistic concurrency, and outbox behavior. Room fingerprints and receipts
  remain Properties-specific.

## Invariants

1. The existing Properties mutation journal becomes resource-aware. Its
   coordinate is `(ScopeId, ResourceKind, ResourceId, OperationId)`.
2. Existing property details and lifecycle operations use the Property
   resource. Room creation also uses the Property resource because it advances
   the Property version. Room updates use the Room resource because that is the
   locked aggregate and version precondition.
3. A non-empty operation id and a valid normalized room definition are required
   before aggregate locking or journal lookup.
4. Every attempt acquires workspace operational admission and the appropriate
   aggregate lock before reading a receipt.
5. Exact replay returns the immutable stored room receipt before current
   lifecycle, version, or uniqueness checks and emits no additional event.
6. Reusing an operation id for another mutation kind, expected version, target,
   or normalized room definition within the same resource returns the existing
   management-operation conflict mapped to HTTP 409.
7. Invalid, unauthorized, unavailable, missing, retired, stale, or duplicate
   room-name attempts do not bind the operation id.
8. Successful room creation stores the generated room receipt, resulting
   Property resource version, room event, room, and operation row in one
   Properties transaction.
9. An unchanged room update stores one successful receipt without advancing the
   Room version or publishing an event. A material update acquires the
   normalized destination-name coordinate and checks uniqueness only when the
   normalized name changes, then mutates and records its receipt atomically.
10. The journal stores only operation/resource coordinates, mutation kind,
    expected version, a canonical SHA-256 fingerprint, typed result
    coordinates/status/version, and completion time. It stores no raw room
    labels.
11. Resource-aware journal rows remain append-only, tenant-filtered, exported,
    bounded during tenant destruction, and denied to a closing workspace even
    on exact replay.

## Efficiency And Persistence

Extend the existing journal instead of adding a competing operation table. Add
resource coordinates and conditional room-receipt fields, backfill existing
rows as Property resources, and key rows by their resource coordinate. The
existing Property lock serializes all Property-scoped attempts; the existing
Room lock serializes all Room-scoped attempts. This avoids a second operation
lock and lets unrelated rooms remain concurrent.

The migration must preserve existing property receipts, constrain valid
resource/kind/result combinations, and keep migration drift clean. The
Properties tenant export schema is bumped because its operation-record shape
widens. Future bed mutations may extend the Room-resource journal without
changing this slice's semantics.

## Surfaces

- Add `OperationId` to create/update room commands and public/Admin request
  contracts.
- Require `--operation-id` for Admin CLI room create and update.
- Keep one browser operation id and the attempt's original expected version
  while the same normalized form is retried. Clear it after success,
  cancellation, target change, or normalized-value change.
- Keep `RoomMutationReceiptDto`; clients refetch authoritative Property and Room
  state after a successful receipt.

## Verification

- Domain and application tests cover normalized no-op updates, exact immutable
  replay, changed/kind reuse, failed-attempt reuse, stale versions, duplicate
  names, admission-before-replay, and one event/receipt.
- API, Admin API, Admin CLI, generated contracts, and web tests cover required
  operation ids and stable browser attempts.
- Persistence and lifecycle tests cover resource-aware keys and checks,
  append-only enforcement, tenant filtering, export, and destruction.
- One focused PostgreSQL scenario covers concurrent exact creation/update,
  generated-id replay, no-op handling, failed-attempt reuse, event cardinality,
  and closing-workspace admission.
- Use focused cheap checks while editing. Run one targeted Docker scenario and
  one consolidated non-Docker gate at slice completion.

Completion evidence (2026-08-07):

- the focused PostgreSQL migration/concurrency scenario passed (`1/1`);
- the consolidated backend gate passed with a zero-warning solution build,
  clean migration drift, `158` Properties tests, `54` non-Docker integration
  tests, and all architecture/source-package checks;
- web type checking, lint, `207` tests, production build, OpenAPI generation,
  and generated-contract drift checks passed;
- all mounted GMA repositories remained clean and required no framework change.

## Not In This Slice

- bed creation, batch creation, update, or retirement replay safety;
- room retirement, draining, retry, or finalization policy owned by Inventory;
- changing expected-version semantics or introducing implicit merge behavior;
- generic GMA idempotency, response caching, or an operation-journal package.

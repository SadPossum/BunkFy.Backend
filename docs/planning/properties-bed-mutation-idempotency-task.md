# Properties Bed Mutation Idempotency Task

Status: complete
Date: 2026-08-07

## Goal

Make direct single-bed addition, atomic batch addition, and bed-label updates
safe to retry after an uncertain response. One logical request must commit at
most one set of beds, Room version sequence, event sequence, and immutable
receipt. Changed reuse of an operation id must fail closed.

## Audit Finding

All three commands already serialize on the Room aggregate, but none has
caller-owned operation identity. A committed add retried after a lost response
fails as a duplicate label or stale Room version, and a committed update fails
as stale. Batch retries can generate an entirely new set of bed and event ids.
Updating a bed to its current normalized label also advances both Bed and Room
versions and publishes an event. Batch add additionally lacks a command
validator, although its handler performs partial defensive checks.

Direct bed retirement is intentionally excluded. The Properties command is a
routing guard and never mutates topology. Inventory owns the durable draining,
retry, and finalization process identified by its topology change id.

## Ownership

- Properties owns normalized bed labels, Room aggregate invariants, operation
  equivalence, immutable bed/batch receipts, topology events, and journal
  export/destruction.
- Public API, Admin API, Admin CLI, and web callers supply one non-empty
  operation id per logical add, batch-add, or update attempt.
- Workspaces remains the operational-admission authority through the existing
  Properties Room-lock path.
- Inventory remains the authority for bed retirement, reservation/block impact,
  draining, retry, and finalization.
- GMA continues to own transactional dispatch, transaction-key locking,
  optimistic concurrency, and outbox behavior. Bed fingerprints and receipts
  remain Properties-specific.

## Invariants

1. Single add, batch add, and update use the existing mutation journal with the
   coordinate `(ScopeId, Room, RoomId, OperationId)` because each operation is
   serialized by and advances the Room aggregate.
2. A non-empty operation id and syntactically valid normalized labels are
   required before Room locking or journal lookup. Batch size and normalized
   duplicate checks also happen before locking.
3. Every attempt acquires workspace operational admission and the Room lock
   before reading a receipt.
4. Exact replay returns the immutable stored receipt before current Room/Bed
   lifecycle, version, or duplicate-label checks and emits no additional event.
5. Reusing an operation id for another mutation kind, expected Room version,
   bed target, or normalized label sequence in the same Room returns the
   existing management-operation conflict mapped to HTTP 409.
6. Batch order is part of operation equivalence. Whitespace normalization is
   ignored, but reordered labels are treated as changed reuse.
7. Invalid, unauthorized, unavailable, missing, retired, stale, duplicate, or
   oversized attempts do not bind the operation id or consume generated ids.
8. Single add stores its generated Bed receipt and resulting Room version in
   the same transaction as the Bed, Room update, event, and outbox message.
9. Batch add stores affected count and resulting Room version in the same
   transaction as every generated Bed and ordered event. The resulting Room
   version must equal expected version plus affected count.
10. Updating to the current normalized label stores one successful receipt
    without advancing Bed or Room versions and without publishing an event. A
    material update advances each version exactly once.
11. The journal stores only operation/resource coordinates, mutation kind,
    expected version, a canonical SHA-256 fingerprint, typed result
    coordinates/status/version/count, and completion time. It stores no label.
12. Bed journal rows remain append-only, tenant-filtered, exported, bounded
    during tenant destruction, and denied to a closing workspace even on exact
    replay.

## Persistence

Extend the resource-aware journal rather than introduce a bed-specific table.
Add nullable typed Bed id/status and batch-count result fields, widen operation
kinds and receipt-shape constraints, and retain the existing Room resource key.
Existing rows require no data rewrite. The migration must:

- constrain property, room, bed, and batch receipt shapes independently;
- require single add to advance Room version by one;
- require batch add to advance Room version by affected count;
- permit unchanged room/bed updates to retain the expected resource version;
- refuse downgrade while any bed mutation receipt exists;
- keep migration drift, tenant export, and destruction proofs deterministic.

The Properties tenant export schema and contributor catalog version advance
because the journal record shape widens. No personal-data catalog change is
needed because labels are represented only by a one-way fingerprint.

## Surfaces

- Add `OperationId` to single-add, batch-add, and update commands and to their
  public/Admin request contracts.
- Require `--operation-id` for Admin CLI bed add, add-many, and update.
- Add a batch command validator covering ids, version, cardinality, and labels.
- Keep one browser operation id and the attempt's original Room version while
  the same normalized form is retried. Clear it after success, cancellation,
  target change, action change, or normalized-value change.
- Keep existing Bed and batch receipt contracts; callers refetch authoritative
  topology after success.

## Verification

- Domain/application tests cover no-op update, generated-id replay, exact
  batch replay, changed/kind/target reuse, failed-attempt reuse, validation,
  stale versions, duplicate labels, admission-before-replay, and event counts.
- API, Admin API, Admin CLI, generated contracts, and web checks cover required
  operation ids and stable browser attempts.
- Persistence tests cover typed receipt shapes, resource-aware lookup,
  append-only enforcement, tenant filtering, export, and destruction.
- One focused PostgreSQL scenario covers concurrent exact single/batch add and
  update, generated-id replay, no-op handling, event cardinality, and closing
  workspace admission.
- Use focused cheap checks while editing. Run one targeted Docker scenario and
  one consolidated non-Docker gate at slice completion.

## Not In This Slice

- bed retirement, draining, retry, or finalization owned by Inventory;
- room mutation semantics completed in the preceding slice;
- changing Room expected-version semantics or introducing implicit merges;
- generic GMA idempotency, response caching, or a framework journal package.

## Completion Evidence

- The focused Properties bed, API, persistence, export, and destruction set
  passes 48/48 tests.
- The PostgreSQL migration/concurrency/admission scenario passes 1/1.
- `eng/verify.ps1 -SkipRestore` reports a synchronized solution, clean source
  packages, a zero-warning build, no migration drift, and 4,775 passing
  non-Docker tests.
- The web completion gate passes typecheck, lint, 210 tests, and the production
  build. OpenAPI and generated TypeScript contracts are current.

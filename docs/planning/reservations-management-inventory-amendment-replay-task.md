# Reservations Management Inventory Amendment Replay Task

Status: complete

## Goal

Make staff inventory reassignment safe to retry after Inventory has already
confirmed or rejected the asynchronous amendment, without publishing another
allocation request or silently accepting changed idempotency-key reuse.

## Audit Finding

`AmendmentRequestId` and the canonical request fingerprint currently deduplicate
only while an amendment is pending on the Reservation aggregate. Both pending
coordinates are cleared by confirmation and rejection. A later retry can then
start the same amendment again; rejection is especially vulnerable because it
does not advance the details revision.

Adapter amendments are already covered by the durable external-operation
receipt coordinator. This task applies only to public, Admin API, and Admin CLI
staff management requests.

## Ownership

- Reservations owns the management operation coordinate, request equivalence,
  aggregate lock, retry result, and outbox exactly-once boundary.
- Inventory remains authoritative for selection validation, allocation
  concurrency, and amendment confirmation or rejection.
- API and CLI callers continue to supply one stable amendment request id per
  logical attempt.
- GMA remains unchanged; the generic transactional command, inbox/outbox, and
  database-conflict primitives already provide the required substrate.

## Invariants

1. The operation coordinate remains `(ScopeId, ReservationId,
   AmendmentRequestId)` and shares the Reservations management journal with
   lifecycle and guest-detail operations.
2. Current authentication, permission, tenant, property, and reservation
   processing checks run before a replay is visible.
3. The reservation mutation lock is acquired before reading the aggregate and
   journal.
4. A first material request validates the current Inventory projection, starts
   the pending amendment, appends the management journal row, and publishes the
   outbox request in one transaction.
5. The journal stores only operation metadata plus the existing canonical
   SHA-256 fingerprint of reservation id, request id, expected details revision,
   and sorted inventory-unit ids. It stores no guest name or contact payload.
6. Exact retry while pending, confirmed, rejected, or after unrelated later
   writes returns the current minimal reservation receipt without validating
   Inventory again or emitting another event.
7. Reusing the coordinate with a different expected details revision or unit
   selection returns the stable management-operation conflict.
8. Invalid, denied, stale, and unchanged requests do not reserve an operation
   id. A legacy pending exact retry may backfill its missing journal row without
   emitting another event.
9. The fingerprint is classified and exported with the management journal,
   follows that journal's reservation-bound retention and tenant destruction,
   and does not extend the retained inventory topology already present in the
   reservation and details history.
10. Adapter-originated amendments continue to use the external-operation
    receipt and never write the management journal.

## Persistence

Add an `InventoryAmendment` management operation kind and nullable 64-character
request fingerprint. PostgreSQL constraints require the details-revision plus
fingerprint shape only for this kind, preserve existing lifecycle and
guest-detail rows, and reject mixed shapes.

Subject and tenant exports advance their schema and management-operation record
versions. The executable personal-data catalogue and generated inventory
classify the new persisted fingerprint.

## Verification

- Nine focused application tests cover first request, current and legacy
  pending replay, confirmed replay, rejected replay, changed reuse conflict,
  unchanged non-binding, adapter isolation, and lock-before-journal ordering.
- Model, subject-export, tenant-export, and executable-catalogue tests cover
  the extended kind and record shape. The existing public API, Admin API, and
  Admin CLI continue to carry the explicit stable amendment request id.
- All 237 Reservations module tests pass. The Integration test host builds
  with zero warnings and errors, and Entity Framework reports no pending model
  changes.
- One real-PostgreSQL upgrade from
  `20260806233308_ExtendReservationManagementOperationsForGuestDetails`
  preserves existing rows, accepts the canonical kind 6 shape, and rejects
  null, cross-kind, and non-canonical fingerprints.

## Outcome

Staff inventory reassignment now reserves its request coordinate and canonical
fingerprint in the Reservations management journal in the same transaction as
the pending aggregate state and outbox request. Exact retries return the
current minimal receipt after confirmation, rejection, or later writes without
consulting Inventory or publishing again. Existing pending staff requests are
backfilled safely, while adapter-originated amendments remain exclusively in
the external-operation journal.

Subject export schema version 6 and tenant export schema version 4 emit
management-operation record version 3. Personal-data catalogue version 14
classifies the retained SHA-256 as an elevated pseudonymous identifier with the
same reservation-bound retention and destruction lifecycle as its journal.

## Deferred

- Stay date and guest-detail amendments through the same Inventory saga need
  explicit product policy before their public management surface expands.
- Cross-module Guest Record creation plus reservation linking remains a
  separate orchestration slice.

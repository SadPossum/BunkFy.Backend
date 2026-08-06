# Reservations Management Create Idempotency Task

Status: complete

## Goal

Make staff-created reservations safe to retry after an uncertain response,
without creating duplicate bookings or retaining an additional fingerprint of
guest personal data.

## Ownership

- Reservations owns operation equivalence, reservation identity, lock ordering,
  and replay results because these rules depend on reservation semantics.
- The public API, Admin API, Admin CLI, and web client must all supply one
  non-empty operation id for a logical create attempt.
- GMA continues to own the transactional command pipeline, database exception
  classification, optimistic concurrency, and outbox delivery. No reservation
  request shape or guest-data comparison belongs in GMA.

## Invariants

1. The operation id is also the reservation id and its serialization
   coordinate.
2. Creation acquires the coordinate lock before checking for an existing
   reservation and before making an absent-record creation decision.
3. An equivalent retry returns the current reservation mutation receipt and
   does not repeat Inventory validation, creation events, or persistence.
4. Reusing an operation id for a different normalized request returns a stable
   conflict.
5. Equivalence ignores inventory-unit ordering and insignificant surrounding
   whitespace, but includes the property, stay, units, guest details, source,
   and notes.
6. Equivalence is evaluated from the aggregate and a transient normalized
   value object. No permanent PII fingerprint or duplicate request payload is
   stored.
7. A new aggregate is added beneath the acquired operation lock, so the
   repository does not provision the same lock twice.
8. Two simultaneous first attempts may yield one transient database conflict;
   the winner is unique and a retry deterministically replays it.

## Surfaces

- Add `OperationId` to the management create command and both HTTP request
  contracts.
- Require `--operation-id` in the Admin CLI so remote automation can safely
  retry.
- Keep one operation id for an unchanged web submission after transport or
  server uncertainty; allocate a new id when the normalized request changes.
- Map operation-reuse conflicts to HTTP 409 on both HTTP surfaces.

## Verification

- Focused application tests cover exact replay, normalized replay, changed
  payload conflict, current receipt replay, and lock-before-read ordering.
- Persistence coverage proves creation beneath a coordinate lock produces one
  lock row and initializes the processing-restriction projection.
- OpenAPI generation, the Admin CLI build, generated web contracts, and the web
  retry-attempt test cover the required field on every management surface.
- Use focused non-Docker checks while editing, then one non-Docker backend and
  web gate at slice completion. Docker and deployed lifecycle proof remain the
  next bounded slice.

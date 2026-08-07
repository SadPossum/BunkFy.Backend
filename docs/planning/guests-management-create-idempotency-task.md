# Guests Management Create Idempotency Task

Status: complete

## Goal

Make staff-created Guest Records safe to retry after an uncertain response,
without creating duplicate durable profiles or retaining another copy or
fingerprint of guest personal data.

This is the prerequisite for repairing the BunkFy-specific workflow that
creates a Guest Record and links it to an existing reservation. It does not
make those two module-owned writes one distributed transaction.

## Ownership

- Guests owns operation equivalence, Guest identity, serialization, and replay
  results because those rules depend on the canonical Guest aggregate.
- The public API, Admin API, Admin CLI, and web client must supply one non-empty
  operation id for one logical create attempt.
- The operation id is also the Guest id. The existing Guests operation-lock
  table remains the serialization mechanism; no request journal is added.
- Reservations continues to own reservation-to-Guest links. A later
  BunkFy-specific workflow may coordinate the two module contracts, but neither
  module may write the other's schema.
- GMA continues to own the transactional command pipeline, database exception
  classification, optimistic concurrency, and outbox delivery. Guest request
  semantics do not belong in GMA.

## Invariants

1. The operation id is also the Guest id and its tenant-scoped serialization
   coordinate.
2. Tenant, authorization, property scope, country policy, and ordinary write
   admission are evaluated on every attempt before replay can succeed.
3. Creation acquires the Guest coordinate lock before reading by Guest id and
   before making an absent-record creation decision.
4. The replay read is tenant-wide by Guest id. Property visibility must not
   turn an existing operation into a second creation attempt.
5. An equivalent normalized retry returns the current minimal mutation receipt
   and does not persist a second profile, repeat profile events, or initialize
   another restriction projection. The existing lock row may advance while
   serializing the attempt.
6. Reusing an operation id for a different origin property or normalized
   profile payload returns a stable conflict.
7. Actor identity is authenticated and validated on every attempt, but is not
   part of request equivalence and replay never rewrites original attribution.
8. Equivalence is evaluated from the aggregate and a transient normalized
   value object. No permanent PII fingerprint or duplicate request payload is
   stored.
9. A new aggregate is added beneath the acquired Guest lock so the repository
   does not provision or persist the same coordinate twice.
10. Two simultaneous first attempts have one durable winner. A retry after any
    transient uniqueness conflict deterministically replays that winner.

## Surfaces

- Add `OperationId` to the management create command and both HTTP request
  contracts.
- Require `--operation-id` in the Admin CLI so remote automation can safely
  retry.
- Keep one operation id for an unchanged standalone Guest form submission and
  for the Guest-creation leg of the reservation workflow; allocate a new id
  when the normalized profile or origin property changes.
- Map operation-reuse conflicts to HTTP 409 on both HTTP surfaces.
- Regenerate OpenAPI and web contracts after the backend contract settles.

## Persistence And Governance

- Reuse the existing tenant-scoped Guest operation-lock row and Guest primary
  key; no business-data migration is expected.
- The operation id is the canonical Guest identifier, not a new personal-data
  category or retained idempotency secret.
- Update the executable personal-data catalogue for newly exposed command and
  API fields, then regenerate its checked inventory.

## Verification

- Focused domain and application tests cover exact and normalized replay,
  changed-payload conflict, current receipt replay, authorization/policy before
  replay, and lock-before-read ordering.
- Persistence coverage proves creation beneath the acquired coordinate creates
  one Guest, one lock, and one restriction projection. The existing
  transactional command/outbox gate continues to cover domain-event delivery.
- OpenAPI generation, Admin CLI build, generated web contracts, and web attempt
  tests cover the required field on every management surface.
- Use focused non-Docker checks while editing, then one consolidated Guests and
  web gate at slice completion. Run a targeted PostgreSQL test only if existing
  lock coverage cannot prove the required behavior.

## Deferred Follow-Up

- Design the BunkFy-specific durable create-and-link workflow after this slice.
  It must resume either leg safely, surface recoverable partial progress, and
  never compensate by deleting a Guest Record that another workflow may have
  started using.

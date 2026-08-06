# Inventory Allocation Mutation Serialization Task

Status: complete

## Goal

Make allocation creation, semantic replay, amendment, release, anonymisation,
and restore observe one authoritative Inventory decision without turning
ordinary contention into database exceptions or allowing post-anonymisation
events to recreate erased correlations.

## Ownership

- Inventory owns allocation request idempotency, allocation operation ordering,
  availability fences, amendment decisions, and anonymisation-safe replay.
- Reservations owns reservation lifecycle and its local allocation projection.
  It must apply an Inventory confirmation only when the reservation accepts that
  continuation.
- GMA continues to own inbox/outbox delivery, command and message transactions,
  optimistic-concurrency mapping, and the provider-neutral transaction-key-lock
  primitive. No Inventory allocation policy belongs in GMA.
- The existing Inventory PostgreSQL migration already backfills one operation
  lock for every allocation. This slice changes lock behavior, not schema.

## Invariants

1. Allocation requests serialize on both allocation-request and reservation
   coordinates. The locks are narrow, transaction scoped, and acquired in one
   stable order, so unrelated reservations remain concurrent.
2. After request-coordinate acquisition, the handler reloads request and
   reservation matches from authoritative storage. A semantic duplicate does
   not enqueue another outcome; the original durable outbox owns delivery.
3. A new allocation and its operation-lock row are persisted atomically.
4. Every writer or replay path for an existing allocation acquires its
   pre-provisioned operation lock before authoritative reload. A missing lock
   for an existing allocation fails closed; a missing allocation remains an
   ordinary not-found decision.
5. Amendment decisions are read and replayed only after allocation locking.
   An anonymised allocation silently consumes stale allocation, amendment, and
   release requests instead of recreating decisions or outgoing correlations.
6. Restore retains an explicit coordinate-lock path because it may need to
   prove an allocation that is absent from the restored backup.
7. Relational steady-state acquisition uses one atomic lock-row revision
   update. Contenders wait and continue with a fresh aggregate instead of both
   loading the same revision and making one fail optimistically.
8. Inventory keeps the established lock order: tenant admission, allocation
   request coordinates when applicable, allocation operation lock, then sorted
   room/unit availability fences.
9. Reservations never marks its Inventory projection active when a stale
   confirmation is rejected by the current reservation state.

## Efficiency

- Two advisory locks are taken only for the same request or reservation during
  allocation creation; they do not serialize a property or tenant globally.
- Existing-allocation mutations issue one lock-row update followed by one
  authoritative aggregate query.
- Semantic duplicate requests no longer amplify outbox traffic.
- Room sales-mode, manual-block, allocation-conflict, and retirement safety
  continue to use their existing room/unit version fences and are not rebuilt
  in this slice.

## Delivery

1. Add the Inventory-owned allocation-request lock port and persistence adapter
   using GMA's existing transaction-key-lock primitive.
2. Split existing-allocation and restore-coordinate operation-lock semantics,
   and make the relational existing path atomic and fail closed.
3. Add an Inventory allocation mutation coordinator and route amendment,
   release, anonymisation, restore, and request replay through it.
4. Remove the unsafe pre-lock amendment replay and suppress stale request
   effects after anonymisation.
5. Make Reservations apply allocation projection state only after its aggregate
   accepts the confirmation.
6. Add focused unit, architecture, persistence, and PostgreSQL concurrency
   coverage.

## Deferred

- Persisting a separate immutable allocation-request decision journal. GMA's
  durable outbox already owns outcome delivery, so semantic duplicates are
  idempotent no-ops rather than response replays.
- Replacing room/unit optimistic availability fences with pessimistic locks;
  current no-overbooking and topology races are already covered by real
  PostgreSQL scenarios.
- Generalizing product resource coordinators into GMA before another project
  proves a shared application-level contract.

## Verification

- Unit tests prove request-lock ordering, lock-before-reload behavior,
  post-anonymisation suppression, and no pre-lock amendment decision read.
- Architecture coverage enumerates every existing-allocation writer and
  requires the shared coordinator, while restore keeps the coordinate path.
- Persistence tests prove atomic revision acquisition, fail-closed missing-lock
  behavior, and atomic new-allocation lock provisioning.
- One targeted PostgreSQL scenario proves competing acquisitions serialize and
  same-reservation allocation requests resolve without a unique-key or
  optimistic-concurrency failure.
- A Reservations regression test proves a stale confirmation cannot reactivate
  its Inventory projection.
- Run focused non-Docker tests during implementation, then one full non-Docker
  gate and only the targeted Docker scenario at slice completion.

## Completion evidence

- Inventory unit tests: 106 passed.
- Reservations unit tests: 205 passed.
- Architecture tests: 92 passed.
- Non-Docker integration tests: 54 passed.
- Full non-Docker verification completed after the task document was added to
  the documentation index; the corrected documentation guard and complete
  Architecture suite then passed.
- Targeted PostgreSQL allocation-contention scenario: 1 passed, proving
  same-reservation request and existing-allocation operation serialization.
- Integration project build and all touched project builds completed with zero
  warnings and zero errors.

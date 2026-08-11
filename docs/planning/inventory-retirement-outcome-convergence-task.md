# Inventory Retirement Outcome Convergence Task

Status: in progress

## Goal

Make room and bed retirement topology and finalization outcomes converge in one
delivery when Properties publishes the correlated facts close together. Broker
redelivery remains a recovery boundary, not the normal coordinator for two
commutative Inventory transitions.

The exact Preview Reservations and Inventory rehearsal exposed the current gap:
`room-retired-topology` and `room-retirement-finalized` were emitted about one
millisecond apart, both loaded the same retirement version, and one delivery
retried after an optimistic-concurrency conflict. The process recovered on
attempt 2, but normal completion should not depend on that retry.

## Ownership

- Properties owns physical room and bed topology and publishes topology plus
  correlated finalization or rejection facts.
- Inventory owns retirement-process state, outcome validation, idempotent
  transition order, and process-local mutation serialization.
- GMA owns message transactions, inbox/outbox delivery, transaction-key locks,
  retry policy, and safe terminal failure behavior. No hostel retirement policy
  or BunkFy event pairing belongs in GMA.
- Reservations remains the reservation and reassignment authority. This slice
  changes no reservation contract or state.

## Invariants

1. Room topology, finalized, and rejected handlers acquire the same room-
   retirement process coordinate before loading or mutating its aggregate.
2. Bed topology, finalized, and rejected handlers acquire the same bed-
   retirement process coordinate before loading or mutating its aggregate.
3. A topology handler discovers only the immutable process id before locking,
   then loads the authoritative tracked aggregate after lock acquisition.
4. Topology-first and finalization-first delivery both finish in `Completed`;
   duplicate finalized, rejected, or topology facts preserve the existing
   domain idempotency rules.
5. A topology fact without an Inventory retirement process still updates the
   topology projection. A correlated outcome without its durable process fails
   closed with the existing stable mismatch behavior.
6. Locks are transaction scoped and process local. Unrelated retirements and
   ordinary room work remain concurrent; no tenant-wide or room-wide lock is
   added for this outcome pair.
7. PostgreSQL optimistic concurrency remains the final corruption guard. The
   module adds no retry loop and does not weaken GMA broker redelivery.

## Efficiency

- Finalized and rejected outcomes already carry the topology-change id and take
  one process lock before their existing aggregate query.
- Topology facts add one indexed, no-tracking scalar lookup so they can acquire
  the matching process lock before aggregate hydration.
- No schema, migration, projection, public contract, or web change is required.

## Delivery

1. Add no-tracking process-id lookups for room and bed topology coordinates.
2. Centralize lock-before-load completion, finalization, and rejection behavior
   in Inventory-owned room and bed outcome coordinators.
3. Route all six retirement outcome handlers through those coordinators without
   changing Properties contracts or GMA behavior.
4. Add focused unit coverage for lock ordering, both event orders, duplicates,
   missing topology processes, and mismatched correlated outcomes.
5. Extend the existing PostgreSQL retirement workflow so one outcome holds the
   process lock while its paired delivery waits, then prove clean convergence.
6. Run focused checks during implementation, one complete non-Docker backend
   gate, one targeted Docker scenario, and one exact Preview rehearsal at the
   completed-slice boundary.

## Completion Evidence

Pending implementation and final slice verification.

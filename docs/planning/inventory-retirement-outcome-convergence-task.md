# Inventory Retirement Outcome Convergence Task

Status: complete

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

- Backend candidate `33b1cad26315f021734c33fc517ac98f606b0302`
  passed the zero-warning build and migration-drift checks, the 137-test
  Inventory suite, the 102-test architecture suite, and the remaining 60-test
  non-Docker integration tail. The focused PostgreSQL lock-order scenario also
  passed once under the slice's Docker gate.
- GitHub completed both backend candidate workflows successfully: Security
  Baseline run `31456841806` and validate run `31456841808`.
- Root candidate `ec423ec` passed repository security, product-image policy,
  latest-submodule, solution-graph, and operations evidence-contract guards.
  It ran as Preview release `preview-ec423ec`; API and Worker used the same
  backend image digest
  `sha256:ff5fa98dab4b310167339e40d5732289b2c74e463291f19e334920e97c41c05d`.
- The self-contained Preview onboarding rehearsal passed 11 checks, including
  its 11-check Reservations/Inventory child lifecycle. The child evidence is
  `.tmp/deployment-probes/preview-onboarding-20260811T040504Z.reservations-inventory.json`;
  the umbrella evidence is
  `.tmp/deployment-probes/preview-onboarding-20260811T040504Z.json`.
- The rehearsal emitted the room-retirement facts about 1.5 milliseconds
  apart. Inventory inbox record `52314f15-46e6-4069-9e4d-ec07dded8d73`
  (`room-retirement-finalized`) and record
  `40f2cbd3-1daf-4499-9a66-d75dcf2578db`
  (`room-retired-topology`) both reached `Processed` on attempt 1 with no last
  error. The durable retirement process reached `Completed` at
  `2026-08-11T04:06:18.952451Z`.
- Fresh candidate Worker logs contained no matching message failure, retry,
  concurrency, or exception entry. Broker redelivery was not needed for the
  normal paired outcome.

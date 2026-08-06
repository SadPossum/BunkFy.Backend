# Reservations Mutation Serialization Task

Status: complete

## Goal

Make every Reservations-owned decision that depends on one reservation observe
one authoritative version under the existing per-reservation operation lock.
This closes races between ordinary management, data-rights restrictions,
adapter changes, Inventory saga continuations, retention, and due reminders.

## Ownership

- Reservations owns the lock coordinate, mutation ordering, restricted versus
  required-continuation reads, reminder suppression, and aggregate reloads.
- Inventory, Guests, Properties, Ingestion, and Operations Notifications remain
  integrated through their Contracts and local Reservations projections.
- GMA continues to own command and inbox transactions, optimistic-concurrency
  mapping, outbox/inbox delivery, and the generic database exception classifier.
  No Reservations vocabulary or lock policy belongs in GMA.
- Provider-specific lock backfill belongs in the Reservations PostgreSQL
  migrations project. Domain and application code remain provider agnostic.

## Invariants

1. A newly created reservation and its operation-lock row are persisted in the
   same Reservations unit of work.
2. The migration backfills one lock for every existing reservation. A missing
   lock for an existing reservation fails closed instead of being repaired by
   an ordinary request.
3. Ordinary mutations acquire the lock, then reload through the ordinary
   restriction-gated repository path. If restriction wins, the mutation sees
   the record as unavailable and cannot apply stale state.
4. Data-rights decisions acquire the same lock, then reload through the named
   data-rights path and bind their exact expected versions.
5. Already-started Inventory allocation and release outcomes use the named
   required-continuation path after locking, so restriction cannot strand an
   in-flight consistency workflow.
6. Adapter detail and amendment requests use the ordinary path. The explicitly
   safety-reducing external cancellation path uses required continuation.
7. Reminder claims scan without tracking, lock candidate reservations in stable
   id order, then reload reminder and restriction state before committing a due
   event. A restriction that wins suppresses the reminder; a competing dispatch
   is observed after the lock and cannot enqueue a duplicate.
8. Restore replay keeps a separate coordinate-lock operation. It may need to
   serialize proof for a reservation absent from a restored backup, so the lock
   table intentionally has no aggregate foreign key and remains explicitly
   removed by tenant termination.

## Efficiency

- ID-addressed mutations issue one lock-row update followed by one authoritative
  aggregate query; they no longer pre-read and then reload.
- Lock rows are pre-provisioned, so the steady-state path does not insert or
  recover from unique-key races.
- Reminder batches acquire only the distinct candidate reservation locks, in a
  deterministic order, and retain the existing bounded batch size.
- Expected-version conflicts remain product results. Broad retries are not
  added around workflows with cross-module coordination or outbox effects.

## Verification

- Unit tests prove post-lock ordinary and required-continuation reload behavior.
- Architecture coverage enumerates every existing-reservation aggregate writer
  and requires the shared serialization boundary.
- Reminder tests prove a restriction becoming effective at lock acquisition
  prevents the due event and a dispatch committed after the candidate scan is
  not emitted twice.
- Migration coverage proves legacy reservations receive locks.
- One focused PostgreSQL/NATS restriction workflow proves the real command
  pipeline uses the pre-provisioned lock while ordinary restricted work remains
  denied and post-release work succeeds.
- Run focused non-Docker tests during implementation, then one full non-Docker
  gate and only the targeted Docker scenario at slice completion.

# Properties Mutation Serialization Task

Status: complete

## Goal

Serialize property and room aggregate mutations so concurrent operators and
Inventory topology-finalization messages resolve against authoritative state
instead of leaking EF concurrency or unique-constraint exceptions.

## Ownership

- Properties owns property codes, room names, topology aggregate ordering,
  governance transitions, and topology-finalization decisions.
- Inventory continues to own whether a room or bed can be drained. Its
  finalization request is only an input to a Properties-owned room mutation.
- GMA continues to own command/inbox transactions and the provider-neutral
  transaction-key-lock primitive. No Properties naming or topology policy is
  added to GMA.
- Properties owns its operation-lock rows through migration, backfill, and
  tenant destruction. They are internal coordination state and are not added
  to personal-data exports.

## Invariants

1. Every mutation of an existing Property acquires its pre-provisioned
   property operation lock before authoritative reload.
2. Every mutation of an existing Room or owned Bed acquires the room operation
   lock before authoritative reload. Beds do not introduce a second aggregate
   lock.
3. Creating a Property or Room persists its operation lock atomically with the
   new aggregate. A missing lock for an existing aggregate fails closed; an
   absent aggregate remains an ordinary not-found result.
4. Property-code and property-local room-name uniqueness checks acquire narrow,
   transaction-scoped locks over the normalized unique coordinate before the
   authoritative existence check.
5. Existing aggregate locks are acquired before optional uniqueness-coordinate
   locks. No Properties operation acquires two existing aggregate locks, so
   the order is stable and cannot form an aggregate-to-aggregate cycle.
6. CreateRoom serializes on the Property because RegisterRoom advances the
   Property version. A concurrent property retirement therefore cannot admit a
   room after retirement.
7. Inventory bed/room finalization messages serialize with operator room and
   bed edits, then decide from a freshly loaded room graph.
8. Client expected versions remain domain preconditions. Contention waits for
   the authoritative state and returns VersionConflict when the submitted
   version is stale instead of surfacing a persistence exception.
9. Property and room operation-lock revisions advance only in successful
   transactions; failed domain decisions roll back their lock acquisition.

## Efficiency

- Existing aggregate mutation uses one indexed lock-row update followed by one
  aggregate reload.
- Unique-coordinate advisory locks are taken only for property-code and
  property-local room-name writes; unrelated properties and rooms remain
  concurrent.
- Room-owned bed operations share the room lock, matching the aggregate
  boundary and avoiding per-bed coordination rows.
- No tenant-wide lock is added beyond the existing admission and revision
  controls.

## Delivery

1. Add typed property and room operation-lock models, mappings, ports, and a
   Properties mutation coordinator.
2. Pre-provision operation locks in PropertyRepository and RoomRepository.
3. Route property, room, bed, governance, retirement, and topology-finalization
   writers through lock-before-reload behavior.
4. Add normalized code/name coordinate locking around uniqueness checks.
5. Add a PostgreSQL migration that creates and backfills the lock rows, updates
   tenant-destruction stages safely, and keeps migration drift clean.
6. Extend tenant destruction and verification to remove both lock sets and
   prove that no orphaned coordination state remains.
7. Add focused unit, architecture, persistence, and real PostgreSQL contention
   coverage.

## Deferred

- Serializing read-only Properties queries; they retain optimistic snapshots.
- Moving aggregate-specific coordinators into GMA. The reusable primitive is
  already generic, while resource identity and lock order remain product
  policy.
- Changing expected-version API contracts or introducing implicit merge
  semantics for concurrent operator edits.

## Verification

- Unit tests prove lock-before-reload ordering and enumerate all existing
  aggregate writers that must depend on the shared coordinator.
- Persistence tests prove atomic lock provisioning, monotonic acquisition,
  fail-closed missing-lock behavior, and normalized uniqueness coordinates.
- Migration drift and tenant-destruction tests cover lock backfill and removal.
- Focused command tests prove stale expected versions return the Properties
  domain conflict after lock-and-reload. Targeted PostgreSQL scenarios prove
  aggregate and unique-coordinate acquisitions wait on competing transactions.
- Focused cheap tests run while implementing. The full non-Docker gate and the
  targeted Docker scenario run once at the coherent slice boundary.

## Result

- Property and Room operation locks are pre-provisioned atomically, acquired
  before reload, and shared by all Bed mutations inside the Room aggregate.
- Normalized property-code and room-name coordinates use GMA's existing
  transaction-key-lock primitive without moving product policy into GMA.
- Migration `20260806084835_AddPropertiesOperationLocks` backfills existing
  topology, remaps the old completed destruction stage, and guards downgrade
  across the newly introduced cleanup stages.
- Properties tenant destruction removes and proves absence of both lock sets;
  personal-data exports intentionally omit this internal coordination state.
- Verification passed with 115 Properties tests, 92 architecture tests, 54
  non-Docker integration tests, five targeted PostgreSQL cases, clean migration
  drift, source-package guards, and a zero-warning serial solution build.

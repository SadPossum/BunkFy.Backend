# Ingestion Execution Mutation Serialization Task

Status: complete

## Goal

Make adapter connection and run decisions observe authoritative execution state
under transaction-scoped locks, without serializing unrelated connections or
turning ordinary adapter concurrency into persistence exceptions.

## Ownership

- Ingestion owns adapter configuration, execution admission, run identity,
  checkpoint progression, remote lease epochs, and the lock order between
  those resources.
- Adapter processes remain implementation-agnostic clients of Ingestion
  Contracts. Local task runners, remote workers, and future standalone services
  use the same execution protocol and do not own database coordination.
- GMA continues to own command and inbox transactions, optimistic-concurrency
  translation, and the provider-neutral shared/exclusive transaction-key-lock
  primitive. Ingestion resource names and lock policy do not belong in GMA.
- The source graph has a separate Ingestion-owned coordinate used by receipts,
  source links, proposals, reprocessing, and anonymisation. Hardening that plane
  is the next Ingestion slice rather than being mixed into execution control.

## Invariants

1. Lock order is task-execution coordinate when present, then adapter
   connection, then run, then source coordinate. No workflow acquires these in
   reverse order.
2. Connection and run writers acquire exclusive locks before authoritative
   reload. Operator expected versions remain domain preconditions and stale
   submissions return `VersionConflict` after waiting for the winner.
3. Observation and parser workflows use shared connection/run fences when they
   only inspect execution state. Concurrent observations remain concurrent,
   while disable, reconfiguration, completion, and lease changes wait for
   already-admitted work.
4. Remote observation authorization mutates the connection lease heartbeat and
   therefore takes the exclusive connection path.
5. Task-run creation serializes both its task execution identity and connection
   identity before checking replay and active-run state. Remote lease claims
   serialize on the connection before expiring, renewing, or creating a run.
6. Multi-record execution transitions acquire connection before run and reload
   both after their locks. Disabling a leased connection, renewing a lease, and
   completing a remote run cannot commit a split-brain pair.
7. Execution locks require the current tenant scope and an active relational
   transaction. Invalid or cross-tenant coordinates fail closed.
8. Transaction-key locks are internal coordination, not module data. They add
   no projection, export, retention, migration, or tenant-destruction surface.

## Efficiency

- Lock resources are scoped by tenant and connection, run, or task execution;
  unrelated adapters never contend.
- Read fences are shared, so ordinary ingestion fan-in does not become one
  observation at a time per connection.
- Lock acquisition adds no table rows or migration. PostgreSQL advisory locks
  and SQL Server application locks are held only by the existing command/inbox
  transaction.
- Existing bounded persistence retry remains a fallback for idempotent unique
  races. Correctly coordinated execution paths do not rely on retries for
  normal contention.

## Delivery

1. Add an Ingestion execution-lock port, persistence adapter, and coordinator
   with explicit shared and exclusive operations.
2. Route connection administration, task-run start/completion, checkpoint
   changes, remote lease workflows, and execution-sensitive observation reads
   through lock-before-reload behavior.
3. Replace tracked active-run discovery before mutation with id-only discovery,
   then lock and authoritatively load the selected run.
4. Add architecture coverage for every connection/run writer and focused tests
   for lock order, stale expected versions, invalid coordinates, and real
   PostgreSQL contention.

## Deferred

- Making the existing source-operation lock atomic and routing all receipt,
  source-link, proposal, reservation-outcome, reprocessing, and anonymisation
  writers through lock-before-reload. That is the next Ingestion slice.
- Moving application coordinators or Ingestion lock order into GMA. The generic
  transaction-key mechanism is already reusable; product resource policy is
  not.
- Replacing the durable run and receipt records with an external workflow
  engine or adapter-specific state.

## Verification

- Unit tests prove shared/exclusive selection, stable multi-resource order, and
  lock-before-reload behavior.
- Architecture tests enumerate execution-state writers and require the shared
  coordinator.
- Focused command tests prove stale operator versions return the Ingestion
  domain conflict after a competing mutation.
- One targeted PostgreSQL scenario proves connection/run contenders wait and
  continue from authoritative state without an EF concurrency exception.
- Run focused non-Docker tests during implementation, then one full non-Docker
  gate and only the targeted Docker scenario at slice completion.

## Result

- Execution-sensitive connection and run workflows now acquire tenant-scoped
  shared or exclusive transaction locks before authoritative aggregate reload.
  Task-run creation additionally serializes its task execution coordinate.
- ID-only run discovery is an explicit repository obligation, so active-run and
  replay checks cannot accidentally track mutable state before its lock.
- The GMA transaction-key primitive was sufficient. This slice adds no GMA
  change, module table, migration, export, retention, or destruction surface.
- Focused execution coordination coverage passed 24 tests, and the complete
  Ingestion project passed 305 tests.
- The targeted PostgreSQL contention scenario passed without skip. It proves
  shared read fan-in, exclusive writer waiting, unrelated-coordinate progress,
  and stale operator conflict translation after authoritative reload.
- `eng/verify.ps1 -SkipRestore` passed the synchronized solution and
  source-package guards, a zero-warning full build, all migration-drift checks,
  92 architecture tests, 54 non-Docker integration tests, and 4,496 total
  non-Docker tests.
- Source-graph serialization remains intentionally deferred to the next
  Ingestion slice described above.

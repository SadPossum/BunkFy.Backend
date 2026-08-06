# Ingestion Source Graph Mutation Serialization Task

Status: complete

## Goal

Make every mutation of one external reservation source graph continue from
authoritative state under one transaction-scoped coordinate, while unrelated
sources and adapters remain concurrent.

## Ownership

- Ingestion owns the reservation source graph: observation receipts, the
  reservation source link, dispatches, change proposals, reprocessing attempts
  and outputs, and Ingestion anonymisation state.
- The graph coordinate is the deterministic `ReservationSourceLink.Id`. It is a
  product concept and remains in Ingestion.
- GMA owns the provider-neutral shared/exclusive transaction-key-lock primitive
  and command/inbox transaction boundaries. No source-link naming, graph
  locator, or Ingestion lock order belongs in GMA.
- Reservation and adapter modules continue to interact through contracts and
  integration events. They do not acquire or understand Ingestion graph locks.

## Invariants

1. Lock order remains task execution, adapter connection, run, then source
   graph. A workflow that needs connection and source state acquires the
   connection fence first.
2. A source writer acquires an exclusive tenant-scoped source coordinate before
   loading any mutable graph aggregate it will decide from or change.
3. Pre-lock discovery reads only immutable identity data with no tracking.
   Receipts, proposals, dispatches, attempts, and source links are reloaded
   authoritatively after the lock.
4. Receipt admission, normalized dispatch, reservation outcomes, proposal
   decisions, reprocessing, raw-payload lifecycle, sensitive-history
   redaction, and anonymisation use the same source coordinate.
5. Batch retention first discovers bounded candidates, acquires all distinct
   connection coordinates in ordinal order and then all source coordinates in
   ordinal order, reloads the candidates, and re-evaluates eligibility.
6. A raw payload remains outside the database transaction. Its durable
   `Purging` claim prevents a reprocessing reservation while object storage is
   being deleted; completion is idempotent and source-serialized.
7. Queries remain lock-free versioned snapshots. Tenant termination continues
   to rely on the tenant lifecycle/quiescence protocol rather than taking every
   source coordinate.
8. Invalid, empty, cross-tenant, unsupported-provider, or transactionless lock
   coordinates fail closed. Tests may provide an explicit non-relational
   substitute.

## Efficiency

- Ordinary contention is limited to one external reservation source. Different
  source references on the same connection remain concurrent after the short
  shared connection fence.
- Identity discovery uses narrow untracked projections; mutable graphs are not
  materialized twice.
- Retention locks only a bounded candidate batch and acquires coordinates in a
  stable order, avoiding cross-batch deadlocks.
- The existing `source_operation_locks` row is coordination rather than domain
  data. Replace it with the GMA transaction-key primitive and remove its table,
  migration model, export, and tenant-destruction surface.
- Correctly coordinated paths do not use optimistic-concurrency retries as the
  normal contention protocol. Domain versions remain client preconditions.

## Delivery

1. Replace the persisted source-operation row adapter with a fail-closed
   transaction-key adapter and add the project migration that drops the table.
2. Add an Ingestion source-coordinate locator and mutation coordinator with
   explicit lock-before-reload operations for each graph entry point.
3. Route receipt processing, normalized dispatch, reservation outcomes,
   proposal decisions, and all reprocessing transitions through the
   coordinator.
4. Route raw-payload and sensitive-history retention through ordered bounded
   source batches, and close the unlocked existing-anonymisation replay path.
5. Add an architecture inventory of source writers plus focused unit and real
   PostgreSQL contention coverage.

## Deferred

- Moving product graph coordinators, source identity, or Ingestion lock order
  into GMA.
- Holding a database transaction open across object-storage deletion. Durable
  claim state is the recovery boundary instead.
- Serializing read-only operational queries or data-rights discovery/export
  snapshots. Their existing versions and evidence fences remain the contract.
- Replacing the modular-monolith transaction boundary with a workflow engine or
  adapter-specific distributed lock.

## Verification

- Focused unit tests prove coordinate resolution, connection-before-source
  order, lock-before-reload, ordered batch acquisition, and stale-version
  outcomes.
- Architecture coverage enumerates every online and retention source writer and
  requires the shared coordinator or an explicitly documented lifecycle fence.
- One targeted PostgreSQL scenario proves same-source writers wait, unrelated
  sources progress, first-use locking creates no row race, and a waiting writer
  reloads the committed winner.
- Run cheap focused tests while implementing, then one full non-Docker gate and
  only the targeted Docker scenario at slice completion.

## Delivered

- All source-graph writers use the shared coordinator or the documented tenant
  lifecycle fence, with deterministic connection-then-source ordering.
- Retention uses bounded discovery, ordered locking, authoritative reload, and
  stale-candidate re-evaluation.
- The persistent source-operation lock row and tenant-destruction stage were
  removed in favor of GMA's transaction-key primitive; Ingestion catalogue v12
  reflects the resulting processing surface.
- Verification passed with 316 Ingestion unit tests, the targeted PostgreSQL
  contention scenario, and `eng/verify.ps1 -SkipRestore` across the repository.

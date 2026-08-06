# Data Rights Coordinator Mutation Serialization Task

Status: complete

## Goal

Make Data Rights case and tenant-termination coordination continue from
authoritative state under concurrent API, task, inbox, and recovery work,
without turning expected conflicts into persistence exceptions or duplicating
protected side effects.

## Ownership

- Data Rights owns case, execution, export, processing-ledger, restore, and
  tenant-termination coordination state.
- Owner modules continue to own their records and execute only through
  versioned contracts and idempotency coordinates.
- GMA owns the provider-neutral transaction-key primitive. BunkFy owns the
  product lock hierarchy and does not move Data Rights vocabulary into GMA.
- No cross-module database access or distributed transaction is introduced.

## Lock Hierarchy

1. Tenant-control coordinate for singleton tenant-termination request and
   process admission.
2. Process coordinate for tenant-termination phase state.
3. Case coordinate for case lifecycle and case-derived child creation.
4. Child coordinate for an export artifact, correction execution,
   anonymisation work item, or owner work item.

The hierarchy is delivered in this slice. Child coordinates are limited to
anonymisation work items and tenant-termination owner work, where parallel
siblings must remain independent. The processing ledger adds one tenant-local
coordinate before its work-item coordinate so append sequence allocation is
single-writer. No path acquires a broader coordinate after a narrower one.

## Invariants

1. A coordinate is acquired inside the current Data Rights transaction and
   before the first tracked read used for a mutable decision.
2. After waiting, the handler reloads authoritative state and applies the
   caller's expected version to that state.
3. Concurrent equivalent retries converge on committed idempotent state;
   conflicting retries return a product error rather than leaking a database
   concurrency or unique-constraint exception.
4. Tenant-termination request, start, and missing-process recovery serialize
   the singleton eligibility checks and protected replay-intent write.
5. Tenant termination acquires tenant control when required, then process,
   case, and child coordinates in that order. Other case mutations acquire
   only the case coordinate before any child coordinate.
6. Different cases and tenants remain concurrent. Lock resources contain only
   normalized tenant ids and opaque ids.
7. Queries remain lock-free snapshots. Independent owner execution remains
   parallel and is not placed behind the tenant-control coordinate.
8. Existing optimistic concurrency, unique indexes, idempotency keys, outbox,
   and protected-store proofs remain defense in depth.

## Delivery

1. Add Data Rights operation-lock ports and PostgreSQL/SQL Server transaction
   key adapters for tenant-control and case coordinates.
2. Route tenant-termination admission and all case writers through a
   lock-before-reload coordinator.
3. Add a writer inventory so new case mutations cannot bypass the coordinate.
4. Audit execution, artifact, ledger, restore, process, and owner-work writers;
   add narrower coordinates only where required.
5. Run focused tests while editing, then one broad non-Docker gate and one
   targeted PostgreSQL contention scenario at slice completion.

## Efficiency

- One O(1) transaction key per active coordinate; no lock rows, polling,
  unbounded scans, or extra projections.
- Normal contention is limited to one case. The tenant-control key is held
  only by rare tenant-termination admission work.
- Locks release automatically on commit or rollback, including failed
  protected-store and outbox paths.

## Deferred

- Coordination across independently deployed services; that requires a
  versioned lease/idempotency protocol rather than a database transaction key.
- Moving product lock selection or Data Rights policy into GMA.
- Serializing independent owner calls or lock-free query snapshots.

## Evidence

- The full Data Rights test project passes 478 tests, including operation-lock
  validation, canonical lock ordering, authoritative reload, recovery
  presence rules, and writer inventory coverage.
- The focused PostgreSQL scenario proves same-case writers wait and reload,
  unrelated cases progress, independent owner children share a process,
  duplicate child work waits, and an exclusive process transition drains all
  shared owner work before continuing.
- The broad non-Docker verifier passes solution synchronization,
  source-package checks, the zero-warning build, migration drift checks, and
  every fast suite, including 54 Integration tests.
- The implementation adds no schema, migration, personal-data catalogue, GMA,
  or cross-module contract change.

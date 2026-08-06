# Workspaces Cross-Graph Correlation Mutation Serialization Task

Status: complete

## Goal

Make rare subject-wide Workspaces retention and Data Rights mutations continue
from authoritative state without racing the source-local Staff-onboarding graph
or the staff-local access-lifecycle graph.

## Ownership

- Workspaces owns its copied onboarding subjects, access-process actors,
  access-plan creators, retention receipts, anonymisation proof, and restore
  replay.
- Retention and DataRights own orchestration and approval. Staff,
  Organizations, Auth, and Access Control retain their own state and are used
  only through contracts.
- GMA owns the provider-neutral transaction-key primitive. The decision that a
  Workspaces subject-correlation mutation spans several product graphs remains
  BunkFy policy and does not move into GMA.
- No schema, migration, catalogue, or cross-module contract change is needed.

## Decision

Use the existing tenant mutation coordinate that every Workspaces operational
writer already acquires in shared mode. A subject-wide correlation mutation
takes that coordinate exclusively before taking narrower staff or row locks.

This is deliberately more conservative than discovering and locking every
affected onboarding source. The operation is rare and destructive, while the
alternative requires an unbounded pre-scan, deterministic multi-source lock
sets, and retry when the set changes between discovery and mutation.

## Invariants

1. Request validation and immutable replay lookup may happen before the
   exclusive lock, but no mutable eligibility decision may.
2. New retention scrub, Data Rights anonymisation, and restore work takes the
   tenant coordinate exclusively before any staff, source, or row coordinate.
   No shared-to-exclusive upgrade is permitted.
3. Acquiring the exclusive coordinate revalidates the active tenant scope and
   termination fence inside the current transaction.
4. The existing staff-access and correlation row locks remain in place after
   the tenant lock, and all state used for mutation is read under that complete
   hierarchy.
5. Ordinary source-local and staff-local writers continue to take the tenant
   coordinate shared, so they drain before a cross-graph mutation and wait
   while it commits.
6. Tenant export and termination use the same exclusive coordinate, so they
   cannot overlap a subject-correlation mutation.
7. Unrelated tenants remain concurrent. Lock resources contain only normalized
   tenant ids and opaque aggregate ids, never subject or contact data.
8. Queries and the early immutable anonymisation-proof replay remain lock-free
   snapshots and fail closed if their proof disappears during tenant
   destruction. Other idempotent mutation commands may conservatively
   reacquire the exclusive coordinate.

## Scope

- irreversible Staff retention-correlation scrub;
- reversible Staff Rights correlation anonymisation; and
- database-restore replay that reapplies the anonymised state.

Application-scoped correction and restriction remain under the onboarding
source/application hierarchy. Tenant destruction remains under its lifecycle
coordinator.

## Efficiency

- One transaction-scoped advisory key replaces an unbounded source-id scan.
- Contention is limited to the affected tenant and only for rare destructive
  operations.
- No persistent lock rows, background polling, distributed lease, or extra
  projection is introduced.
- Existing command/inbox transactions release the lock automatically on commit
  or rollback.

## Delivery

1. Add a Workspaces-owned cross-graph mutation-lock port backed by the existing
   tenant mutation key in exclusive mode.
2. Route retention scrub, anonymisation, and restore through it before their
   narrower locks.
3. Add writer-inventory, lock-order, invalid-scope, and real PostgreSQL
   contention coverage.
4. Run focused tests while editing, then one broad non-Docker gate and one
   targeted Docker scenario at slice completion.

## Deferred

- Distributed service coordination outside the modular monolith transaction.
- Changing generic GMA locking or tenant-termination contracts.
- Combining Staff, Organizations, Auth, or Access Control persistence into the
  Workspaces transaction.

## Evidence

- The full Workspaces test project passes 338 tests, including writer
  inventory, lock ordering, invalid-scope, retention scrub, anonymisation, and
  restore coverage.
- The focused PostgreSQL scenario proves same-tenant shared writers drain
  before the exclusive mutation, new writers wait until it commits, existing
  narrower lock acquisition remains reentrant, unrelated tenants progress,
  and the termination fence fails closed.
- The broad non-Docker verifier passes solution synchronization,
  source-package checks, the warning-free build, migration drift checks, and
  every fast suite, including 54 Integration tests.
- The implementation adds no schema, catalogue, migration, GMA, or
  cross-module contract change.

# Workspace Staff Access Mutation Serialization Task

Status: complete

## Goal

Make every Workspaces-owned decision that mutates one staff member's access
lifecycle continue from authoritative state under one tenant-admitted,
transaction-scoped staff coordinate.

## Ownership

- Workspaces owns staff-access process ordering, retry decisions, restoration
  snapshots, and the local correlation records that refer to those processes.
- Staff owns employment lifecycle state, Organizations owns memberships, and
  Access Control owns profiles and assignments. Workspaces continues to
  coordinate those modules only through their contracts.
- GMA owns command and inbox transactions, tenant-termination coordination,
  and the provider-neutral transaction-key-lock primitive. Staff identifiers,
  access-process discovery, and Workspaces lock order remain product policy.
- No persistent lock table or provider-specific migration is required.

## Invariants

1. Operational staff-access work acquires the tenant admission fence before a
   per-staff exclusive coordinate.
2. Preparation locks the staff coordinate before replay, open-process, or
   restoration-snapshot reads, so competing Staff versions cannot create
   incompatible processes.
3. Process-id commands perform only immutable staff-coordinate discovery
   before locking, then reload the aggregate before deciding or invoking
   Organizations and Access Control.
4. Staff lifecycle events lock by staff identifier before loading the exact
   process version and restoring access.
5. Retention correlation scrubbing takes the same staff coordinate before
   testing active workflows or rewriting terminal references.
6. Reversible correlation anonymisation and restore take the staff coordinate
   before their existing anchor-row proof lock and authoritative snapshot.
7. Tenant destruction remains the outer lifecycle fence. Queries and exports
   remain lock-free snapshots.
8. Empty, cross-tenant, missing-process, or transactionless relational
   coordinates fail closed. Non-relational tests use an explicit no-op key
   implementation while preserving lock-before-reload order.

## Efficiency

- Contention is limited to one staff member; unrelated staff workflows remain
  concurrent.
- Process-id discovery is a narrow, untracked scalar query. The mutable
  aggregate is materialized only after the lock.
- Correctly coordinated contention waits and reloads instead of using broad
  retries around cross-module side effects.
- The transaction-key lock creates no rows and adds no catalogue, retention,
  export, or tenant-destruction surface.

## Delivery

1. Add the Workspaces staff-access operation-lock port, product coordinator,
   and EF transaction-key adapter.
2. Route preparation, denial, retry, and Staff lifecycle handling through the
   coordinator.
3. Share the coordinate with retention correlation and reversible
   anonymisation/restore before their existing owner-specific proof work.
4. Add writer-inventory, lock-order, and real PostgreSQL contention coverage.

## Deferred

- Onboarding source/access-plan serialization; that is the next independent
  Workspaces slice with a different source-and-applicant lock hierarchy.
- Moving staff-access vocabulary or orchestration into GMA.
- Replacing the current modular-monolith transaction boundary with a
  distributed workflow engine.

## Verification

- Focused unit tests prove lock-before-reload and enumerate every access writer.
- One targeted PostgreSQL test proves same-staff writers wait and reload while
  unrelated staff coordinates remain concurrent.
- Run cheap focused tests while editing, then one broad non-Docker gate and one
  targeted Docker scenario only at slice completion.

## Delivered

- Preparation, denial, retry, Staff lifecycle continuation, retention
  correlation, and reversible correlation execution now share one
  tenant-admitted per-staff transaction key.
- Process-id paths discover only the immutable staff identifier before the
  lock and reload the aggregate after acquisition; no lock rows or migrations
  were introduced.
- Writer-inventory and sequencing coverage is part of the 305-test Workspaces
  suite. The targeted PostgreSQL proof demonstrated same-staff waiting and
  authoritative reload, version 2 to version 3 continuation, and concurrent
  progress for an unrelated staff coordinate.
- `eng/verify.ps1 -SkipRestore` passed in 395 seconds with synchronized
  solutions, source-package checks, a zero-warning build, clean migration
  drift, Workspaces 304/304 at that checkpoint, Architecture 92/92, and
  non-Docker Integration 54/54. The final invalid-coordinate guard then passed
  the focused suite and the complete Workspaces suite at 305/305 without
  repeating the expensive repository gate.

# Workspace Staff Onboarding Source Mutation Serialization Task

Status: complete

## Goal

Make every Workspaces-owned access-plan and ordinary Staff-onboarding mutation
continue from authoritative state under one tenant-admitted,
transaction-scoped source graph.

## Ownership

- Workspaces owns access-plan state, onboarding applications, processing
  restrictions, correction receipts, and the ordering between those records.
- Organizations owns invitation, enrollment-link, and enrollment-claim
  authority. Staff owns staff profiles and property assignments. Access
  Control owns profiles and memberships. Workspaces continues to integrate
  with those modules only through contracts.
- GMA owns command and inbox transactions, tenant-termination coordination,
  and the provider-neutral transaction-key-lock primitive. Source, applicant,
  and onboarding-application coordinates remain BunkFy product policy.
- No persistent lock table, schema migration, or new cross-module contract is
  required.

## Invariants

1. Operational onboarding work acquires the tenant admission fence before any
   source-graph lock.
2. A source identifier is the graph root because the access-plan primary key
   is the source identifier. This also serializes the defensive case where two
   source kinds present the same identifier.
3. Ordinary application work takes a shared source lock, then the existing
   application row lock, and reloads before deciding or provisioning.
4. First submission additionally takes an exclusive applicant coordinate,
   derived from a SHA-256 digest rather than exposing a subject identifier in
   a lock resource, before checking for an existing application or creating
   one.
5. Plan preparation and activation, source expiry or supersession, enrollment
   finalization, and retention reconciliation take the source exclusively from
   the outset. No flow upgrades a shared source lock.
6. Application-id commands perform only narrow, untracked immutable source
   discovery before locking, then reload the mutable application.
7. Processing restriction and correction flows share the source/application
   hierarchy with operational processing, so a restriction cannot race a
   provisioning continuation.
8. Queries and exports remain lock-free snapshots. Tenant destruction remains
   the outer lifecycle fence.
9. Empty, missing, cross-tenant, or transactionless relational coordinates
   fail closed. Non-relational tests preserve lock-before-reload ordering with
   an explicit no-op key implementation.

## Efficiency

- Normal contention is limited to one source graph; unrelated invitations and
  enrollment links remain concurrent.
- Applicants under one reusable enrollment link can process concurrently
  because they share the source lock and serialize only their own application
  row. The applicant coordinate is used only around absent-row creation.
- Source lifecycle work intentionally pauses all applicants for that source so
  it can expire or supersede the plan and active applications atomically.
- Application-id discovery is a narrow, untracked scalar query. Transaction
  keys create no rows and add no catalogue, export, retention, or destruction
  surface.

## Delivery

1. Extend the onboarding operation-lock port with source and applicant
   coordinates and add the Workspaces mutation coordinator.
2. Route access-plan preparation/activation, submission, processing, retry,
   restriction, correction, retention, and Organizations lifecycle consumers
   through the hierarchy.
3. Add writer-inventory, sequencing, and real PostgreSQL contention coverage.
4. Run focused tests while editing, then one broad non-Docker gate and one
   targeted Docker scenario at slice completion.

## Deferred

- Subject-wide Staff correlation anonymisation and restore span multiple source
  graphs and remain governed by the separate staff-access correlation
  hierarchy. Their cross-coordinate lock composition belongs to the following
  Workspaces Data Rights capstone rather than this source-local slice.
- Distributed adapter/service orchestration; transaction-key locks are a
  modular-monolith coordination mechanism.
- Moving onboarding vocabulary or source policy into GMA.
- Redesigning Organizations invitation or enrollment contracts.

## Evidence

- The full Workspaces test project passes 331 tests, including writer
  inventory, lock ordering, restriction enforcement, lifecycle, retry,
  correction, and retention coverage.
- The focused PostgreSQL scenario proves applicant-level creation isolation,
  shared-source concurrency, exclusive source-finalization waiting,
  authoritative reload after the waiter resumes, and unrelated-source
  progress.
- The implementation adds no schema, catalogue, migration, or cross-module
  contract change; all product coordinates remain inside Workspaces and reuse
  GMA's provider-neutral transaction-key primitive.

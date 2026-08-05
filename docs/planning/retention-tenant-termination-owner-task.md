# Retention Tenant Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Make Retention an authoritative destructive tenant-termination owner without
moving BunkFy scheduling policy into Data Rights or GMA.

The owner must stop late scheduling and transport mutations, remove its tenant
control state in bounded resumable batches, and return exact PII-free proof.
Production execution remains disabled until cross-owner preflight, protected
replay, operator controls, and production admission are complete.

## Ownership

- Retention owns execution history, schedule health state, organization and
  property projections, its inbox, and local tenant lifecycle state.
- Retention does not own the business records processed by retention jobs and
  does not create a second legal-hold policy for them.
- Data Rights supplies approved process coordinates and stores only bounded
  owner status, counts, catalogue coordinates, and proof revisions.
- Workspaces supplies the exact frozen process and termination-epoch fence.
- Ingestion must complete its destructive contribution before Retention begins.
- GMA supplies provider-neutral transaction key locks and messaging admission
  hooks. No Retention owner key, table graph, or scheduling rule belongs in GMA.

## Destruction Policy

Running executions and current schedule state are tenant-owned control history,
not independent tenant-termination holds. Once the exact workspace fence is
frozen, closing waits for already-admitted transactions and prevents new
schedules, execution writes, projection writes, and scoped inbox delivery.

Task Runtime owns scheduled-task leases and is destroyed later. A task already
dispatched for Retention can no longer mutate this owner after local closing;
its normal retry or terminal state remains Task Runtime's responsibility.

The owner retains only one closed tenant lifecycle row and one immutable,
PII-free destruction receipt bound to the exact operation, request digest,
selected/resulting revision, batch size, removal count, chained removal proof,
and timestamps.

## Lifecycle And Concurrency

- The existing tenant revision becomes the local `Open`, `Closing`, or
  `Closed` lifecycle fence. The first accepted destruction operation advances
  the selected revision exactly once.
- One operation id and one scope may identify only one canonical request.
  Equivalent retries resume or replay; changed coordinates conflict.
- Destruction takes an operation lock and the exclusive tenant lifecycle lock.
  Ordinary writes and admitted inbox handlers use the shared tenant lock, so
  closing waits for in-flight transactions.
- Schedule discovery excludes closing and closed tenants across all scopes.
- Unknown or unavailable Workspaces fence state fails closed. The exact process
  id, epoch, and frozen state are rechecked on every attempt.

## Bounded Removal

Each invocation removes at most one non-empty batch of 500 rows. Empty stages
advance in the same transaction until work is found or completion is reached.
Stages cover every tenant-owned table in deterministic order:

1. inbox messages;
2. schedule state;
3. execution history;
4. property projections;
5. tenant projections; and
6. final absence verification and immutable receipt creation.

Every committed batch extends a versioned SHA-256 chain over the stage and
stable owner-local row keys. Central state receives only the cumulative count
and selected/resulting revisions.

The destruction receipt is append-only in application and PostgreSQL layers.
No retained execution, schedule, projection, inbox, or actor value is copied
into the receipt.

## Ordering

Retention keeps its existing export dependency on Ingestion and declares the
same dependency for destruction. Operations Notifications remains downstream,
followed by generic Organizations, Access Control, and Task Runtime owners
according to phase-specific plans.

## Acceptance

- export behavior and deterministic schema remain unchanged;
- equivalent retries resume and completed retries replay exactly;
- changed operation coordinates conflict;
- closing suppresses schedule discovery, scoped writes, and inbox handling;
- one call removes at most one non-empty bounded batch;
- every tenant-owned table is empty at completion except lifecycle state and
  the immutable receipt;
- another tenant remains untouched;
- migration, locking, replay, admission, batch bounds, receipt immutability,
  and tenant isolation are proven once at the end of the slice; and
- the complete fast Retention suite passes before one exact Docker scenario.

## Deferred Production Work

- cross-owner preflight for active work and operator readiness;
- protected replay delta generation and restore-readiness enforcement;
- operator API, Admin API, CLI, assurance, retry, and recovery UX; and
- production admission tied to the exact final mandatory-owner catalogue.

## Verification

- all 34 fast Retention tests pass;
- the Integration test project builds with zero warnings and zero errors;
- EF reports no pending Retention model changes after
  `AddRetentionTenantDestructionLifecycle`;
- the exact PostgreSQL 16 scenario proves that workspace freeze waits for an
  already-admitted shared-lock transaction, then verifies bounded destruction,
  global schedule exclusion, exact replay and conflict, append-only receipt
  enforcement, closed-scope admission, and tenant isolation; and
- Reservations, Guests, Staff, Ingestion, and Retention now use shared tenant
  locks for ordinary admission while export and lifecycle work retain exclusive
  locks, avoiding unnecessary same-tenant write serialization.

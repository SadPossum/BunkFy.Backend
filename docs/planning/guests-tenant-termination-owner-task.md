# Guests Tenant Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Make Guests an authoritative destructive tenant-termination owner without
moving Guest policy into Data Rights or exposing Guest, stay, property, or
governance identifiers in central work records, logs, metrics, or
notifications.

The owner must close late mutation and projection paths, remove its tenant
state in bounded resumable batches, and return an exact PII-free proof.
Production execution remains disabled until cross-owner preflight, protected
replay, operator controls, and production admission are complete.

## Ownership

- Guests alone evaluates Guest legal holds and deletes Guest profiles, stay
  history, projections, transport journals, checkpoints, and local governance
  evidence.
- Data Rights supplies approved process coordinates and stores only bounded
  owner status, counts, catalogue coordinates, and proof revisions.
- Workspaces supplies the exact frozen process and termination-epoch fence.
- Reservations remains the authoritative booking owner and must complete its
  destructive contribution before Guests begins.
- GMA supplies provider-neutral transaction key locks and messaging admission
  hooks. No BunkFy owner key, Guest rule, or termination phase belongs in GMA.

## Destruction Policy

An active Guests legal hold blocks before local closing state or any
destructive mutation is persisted. The result exposes only a bounded count and
stable code; hold reasons and Guest identities remain local.

Once the exact tenant termination is approved and frozen, active, archived,
and anonymised Guest profiles are all in scope. Profile lifecycle state is not
an independent tenant-termination hold. A future statutory Guest minimum must
be an explicit Guests policy extension and catalogue change before production
admission.

The owner retains only:

- one tenant lifecycle row in closed state; and
- one immutable, PII-free destruction receipt bound to the exact operation,
  request digest, selected/resulting revision, batch size, removal count,
  chained removal proof, and timestamps.

Those control rows are not reported as retained customer records.

## Lifecycle And Concurrency

- The existing tenant revision becomes the local `Open`, `Closing`, or
  `Closed` lifecycle fence. The first accepted destruction operation advances
  the selected revision exactly once.
- One operation id and one scope may identify only one canonical request.
  Equivalent retries resume or replay; changed operation, scope, revision, or
  batch coordinates conflict or become stale.
- Destruction takes an operation lock and the tenant mutation lock before
  changing local state. Ordinary writes and admitted inbox handlers use the
  same tenant lock, so closing waits for in-flight transactions.
- Scoped projection and rebuild writes are locally admission-checked without
  changing export revision semantics.
- Inbox delivery is suppressed once local state is closing or closed. Outbox
  claims exclude closing and closed scopes.
- A currently leased outbox message returns retry-required until the lease is
  completed or expires. Unleased pending and processed messages are owner data
  and are removed by the lifecycle.
- Unknown or unavailable Workspaces fence state fails closed. The exact
  process id, epoch, and frozen state are rechecked on every attempt.

## Bounded Removal

Each invocation removes at most one non-empty batch of 500 rows. Empty stages
advance in the same transaction until work is found or completion is reached.
Stages are foreign-key safe and cover every tenant-owned table:

1. transport journals;
2. retention, anonymisation, restore, hold, restriction, and correction
   receipts in dependent-first order;
3. anonymisation tombstones, legal holds, restrictions, and retention
   executions;
4. stay history followed by Guest profiles;
5. restriction and property projections;
6. projection and retention checkpoints plus operation locks; and
7. final absence verification and immutable receipt creation.

Every committed batch extends a versioned SHA-256 chain over the stage and
stable owner-local row keys. The central result receives only the cumulative
count and selected/resulting revisions, never row keys or digest inputs.

Existing PostgreSQL receipt and tombstone triggers remain strict for normal
writes. Destruction uses a transaction-local operation id, and trigger bypass
is accepted only when that id matches the live operation, scope, request
digest, and `Closing` lifecycle state. Updates remain prohibited. The final
destruction receipt is independently append-only in application and database
layers.

## Ordering

Guests keeps its existing export dependency on Reservations and declares the
same dependency for destruction. This preserves booking blockers and
reservation-to-Guest unlinking before canonical Guest identity is removed.

## Acceptance

- export behavior and deterministic schema remain unchanged;
- active legal holds block before any local destruction progress;
- equivalent retries resume and completed retries replay exactly;
- changed operation coordinates conflict and stale revisions retry safely;
- closing suppresses late scoped writes, inbox handling, and outbox claims;
- active outbox leases cannot be deleted beneath a publisher;
- one call removes at most one non-empty bounded batch;
- every tenant-owned table is empty at completion except lifecycle state and
  the immutable receipt;
- ordinary governance receipt mutation and tombstone deletion stay blocked;
- another tenant remains untouched;
- PostgreSQL trigger authorization, migration, lock, foreign-key order,
  replay, transport race, and receipt immutability are proven once at the end
  of the slice; and
- the complete fast Guests suite passes before the single exact Docker
  scenario is run.

## Deferred Production Work

- cross-owner preflight and explicit active-booking confirmation;
- protected replay delta generation and restore-readiness enforcement;
- counsel-approved statutory retention exceptions and backup expiry evidence;
- operator API, Admin API, CLI, assurance, retry, and recovery UX; and
- production admission tied to the exact final mandatory-owner catalogue.

## Completion Evidence

- migration `20260804091217_AddGuestsTenantDestructionLifecycle` has no
  pending PostgreSQL model changes;
- all 134 Guests tests pass; and
- `Tenant_destroy_is_bounded_resumable_immutable_and_isolated` passes against
  PostgreSQL 16 with a dense governance graph, active-hold and outbox-lease
  blockers, the 500/1 batch boundary, trigger-authorized receipt and tombstone
  deletion, exact replay/conflict, append-only destruction receipt,
  closed-scope message admission, and cross-tenant isolation.

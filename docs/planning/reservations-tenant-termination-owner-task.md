# Reservations Tenant Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Make Reservations an authoritative destructive tenant-termination owner without
moving booking policy into Data Rights or leaking reservation identifiers into
central work records, logs, metrics, or notifications.

The owner must close late mutation paths, remove its tenant state in bounded
resumable batches, and return an exact PII-free proof. Production execution
remains disabled until cross-owner preflight, protected replay, operator
controls, and production admission are complete.

## Ownership

- Reservations alone evaluates reservation holds and deletes Reservations
  aggregates, history, projections, message journals, checkpoints, and local
  governance evidence.
- Data Rights supplies the approved process coordinates and stores only
  bounded owner status, counts, catalogue coordinates, and proof revisions.
- Workspaces supplies the exact frozen process and termination-epoch fence.
- GMA supplies provider-neutral transaction key locks and messaging admission
  hooks. No BunkFy owner key, reservation rule, or termination phase belongs in
  GMA.

## Destruction Policy

An active Reservations legal hold blocks before local closing state or any
destructive mutation is persisted. The result exposes only a bounded count and
stable code; hold reasons and reservation identities remain local.

Once the exact tenant termination is approved and frozen, non-terminal,
future, pending, and terminal reservations are all in scope. Treating an
ordinary booking state as an undeletable hold would let stale or malformed
business state prevent workspace closure forever. The production coordinator
must nevertheless preflight and visibly confirm active bookings before the
first irreversible owner executes.

The owner retains only:

- one tenant lifecycle row in closed state; and
- one immutable, PII-free destruction receipt bound to the exact operation,
  request digest, selected/resulting revision, batch size, removal count,
  chained removal proof, and timestamps.

Those control rows are not reported as retained customer records. Any future
jurisdiction-specific statutory reservation minimum belongs to an explicit
Reservations policy extension and must change the owner catalogue before
Production admission.

## Lifecycle And Concurrency

- The existing tenant revision becomes the local `Open`, `Closing`, or
  `Closed` lifecycle fence. The first accepted destruction operation advances
  the selected revision exactly once.
- One operation id and one scope may identify only one canonical request.
  Equivalent retries resume or replay; changed operation, scope, revision, or
  batch coordinates conflict or become stale.
- Destruction takes an operation lock and the tenant mutation lock before
  changing local state. Ordinary writes and admitted inbox handlers use the
  same tenant lock, so the close transition waits for in-flight transactions.
- Inbox delivery is suppressed once the local state is closing or closed.
  Outbox claims exclude closing and closed scopes.
- A currently leased outbox message returns retry-required until the lease is
  completed or expires. Unleased pending and processed messages are owner data
  and are removed by the lifecycle.
- Unknown or unavailable Workspaces fence state fails closed. The exact
  process id, epoch, and frozen state are rechecked on every attempt.

## Bounded Removal

Each invocation removes at most one non-empty batch. Empty stages advance in
the same transaction until work is found or completion is reached. Stages are
foreign-key safe and cover every tenant-owned table:

1. transport journals and local operation locks;
2. dependent hold, anonymisation, restore, retention, and restriction proof;
3. reminder, external-operation, details-history, hold, restriction, and
   retention control state;
4. reservation children followed by reservation aggregates;
5. inventory, Guest, property, policy, and restriction projections;
6. projection and retention checkpoints; and
7. final absence verification and immutable receipt creation.

Every committed batch extends a versioned SHA-256 chain over the stage and
stable owner-local row keys. The central result receives only the cumulative
count and selected/resulting revisions, never the row keys or digest inputs.

## Ordering

Reservations keeps its existing export dependency on Inventory. Destruction
is a root owner in the product graph so booking blockers are discovered before
dependent Guest, Inventory, topology, generic access, and task state is
removed. Later product owners declare their own destruction dependency on
Reservations where their policy requires it.

## Acceptance

- export behavior and deterministic schema remain unchanged;
- legal holds block before any local destruction progress;
- equivalent retries resume and completed retries replay exactly;
- changed operation coordinates conflict and stale revisions retry safely;
- closing suppresses late inbox handling and new outbox claims;
- active outbox leases cannot be deleted beneath a publisher;
- one call removes at most one non-empty bounded batch;
- every tenant-owned table is empty at completion except lifecycle state and
  the immutable receipt;
- another tenant remains untouched;
- PostgreSQL migration, transaction-lock, foreign-key order, replay, and
  transport-race behavior are proven once at the end of the slice; and
- the complete fast Reservations suite passes before the single exact Docker
  scenario is run.

## Deferred Production Work

- cross-owner preflight and active-booking confirmation before irreversible
  execution;
- protected replay delta generation and restore-readiness enforcement;
- counsel-approved statutory retention exceptions and backup expiry evidence;
- operator API, Admin API, CLI, assurance, retry, and recovery UX; and
- Production admission tied to the exact final mandatory-owner catalogue.

## Completion Evidence

- migration `20260804082947_AddReservationsTenantDestructionLifecycle` has no
  pending PostgreSQL model changes;
- all 169 Reservations tests pass;
- the focused Data Rights owner-result shape suite passes all 7 tests; and
- `Tenant_destroy_is_bounded_resumable_immutable_and_isolated` passes against
  PostgreSQL 16 with active-hold and outbox-lease blockers, 500/1 batch resume,
  exact replay/conflict, append-only receipt enforcement, closed-scope message
  admission, and cross-tenant isolation.

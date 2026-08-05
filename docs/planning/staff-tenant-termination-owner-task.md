# Staff Tenant Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Make Staff an authoritative destructive tenant-termination owner without
moving employment policy into Data Rights or leaking staff, account, property,
or governance identifiers into central work records, logs, metrics, or
notifications.

The owner must close late mutation and projection paths, remove its tenant
state in bounded resumable batches, and return exact PII-free proof.
Production execution remains disabled until cross-owner preflight, protected
replay, operator controls, and production admission are complete.

## Ownership

- Staff alone evaluates Staff legal holds and deletes employment profiles,
  property-assignment history, employment governance, projections, transport
  journals, checkpoints, and local governance evidence.
- Auth owns credentials, authenticators, sessions, and account lifecycle.
  Organizations owns memberships and invitations. Access Control owns roles,
  grants, profiles, and effective authorization. Staff stores only its
  employment-side subject reference.
- Data Rights supplies approved process coordinates and stores only bounded
  owner status, counts, catalogue coordinates, and proof revisions.
- Workspaces supplies the exact frozen process and termination-epoch fence.
- Guests must complete its destructive contribution before Staff begins;
  Ingestion and later generic owners remain downstream.
- GMA supplies provider-neutral transaction key locks and messaging admission
  hooks. No BunkFy employment rule or owner key belongs in GMA.

## Destruction Policy

An active Staff legal hold blocks before local closing state or any destructive
mutation is persisted. The result exposes only a bounded count and stable
code; hold reasons and staff identities remain local.

Once the exact tenant termination is approved and frozen, active, suspended,
departed, and anonymised employment profiles are all in scope. Employment or
account lifecycle state is not an independent tenant-termination hold. The
production coordinator must preflight active staff and owner continuity before
the first irreversible owner executes.

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
- Destruction takes an operation lock and the shared tenant mutation lock.
  Ordinary writes and admitted inbox handlers use the same tenant lock, so
  closing waits for in-flight transactions.
- Scoped projections and rebuild writes are locally admission-checked without
  changing export revision semantics.
- Inbox delivery is suppressed and outbox claims exclude a closing or closed
  scope. An active outbox lease returns retry-required until completion or
  expiry.
- Unknown or unavailable Workspaces fence state fails closed. The exact
  process id, epoch, and frozen state are rechecked on every attempt.

## Bounded Removal

Each invocation removes at most one non-empty batch of 500 rows. Empty stages
advance in the same transaction until work is found or completion is reached.
Stages are foreign-key safe and cover every tenant-owned table:

1. transport journals;
2. retention, anonymisation, restore, hold, employment-governance,
   restriction, and correction receipts in dependent-first order;
3. anonymisation tombstones, property assignments, legal holds, restrictions,
   employment governance and acknowledgements, retention executions, and
   operation locks;
4. Staff member aggregates;
5. restriction and property projections;
6. projection and retention checkpoints; and
7. final absence verification and immutable receipt creation.

Every committed batch extends a versioned SHA-256 chain over the stage and
stable owner-local row keys. Central state receives only the cumulative count
and selected/resulting revisions.

Existing PostgreSQL receipt and tombstone triggers remain strict for normal
writes. Destruction uses a transaction-local operation id, accepted only when
it matches the live operation, scope, request digest, and `Closing` lifecycle
state. Updates remain prohibited. The final destruction receipt is
independently append-only in application and database layers.

## Ordering

Staff keeps its existing export dependency on Guests and declares the same
dependency for destruction. Ingestion remains downstream of Staff, preserving
the current product-owner chain before Retention, Operations Notifications,
Organizations, Access Control, and Task Runtime execute.

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
- normal receipt mutation and tombstone deletion stay blocked;
- another tenant remains untouched;
- PostgreSQL trigger authorization, migration, lock, foreign-key order,
  replay, transport race, and receipt immutability are proven once at the end
  of the slice; and
- the complete fast Staff suite passes before one exact Docker scenario.

## Deferred Production Work

- cross-owner preflight for active staff, owner continuity, and active
  bookings before irreversible execution;
- protected replay delta generation and restore-readiness enforcement;
- counsel-approved statutory employment retention and backup expiry evidence;
- operator API, Admin API, CLI, assurance, retry, and recovery UX; and
- production admission tied to the exact final mandatory-owner catalogue.

## Verification

- `dotnet test` for the complete Staff suite: 167 passed;
- PostgreSQL migration model drift: none;
- Integration project build: zero warnings and errors; and
- exact PostgreSQL 16 destruction scenario: 1 passed, proving active-hold and
  live-lease blockers, the 500/1 boundary, trigger-authorized owner deletion,
  exact replay/conflict, closed-scope admission, immutable receipt, and tenant
  isolation.

The earlier export-only container scenario was not rerun during this slice.

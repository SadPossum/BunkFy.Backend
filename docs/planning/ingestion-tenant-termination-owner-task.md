# Ingestion Tenant Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Make Ingestion an authoritative destructive tenant-termination owner without
moving provider, legal-hold, evidence-retention, or raw-payload policy into Data
Rights or GMA.

The owner must close every late local mutation path, remove database and raw
payload state in bounded resumable work, and return exact PII-free proof.
Production execution remains disabled until cross-owner preflight, protected
replay, operator controls, and production admission are complete.

## Ownership

- Ingestion owns adapter connections and credential metadata, tenant ingress
  controls, runs, observations and raw payload objects, reprocessing history,
  reservation source links and dispatches, proposals, legal holds, retention
  history, projections, transport journals, checkpoints, and anonymisation
  evidence.
- The global adapter-ingress control is deployment-wide infrastructure state.
  It is not tenant-owned and must never be selected, counted, or removed by a
  tenant operation.
- The configured file-storage adapter owns object persistence mechanics;
  Ingestion owns the deterministic raw-payload key and deletion policy. GMA
  Files must not acquire BunkFy evidence semantics.
- Data Rights supplies approved process coordinates and stores only bounded
  owner status, counts, catalogue coordinates, and proof revisions.
- Workspaces supplies the exact frozen process and termination-epoch fence.
- Staff must complete its destructive contribution before Ingestion begins.
- GMA supplies provider-neutral transaction key locks and messaging admission
  hooks. No Ingestion owner key, table graph, MinIO key, or legal-hold rule
  belongs in GMA.

## Destruction Policy

Any active Ingestion legal hold blocks before local closing state, raw-object
deletion, or database destruction progress is persisted. Released holds remain
tenant-owned history and are deleted during an approved destruction.

Pending or active runs, proposals, reprocessing attempts, dispatches, adapter
leases, and credentials are not independent termination holds. Closing takes
the exclusive tenant lifecycle lock, waits for already-admitted transactions,
and prevents new work before those records are removed. An active outbox lease
returns retry-required until the publisher completes or its lease expires.

The owner retains only one closed tenant lifecycle row and one immutable,
PII-free destruction receipt bound to the exact operation, request digest,
selected/resulting revision, database and object batch counts, chained proofs,
and timestamps.

## Raw Payload Protocol

Raw payload objects are authoritative tenant data outside PostgreSQL. They are
removed before their observation receipts:

1. under the lifecycle lock, select a stable bounded batch of non-purged
   receipts and commit any new local closing operation;
2. release the database transaction before calling the file-storage adapter;
3. delete each exact `(tenant, connection, payload)` object and verify a read
   returns no object;
4. re-enter under the lifecycle lock, revalidate the exact request, fence,
   operation, receipt coordinates, and unchanged stage;
5. mark those payloads purged and extend a separate versioned SHA-256 object
   proof in the same database transaction; and
6. retry until no non-purged payload remains, then advance to row removal.

Deletion and absence are both successful idempotent outcomes. A crash after
object deletion but before database progress causes the same batch to be
selected and verified again. A crash after progress commit resumes from the
remaining non-purged rows. Failed or inconclusive object reads never permit the
receipt row to be removed. Controlled parallelism bounds storage pressure; no
database transaction is held across storage I/O.

An observation already marked `Purged` is trusted only because all supported
retention and anonymisation flows mark that state after an idempotent delete
returns successfully; no admitted writer can recreate its immutable key after
local closing begins.

## Lifecycle And Concurrency

- The existing tenant revision becomes the local `Open`, `Closing`, or
  `Closed` lifecycle fence. The first accepted destruction operation advances
  the selected revision exactly once.
- One operation id and one scope may identify only one canonical request.
  Equivalent retries resume or replay; changed coordinates conflict.
- Destruction takes an operation lock and the exclusive tenant lifecycle lock.
  Ordinary writes and admitted inbox handlers use the shared tenant lock, so
  closing waits for in-flight transactions.
- Direct credential mutations, scoped projections, checkpoints, and transport
  writes are locally admission-checked. Projection and message admission does
  not change export revision semantics.
- Inbox delivery is suppressed and outbox claims exclude a closing or closed
  scope.
- Unknown or unavailable Workspaces fence state fails closed. The exact
  process id, epoch, and frozen state are rechecked before every irreversible
  database or object-storage step.

## Bounded Removal

Raw-object work uses a smaller bounded batch with controlled concurrency.
Database invocations remove at most one non-empty batch of 500 rows. Empty
stages advance in the same transaction until work is found or completion is
reached. Foreign-key-safe stages cover:

1. transport journals;
2. reservation dispatches and proposals;
3. reprocessing outputs, derived observations, attempts, and source
   observations;
4. anonymisation receipts, fingerprints, record plans, and tombstones;
5. source links, runs, credentials, legal holds, retention executions, source
   operation locks, and adapter connections;
6. tenant ingress controls, property projections, and rebuild checkpoints; and
7. final owner-graph and raw-object absence verification followed by immutable
   receipt creation.

Every committed database batch extends a versioned SHA-256 chain over its stage
and stable owner-local row keys. Raw objects use an independent chain over
receipt, payload, and connection coordinates. Central state receives only the
cumulative affected count and selected/resulting revisions.

Existing PostgreSQL anonymisation receipt and tombstone triggers remain strict
for normal writes. Destruction uses a transaction-local operation id, accepted
only when it matches the live operation, scope, request digest, and `Closing`
lifecycle state. Updates remain prohibited. The final destruction receipt is
independently append-only in application and database layers.

## Ordering

Ingestion keeps its existing export dependency on Staff and declares the same
dependency for destruction. Retention and Operations Notifications remain
downstream, followed by generic Organizations, Access Control, and Task Runtime
owners according to phase-specific plans.

## Acceptance

- export behavior and deterministic schema remain unchanged;
- active legal holds block before any local or external destruction progress;
- raw-object deletion is bounded, transaction-free during I/O, absence-checked,
  crash-resumable, and represented by an immutable proof chain;
- equivalent retries resume and completed retries replay exactly;
- changed operation coordinates conflict;
- closing suppresses direct credential changes, operational and projection
  writes, inbox handling, and outbox claims;
- active outbox leases cannot be deleted beneath a publisher;
- one call removes at most one non-empty bounded database batch;
- every tenant-owned database row and raw object is absent at completion except
  lifecycle state and the immutable receipt;
- global ingress control and another tenant remain untouched;
- normal proof mutation and tombstone deletion stay blocked;
- PostgreSQL trigger authorization, migration, lock, foreign-key order, storage
  crash boundaries, replay, transport race, and receipt immutability are proven
  once at the end of the slice; and
- the complete fast Ingestion suite passes before one exact Docker scenario.

## Deferred Production Work

- cross-owner preflight for active bookings, adapters, and legal-hold/operator
  readiness before irreversible execution;
- protected replay delta generation and restore-readiness enforcement;
- counsel-approved provider evidence retention and backup expiry evidence;
- operator API, Admin API, CLI, assurance, retry, and recovery UX; and
- production admission tied to the exact final mandatory-owner catalogue.

## Verification

- migration `20260804100825_AddIngestionTenantDestructionLifecycle` has no
  pending PostgreSQL model changes;
- all 271 fast Ingestion tests pass, including the version 10 personal-data
  catalogue and deterministic inventory;
- the Integration project builds with zero warnings and errors; and
- exact scenario
  `Tenant_destroy_is_storage_safe_bounded_immutable_and_isolated` passes
  against PostgreSQL 16. It proves active-hold and live-outbox-lease blockers,
  physical absence for source and derived raw payloads, the 500-row boundary,
  derived reprocessing graph order, trigger-authorized proof deletion,
  replay/conflict, closed-scope admission, immutable receipt, global-control
  preservation, and cross-tenant isolation.

The earlier export-only container scenario and the broad Docker suite were not
rerun during this slice.

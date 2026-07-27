# Data Rights Multi-Owner Execution Task

Status: implementation and exact-candidate repository gates complete;
destructive multi-account browser proof remains

## Outcome

Complete the DataRights coordinator slice required after Guests, Reservations,
and Ingestion have each proven owner-local anonymisation and restore safety.
One approved case may explicitly select records from those owners, dispatch one
durable work item per selected record, and reach a terminal case state only
after every owner result has a matching durable outcome.

This is also a correctness repair for the existing single-owner path. A
successful owner proof currently receives an in-database and externally
protected ledger entry, but its work item remains `OwnerProofRecorded` and the
case remains `Executing`. Production code must not infer completion from the
task run or from owner mutation alone.

## Boundary Decision

- `DataRights` owns an execution batch, work-item dispatch, aggregate progress,
  terminal case reconciliation, and the protected processing ledger.
- Guests, Reservations, and Ingestion continue to own discovery, current
  eligibility, mutation, immutable owner receipts, tombstones, and restore
  replay.
- Explicit operator selection remains mandatory. Selecting a Reservation does
  not silently select related Ingestion evidence, and selecting a Guest does
  not authorize every matching record.
- BunkFy rights vocabulary, batch semantics, owner routing, and case outcomes
  remain outside GMA. Existing GMA task, messaging, scope, time, identity, and
  persistence primitives are sufficient.
- Inventory and Operations Notifications are later owner slices. They are not
  treated as completed merely because the coordinator can run multiple owners.

## Execution Model

Add one small `DataRightsExecutionBatch` aggregate per case execution. It owns:

- tenant, case, property, approval, and execution revisions;
- the caller's execution idempotency key;
- the immutable selected-work-item count;
- creation actor, time, and optimistic version.

Each selected owner coordinate creates one `DataRightsExecutionWorkItem` linked
to the batch. The work item keeps its owner-local idempotency key, derived
deterministically from the batch key and normalized coordinate. Work-item ids
remain generated opaque ids.

The batch idempotency key is unique per tenant. Equivalent retries return the
existing batch and its ordered work items. Reusing the key for another case,
revision, property, or selected-coordinate set fails closed. One case cannot
acquire a second batch for the same execution.

Work items are ordered by owner, record type, and record id in every response
and reconciliation query. The database stores no identity preview or search
criterion.

## Terminal Lifecycle

1. `Approved` to `Executing` persists the batch, every work item, and one
   prepared outbox event per item in a single transaction.
2. Each GMA task resolves exactly one version-matched owner contributor.
3. `Blocked` and owner-declared `Failed` results become durable work-item
   outcomes. Retryable infrastructure exceptions continue through GMA task
   retry and do not become terminal owner results.
4. A successful owner proof remains non-terminal until the canonical
   in-database ledger entry and externally protected delta both match.
5. Only then does the work item transition from `OwnerProofRecorded` to
   `Completed`.
6. Every terminal work-item transaction emits one PII-free self event.
   Idempotent reconciliation loads the whole bounded batch after commit.
7. While any work item is non-terminal, the case remains `Executing`.
8. All `Completed` or `NoOp` items produce `Completed`.
9. No successful items plus at least one `Blocked` or owner-declared `Failed`
   item produces `Blocked`.
10. A mixture of successful and unsuccessful terminal items produces
    `PartiallyCompleted`.

Reconciliation is event-driven after committed work-item state so concurrent
tasks cannot both observe each other before commit and strand the case in
`Executing`. Duplicate and out-of-order terminal events are idempotent.

## PostgreSQL Upgrade

Migration `20260726214049_AddDataRightsExecutionBatches`:

- adds execution batches and links every work item to one batch;
- preserves existing execution and owner idempotency coordinates;
- backfills one batch per existing single-owner case;
- keeps tenant-first unique indexes for batch idempotency, one batch per case,
  owner idempotency, and one selected coordinate per batch;
- converts legacy `OwnerProofRecorded` work items to `Completed` only when a
  matching in-database ledger entry exists;
- converts a legacy `Executing` case to `Completed` only when its sole work item
  is proven complete;
- leaves blocked, failed, prepared, processing, or unproved owner state
  unchanged; and
- refuses a destructive downgrade when multi-owner batches or terminal
  reconciliation evidence would be lost.

Migration tests must begin from the previous published migration and prove
data-preserving upgrade, fail-closed downgrade, constraints, indexes, and zero
model drift.

## Operator Experience

The privacy-request workflow must expose explicit owner selection:

- Guest Record;
- Reservation; and
- Ingestion source evidence linked by exact Reservation id.

Operators may run more searches while records are selected, review every
selected owner coordinate, and remove individual selections before review.
The UI never auto-selects another owner and never displays raw Ingestion
evidence during discovery.

Execution responses contain the ordered work-item collection. The case detail
shows per-owner progress and keeps live refresh active while any item is
prepared, processing, or waiting for durable ledger completion. Terminal
refresh invalidates Guests, Reservations, Ingestion, Inventory availability,
and privacy-case queries without embedding subject coordinates in URLs or
notifications.

## Security And Efficiency

- Approval and current owner eligibility are revalidated independently for
  every selected coordinate before irreversible work.
- The selection limit remains bounded by `DataRightsCase.MaxSelectedSubjects`.
- The task runtime provides bounded worker concurrency; no unbounded
  `Task.WhenAll` or shared-DbContext parallelism is introduced.
- Task payloads, outbox/inbox messages, work items, batches, metrics, logs, and
  errors remain PII-free and constant-size per selected record.
- External ledger append remains ahead of terminal database state. A crash in
  that window resumes idempotently.
- Reconciliation performs one tenant/case-indexed work-item query and no owner
  table reads.
- Owner modules continue referencing DataRights Contracts only. DataRights
  never reads or writes an owner schema.

## Acceptance

- Domain tests cover batch identity, normalized deterministic owner
  idempotency, selected-set immutability, item completion, and all case outcome
  combinations.
- Handler tests cover one and multiple owners, equivalent replay, changed-set
  conflict, missing/duplicate contributor, blocked/failed mixtures, duplicate
  terminal events, and concurrent reconciliation.
- Real PostgreSQL tests execute Guests, Reservations, and Ingestion work items
  through composition and prove ordered terminal state plus protected ledger
  deltas.
- Restore tests replay ledger deltas for every completed owner before
  readiness.
- API/OpenAPI and frontend tests prove explicit selection, multiple selected
  records, per-owner progress, and live refresh.
- Personal-data catalogues classify every new persisted or transported member;
  log, metric, notification, and source-boundary guards remain green.
- Architecture tests keep product semantics out of GMA and preserve
  contracts-only module dependencies.
- Complete non-Docker, migration, Docker, vulnerability, browser, and
  exact-commit GitHub gates pass before this task is marked complete.

## Current Evidence

- DataRights creates one immutable ordered work item per selected Guest,
  Reservation, or Ingestion coordinate and reconciles the case only after every
  owner records durable proof. The real PostgreSQL, NATS, and Worker integration
  drill covers all three owners in one execution batch.
- The operator workflow exposes explicit owner tabs, keeps prior selections
  while another bounded search runs, removes individual selections, shows
  per-owner work-item progress, and refreshes only non-terminal execution.
- Published backend commit `0f6f0f2191de15a377fa422ae61e655bd7bf9962`
  passed validation and all 63 Docker integration tests. Published web commit
  `de7c98f55efc7a40743d3a23b6db0ae8c8fcae06` passed validation.
- Product commit `2a6e9a8189f5a42e79987e6990ef8d0bd52ffa1e`
  passed the combined repository verifier, Security Baseline, and C# plus
  JavaScript/TypeScript CodeQL analysis.
- A read-only authenticated preview smoke confirmed the request queue, scoped
  intake, selected-record summary, protected-export state, and permission-aware
  operation choices. The destructive multi-owner flow was intentionally not
  executed against the developer's local workspace; separate approver/executor
  browser evidence remains a deployment acceptance activity.

## Non-Goals

- Automatic or fuzzy cross-owner selection.
- Inventory or Operations Notifications owner mutation.
- Tenant termination.
- Protected export artifact assembly.
- New generic GMA orchestration contracts.
- Legal approval of country, retention, or exception policy.

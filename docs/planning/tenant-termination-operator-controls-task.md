# Tenant Termination Operator Controls Task

Status: complete
Date: 2026-08-04

## Goal

Expose the completed tenant-termination coordinator only through audited,
tenant-scoped Admin API and Admin CLI controls. Keep the public product API
free of tenant-destruction commands and keep Production execution disabled
until recovery can reconstruct the exact approved start intent.

## Authority Boundary

- The tenant-termination `DataRightsCase` owns request, review, approval,
  immutable policy-evidence digest, and approver/executor separation.
- `TenantTerminationProcess` owns execution, cancellation, retry, phase status,
  owner work, and terminal proof.
- The protected replay provider stores the exact approved start intent before
  the process transaction. A database restore that retains the approved case
  but predates that transaction can therefore restore its execution state and
  reconstruct the same process coordinates.
- Data Rights validates the exact BunkFy owner catalogue supplied by product
  composition. Required BunkFy owner keys remain outside GMA.
- Admin API and Admin CLI are adapters over the same application commands and
  queries. Neither adapter reads module tables or mutates task rows directly.

## Permissions

Use distinct tenant-scoped permissions for:

- read status and bounded evidence;
- request termination;
- approve or deny a request;
- start irreversible execution;
- retry blocked or failed owner work;
- request pre-destruction cancellation; and
- recover an exact protected start intent and resume coordination.

Approval and execution require different actors. Every mutating adapter also
requires an explicit confirmation flag; the Admin API host applies its
configured recent privileged-authentication assurance to all admin operations.

## Workflow

1. Request creates one active tenant-termination case and records whether a
   final export is required. Tenant termination never invents subject rows.
2. Approval records a canonical digest over bounded, non-secret approval,
   owner-catalogue, backup, restore-drill, and operator-assurance references.
3. Execution verifies the live owner catalogue, requires an actor distinct
   from the approver, protects the exact start intent, then atomically creates
   the process, moves the case to executing, and emits the coordinator wake-up.
4. Retry requeues only blocked or failed owner work in the current operation;
   completed work and deterministic identities remain unchanged.
5. Cancellation is allowed only at the documented reversible boundary and
   completes only after the Workspaces restore owner returns exact proof.
6. Recovery validates the protected intent, restores the retained approved
   case to execution, recreates the missing process with the same identities,
   and emits the same coordinator signal. A missing approved case fails closed;
   existing dispatch/result replay remains authoritative.
7. Status returns bounded case, process, owner-work, replay-readiness, and
   checkpoint evidence without removed identifiers or free text.

## Invariants

- One non-terminal tenant-termination case and one active process per tenant.
- Request id, process id, execution idempotency key, and termination epoch are
  immutable and exact-replay checked.
- Approved evidence references are validated before hashing and are not copied
  into process rows, task payloads, logs, notifications, or status responses.
- The approved owner-catalogue SHA-256 must equal the live complete catalogue
  before execution or reconstruction.
- A protected intent is durable before the database transaction; an orphaned
  intent is recoverable and cannot authorize different coordinates.
- Retry preserves the current operation revision, work-item ids, task-run
  sequence, and owner idempotency keys.
- Recovery never fabricates or edits owner results. Task handlers consume the
  exact authenticated replay entries already recorded.

## Verification Cadence

- focused domain and application tests while each control is implemented;
- one migration/model-drift check after persistence is coherent;
- one exact PostgreSQL crash-boundary scenario for protected-intent recovery;
- one focused Admin API/CLI composition check; and
- one repository-wide non-Docker gate at the end of the complete slice.

## Implemented Slice

- Dedicated Admin API routes and Admin CLI commands cover status, request,
  decision, start, retry, cancellation, and exact-intent recovery.
- Every mutation is independently permissioned and explicitly confirmed;
  approval/start/recovery require bounded evidence references.
- Both operator hosts compose the exact product owner catalogue. The
  destructive Production admission gate remains Worker-only.
- The public product host composes the same read-side catalogue so Data Rights
  can validate its complete application graph, but supplies an explicit
  fail-closed scheduler because Task Runtime execution is intentionally absent
  from that profile.
- Recovery reconstructs an orphaned protected start intent, re-signals active
  work, directly resumes a persisted verification boundary, and refuses
  blocked or failed work until an explicit retry.
- Status validates case/process/work-item consistency and omits raw evidence,
  actor identifiers, tenant identifiers, free text, and replay contents.
- Data-governance catalogue version 19 classifies all operator commands,
  actor attribution, protected replay metadata, and bounded status models.
- The generic Notifications Admin CLI adapter is composition-only and is
  verified not to expose invented commands.
- Data Rights outbox publishing now forwards GMA scope resolvers, so the
  coordinator wake-up remains tenant-scoped in durable storage.

## Verification Evidence

- Data Rights module tests: 466 passed.
- Architecture tests: 91 passed.
- Generic Notifications module tests: 115 passed.
- The Admin API composes and reaches its HTTP listeners; absent local
  PostgreSQL produces only expected background retry logs.
- The exact PostgreSQL recovery scenario proves that an encrypted intent can
  predate the relational start transaction, recovery persists the exact case
  transition, process coordinates, and tenant-scoped outbox wake-up, and an
  explicit replay preserves one process and one intent while safely
  re-signalling coordination.
- Exact PostgreSQL persistence scenarios prove the current case/process
  lifecycle, one-active-case and one-active-process fences, freeze checkpoint,
  cancellation, immutable receipt, and tenant-isolation boundaries.

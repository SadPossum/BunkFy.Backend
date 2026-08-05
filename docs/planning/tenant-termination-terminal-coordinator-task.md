# Tenant Termination Terminal Coordinator Task

Status: complete; production activation intentionally disabled pending private
admission evidence and an external replay provider
Date: 2026-08-04

## Goal

Turn the completed tenant-owner catalogue into one restartable Data Rights
control plane without moving BunkFy policy into GMA or exposing a partially
safe production route.

## Boundaries

- Data Rights owns the process, dependency scheduling, bounded owner work,
  protected replay, terminal verification, and operator-facing status.
- Every contributor remains authoritative for its own data, local batching,
  legal holds, idempotency, and retained proof.
- Task Runtime owns durable task runs, retries, leases, and controls. Data
  Rights stores one work item per owner and phase, never one row per removed
  business record.
- Workspaces remains the tenant-processing fence authority.
- GMA may own only reusable task, lock, lifecycle, or protected-journal seams.
  BunkFy owner keys, ordering, policy, and readiness rules stay in BunkFy.

## Slices

1. **Complete.** Correct operation revision semantics so every phase operation is newer than
   its approved case revision, then add a pure transactional phase/work kernel.
2. **Complete.** Add an external append-only protected replay journal. Persist an encrypted
   exact dispatch before any irreversible owner call and an authenticated result
   before central work becomes terminal.
3. **Complete.** Add the owner task handler and restartable reconciler. Schedule only owners
   whose dependencies are complete, use stable deduplication identities, and
   recover safely from every commit/enqueue boundary.
4. **Complete.** Seal a PII-minimised terminal receipt and restore checkpoint only after an
   exact mandatory-owner verification pass and closed Workspaces proof.
5. **Complete.** Add separate request, approval, execution, retry, cancellation, recovery,
   and read permissions across Admin API and Admin CLI. Keep the public API
   free of tenant-destruction commands.
6. **Complete.** Add production admission for the exact owner catalogue, replay-store
   readiness, key material, Worker group, backup evidence, and operator
   assurance. Repository defaults remain disabled.

## Current checkpoint

- The phase planner, deterministic owner work identities, authenticated replay
  journal, owner executor, transactional phase reconciler, and durable
  coordinator wake-up are implemented.
- Freeze completion now atomically persists the Workspaces fence revision and
  the exact ordered Export contributor catalogue with its canonical digest.
- Export fragments and artifact confirmation are bound to that immutable freeze
  checkpoint, so later contributor drift cannot silently change the export.
- Central Verify replays every exact protected owner result, compares it with
  the live owner proof, requires a trusted replay checkpoint, and seals one
  immutable terminal receipt before the process can complete.
- Worker admission validates the exact 12-owner BunkFy catalogue, terminal
  Workspaces sink, export catalogue, task worker group, production-grade replay
  store, and bounded approval, backup, restore-drill, and operator evidence.
- Separate operator permissions, Admin API/CLI commands, bounded status, and
  exact protected-intent recovery are implemented and verified. Execution
  remains disabled by repository default until a deployment supplies the
  private approval package, production-grade replay provider, key material,
  backup/restore evidence, and the exact admitted catalogue digest.

## Invariants

- `OperationRevision` starts at `ApprovalRevision`; the first phase operation
  is therefore `ApprovalRevision + 1` and every retry starts a newer operation.
- Work-item ids, idempotency keys, task run ids, and task deduplication keys are
  deterministic from bounded process/phase/owner coordinates.
- Task Runtime destruction and the dependent final Workspaces destruction run
  as global control tasks that resolve and establish the exact tenant context;
  they are never stored inside the Task Runtime scope they close.
- A contributor result is accepted only for the exact descriptor catalogue,
  current process operation, task run attempt, policy digest, and tenant scope.
- Independent owners may run concurrently, but no owner starts before all of
  its phase dependencies complete.
- `RetryRequired` resumes the same owner work item and idempotency key. Blocked
  or failed work requires an explicit operator requeue.
- A phase completes only when every planned owner has exact terminal proof and
  reports no remaining active records.
- Destruction cannot start unless the protected replay store is production
  ready and the exact dispatch intent is durable.
- Restored deployments fail readiness until replay has re-established the
  fence and converged every recorded destructive dispatch.
- No removed identifiers, free text, credentials, or owner-local cursors enter
  Data Rights rows, task payloads, logs, metrics, notifications, or terminal
  receipts.

## Verification

- focused domain and coordinator tests during each sub-slice;
- one PostgreSQL/Task Runtime crash-boundary scenario after the coordinator is
  coherent;
- one protected-store tamper, truncation, idempotency, and restore scenario;
- production composition and admission tests; and
- the repository-wide non-Docker gate once at the end.

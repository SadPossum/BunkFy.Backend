# Task Runtime Tenant-Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Compose GMA Task Runtime's generic scope lifecycle into BunkFy's frozen
workspace termination flow without exporting opaque operational task copies or
moving workspace policy into GMA.

## Ownership

- GMA Task Runtime owns scoped run and control-message persistence, shared-lock
  admission, lease draining, bounded destruction, and immutable payload-free
  proof.
- Task-owning domain modules remain authoritative for the business meaning of
  task payloads, progress, and errors and own any portability export.
- BunkFy maps the exact canonical workspace id to `ScopeId`, validates the
  frozen Workspaces fence, orders the owner after Access Control, and maps
  generic progress into Data Rights results.
- The extension owns no database and references only Task Runtime Contracts.
  It registers no export contributor.

## Contract

- The workspace id must be the lower-case `D` GUID form and equal the ambient
  scope.
- The first accepted call closes enqueue, retry, control-message, and claim
  admission before deletion starts.
- Active leases return retryable busy progress until their workers cancel,
  complete, fail, or time out.
- One invocation removes at most one non-empty bounded batch, preserving the
  exact operation id, selected/resulting revision, stage, counts, and proof.
- Completion and replay are accepted only for the exact frozen fence and
  immutable generic receipt.
- Final cleanup executes outside the target workspace task scope so the owner
  never deletes the task run that must record its own completion.

## Evidence

- 15 focused adapter and executable-catalog tests pass;
- five complete-topology composition tests pass, including resolution against
  `Gma.Modules.TaskRuntime.Persistence` and deliberate absence from the export
  contributor set;
- 28 focused host and personal-data output guards pass;
- all 27 GMA Task Runtime fast tests and all 1,096 GMA Framework tests pass,
  and the integration project builds with zero warnings; and
- the exact PostgreSQL lifecycle scenario proves upgrade backfill, scoped
  concurrent deduplication, admission and claim fencing, active-lease drain,
  bounded deletion, cross-scope safety, and replay/conflict proof.

## Deferred

- Terminal cross-owner orchestration. The generic Files owner is not part of
  BunkFy's current composition; its admission boundary is documented
  separately.
- Product-approved retention values for task operational copies.
- A SQL Server container rerun of the new lifecycle scenario; the provider
  migration and key-lock path compile and remain model-drift-free in this slice.

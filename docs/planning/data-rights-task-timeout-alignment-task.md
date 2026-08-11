# Data Rights Task Timeout Alignment Task

Status: complete
Date: 2026-08-11

## Goal

Allow bounded Data Rights owner workflows to use the execution budgets already
promised by their domain contracts instead of being canceled by GMA's shorter
worker-wide fallback. Keep every Data Rights coordinate and deadline in the
module while consuming GMA's generic per-handler timeout registration.

## Audit Findings

- anonymisation owner execution has an explicit two-minute deadline;
- tenant-termination owner execution and export-fragment generation have an
  explicit two-minute deadline;
- terminal tenant-termination verification may sequentially revisit up to the
  contract maximum of 64 frozen owners, with a fresh two-minute verification
  deadline for each owner;
- all of those task handlers currently inherit the 30-second worker fallback,
  so the runtime can cancel valid work before the module deadline; and
- retries preserve safety but cannot make the promised deadline usable when
  every attempt has the same shorter outer boundary.

## Ownership

- GMA Tasks owns generic handler-timeout metadata and enforcement.
- Data Rights Application owns the timeout policy derived from its owner-call
  deadlines, contributor-count bound, and terminal-completion margin.
- Data Rights Contracts and Domain retain the existing contributor and frozen
  owner limits.
- Host configuration remains the fallback for handlers without a module-owned
  execution budget.

## Invariants

1. Each anonymisation or tenant-termination owner task receives its existing
   two-minute owner budget plus a short completion margin.
2. Verification covers no more than the existing maximum contributor count
   multiplied by the per-owner verification deadline, plus the same margin.
3. Owner-level cancellation continues to enforce the exact request deadline;
   the task-handler timeout remains an outer recovery boundary.
4. Task identities, payload versions, retries, heartbeat, tenant scope,
   protected replay, and persistence contracts do not change.
5. Short bounded tasks continue to use the worker-wide fallback.

## Delivery

1. Add one internal Data Rights task-execution policy derived from existing
   domain constants.
2. Apply the single-owner budget to anonymisation and tenant-termination owner
   handlers.
3. Apply the bounded aggregate budget to terminal verification.
4. Add composition tests for exact handler coverage and fallback preservation.
5. Update Data Rights development notes and run focused verification before
   one consolidated non-Docker slice gate.

## Deferred

- export-artifact generation, which is bounded by records and bytes but has no
  honest execution-time contract yet;
- projection rebuild execution, which should remain batch/continuation owned;
- changing verification into owner-specific fan-out tasks without measured
  throughput or recovery evidence; and
- global worker timeout changes for unrelated modules.

## Completion Criteria

- every existing bounded owner deadline fits inside its handler boundary;
- only the six intended long-running handlers receive an override;
- short Data Rights handlers retain the configured worker fallback;
- focused Data Rights and architecture tests pass;
- consolidated non-Docker verification passes; and
- no GMA repository, task payload, or persistence schema changes.

## Verification

- focused Data Rights module suite: 480 passed, 0 failed;
- architecture suite: 102 passed, 0 failed;
- backend solution synchronization: passed; and
- consolidated non-Docker verification: passed with a zero-warning build,
  clean migration drift, GMA framework 1,123/1,123, Data Rights 480/480,
  Architecture 102/102, Operations Notifications 100/100, and Integration
  60/60.

Docker verification was not rerun because this slice changes compiled handler
composition only and does not alter persistence, messaging, or host data flow.

# Retention Task Timeout Alignment Task

Status: complete
Date: 2026-08-11

## Goal

Ensure the Retention control-plane task can honor every valid owner-contributor
deadline without raising the timeout of unrelated BunkFy task handlers. Reuse a
generic GMA task-registration capability and keep all Retention coordinates and
policy in the BunkFy module.

## Audit Findings

- Retention contributors declare bounded execution deadlines of ten or fifteen
  minutes, and the reusable descriptor contract permits up to one hour.
- `BunkFy.Host.Worker` currently inherits GMA's 30-second task-handler timeout.
- A contributor that runs beyond 30 seconds is canceled by the worker before
  Retention's own deadline. Task Runtime schedules a retry, but the owner call
  cannot use the execution budget promised by the Retention contract.
- Small development datasets complete quickly enough to hide this mismatch.
- Raising the worker-wide timeout would also lengthen recovery for every other
  handler that still relies on the global default.

## Ownership

- GMA owns optional handler-registration timeout metadata and generic worker
  enforcement.
- Retention Contracts owns the maximum valid contributor execution timeout.
- Retention Application registers its one task handler with that maximum plus a
  bounded completion margin for persisting the terminal receipt.
- BunkFy host configuration and unrelated tasks remain unchanged.

## Invariants

1. A contributor deadline cannot exceed the Retention contract maximum.
2. The Retention handler timeout covers the maximum contributor deadline and a
   short terminal-completion margin.
3. Contributor-level cancellation still fires at the exact descriptor deadline;
   the worker timeout remains the outer recovery boundary.
4. Existing task identity, payload version, schedule identity, retries, tenant
   context, heartbeat, and persistence contracts do not change.
5. No Retention owner key, data class, or policy enters GMA.

## Delivery

1. Consume GMA's optional task-handler timeout registration.
2. Name and reuse the Retention maximum execution timeout in descriptor
   validation.
3. Register `ExecuteRetentionScheduleTaskHandler` with maximum deadline plus
   completion grace.
4. Add exact contract tests for descriptor bounds and handler registration.
5. Update Retention development documentation and run focused tests before one
   consolidated non-Docker slice gate.

## Deferred

- Retention execution-history cleanup until evidence periods and hold policy
  receive product and legal approval;
- changing the schedule-health snapshot query shape without measured scale
  evidence;
- changing Task Runtime persistence or retry semantics; and
- assigning explicit timeouts to unrelated task handlers before their owning
  domains are audited.

## Completion Criteria

- every currently valid Retention contributor deadline fits inside the handler
  boundary;
- unrelated task handlers retain the worker-wide fallback;
- focused GMA and Retention tests prove override and fallback behavior;
- consolidated non-Docker verification passes; and
- all unrelated GMA repositories remain clean.

## Verification

- focused GMA Tasks contract and worker suite: 121 passed, 0 failed;
- focused Retention module suite: 44 passed, 0 failed;
- architecture suite: 102 passed, 0 failed;
- GMA and BunkFy solution synchronization checks: passed; and
- consolidated non-Docker verification: passed with a zero-warning build,
  clean migration drift, GMA framework 1,123/1,123, Retention 44/44,
  Architecture 102/102, Operations Notifications 100/100, and Integration
  60/60.

Docker verification was not rerun because this slice changes compiled handler
composition only and does not alter persistence, messaging, or host data flow.

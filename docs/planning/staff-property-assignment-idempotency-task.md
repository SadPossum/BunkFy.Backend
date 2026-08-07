# Staff Property Assignment Idempotency Task

Status: complete
Date: 2026-08-07

## Goal

Make direct Staff property assignment and unassignment safe to retry after an
uncertain response without duplicating assignment history or acknowledging a
different change as the requested operation.

## Audit Finding

Staff already serializes assignment mutations with the per-member lock and
commits assignment history and outbox events in one transaction. Direct assign
and unassign commands still have no durable operation identity and return a
mutable directory projection. A response lost after commit therefore leaves
callers unable to distinguish success from a stale retry.

The aggregate also accepts matching assignment state, and any historical
unassignment, before checking the selected Staff version. That makes a distinct
stale request look successful. Workspace onboarding reconciliation does not
share this flaw: it is a desired-state integration command that computes a diff
under the same member lock and is naturally replay safe.

## Ownership

- Staff owns assignment request equivalence, immutable success receipts,
  assignment history, domain events, and projection visibility.
- Properties remains the authority for whether a property is currently active.
  Staff consumes only its existing local projection and does not query the
  Properties database.
- Workspaces keeps ownership of onboarding access plans and continues to call
  the existing reconciliation contract. It does not provide operation ids for
  direct Staff management commands.
- Public API, Admin API, Admin CLI, and web callers supply one non-empty
  operation id for each logical direct assignment attempt.
- GMA continues to own generic transactions, optimistic concurrency,
  persistence retries, and outbox/inbox behavior. Staff-specific assignment
  semantics remain in BunkFy.

## Invariants

1. The idempotency coordinate is `(ScopeId, StaffMemberId, OperationId)`.
2. Assign requests pass current property availability and operational Staff
   visibility before a completed receipt is disclosed.
3. Unassign requests keep the existing fail-closed safety-transition Staff
   visibility path so access reduction remains possible for suspended members.
4. The per-member mutation lock is acquired before reading the operation
   journal or aggregate state.
5. Assign equivalence includes action kind, Staff member, property, expected
   Staff version, normalized property job title, primary flag, and effective
   date.
6. Unassign equivalence includes action kind, Staff member, property, expected
   Staff version, effective-through date, and normalized reason.
7. Actor identity is validated on every attempt but is not stored in the
   request fingerprint or operation journal.
8. An exact completed replay returns the immutable minimal Staff mutation
   receipt and emits no second assignment event.
9. Reusing an operation id for another kind or payload returns one stable
   conflict mapped to HTTP 409.
10. Invalid, stale, unavailable, or failed attempts do not bind an operation
    id because no Staff assignment mutation committed.
11. A matching PUT assignment at the current Staff version remains a truthful
    no-op and records a receipt without advancing the version. A stale matching
    assignment is rejected before the no-op check.
12. A new unassign command with no current assignment returns the existing
    assignment-not-found result. Only a matching completed operation may replay
    a prior unassignment as success.
13. The successful Staff mutation, assignment event, and journal row commit
    atomically in the Staff transaction.
14. Reconciliation remains a desired-state command. Exact replays produce an
    empty diff, and transient persistence conflicts may rerun it with a clean
    tracker.
15. The journal stores no title, reason, actor, or property id in plaintext;
    request details are represented only by a canonical SHA-256 fingerprint.

## Surfaces

- Add `OperationId` to direct assign and unassign application commands.
- Require it in public and Admin API request contracts.
- Require `--operation-id` in Admin CLI assignment commands.
- Return `StaffMemberMutationReceiptDto` from direct assignment commands and
  endpoints; callers refetch current Staff detail after success.
- Keep one web operation id and the attempt's original expected version while
  an unchanged submission is retried. Allocate a new id when the selected
  member/property, action, normalized values, or successful attempt changes.
- Mark assign, unassign, and reconciliation commands for the Staff persistence
  retry behavior.

## Persistence

Extend `StaffMemberMutationKind` with assign-property and unassign-property
values and widen the PostgreSQL kind constraint. Reuse the existing append-only,
tenant-filtered member mutation journal. Existing export, retention, cascade,
and tenant-destruction behavior applies without a new table.

## Verification

- Focused domain and application tests cover current-version no-op assignment,
  stale no-op rejection, exact replay, changed/cross-kind conflict, failed
  attempt reuse, visibility before replay, and no duplicate events.
- Reconciliation tests cover desired-state replay and explicit persistence
  retry eligibility.
- API, Admin API, CLI, contract, and web tests cover operation ids, receipt
  responses, and stable browser attempt behavior.
- Persistence tests cover the new kinds and migration constraint.
- One focused PostgreSQL integration scenario proves concurrent exact retries,
  atomic rollback, tenant isolation, immutable receipts, and one assignment
  event per committed transition.
- Use cheap focused checks while editing. Run one consolidated non-Docker slice
  gate and one targeted Docker scenario at completion.

## Not In This Slice

- changing property-retirement coordination or closing historical Staff
  assignments when a property retires;
- editing an existing assignment in place;
- changing Workspace onboarding or access-plan contracts;
- generic GMA idempotency or response-cache infrastructure.

## Completion Evidence

- Solution synchronization and source-package ownership checks passed. The
  serial solution build completed with zero warnings and zero errors, and every
  configured PostgreSQL and SQL Server migration project reported no pending
  model changes.
- All 242 Staff tests, all 94 architecture tests, and the complete 4,715-test
  non-Docker backend matrix passed. The migrated Operations Notifications
  contributor remained green at 99/99 tests.
- The targeted PostgreSQL scenario passed 1/1 and proved legacy migration,
  concurrent exact assignment and unassignment retries, atomic outbox rollback,
  tenant isolation, immutable receipts, one event per committed transition, and
  cascade cleanup.
- Generated OpenAPI and TypeScript contracts are current. Web lint, type
  checking, all 197 tests across 35 files, and the production build passed.
- GMA source remained unchanged. Staff owns assignment equivalence and durable
  receipts; Workspaces keeps desired-state reconciliation, Properties keeps
  property authority, and GMA keeps generic transaction, lock, retry, and
  outbox behavior.

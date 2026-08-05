# Data Rights Deadline Alerts Task

Status: completed
Date: 2026-08-05

## Goal

Turn authoritative Guest Rights response deadlines into durable, bounded, and
actionable operational alerts without leaking privacy-request details or
granting notification visibility beyond Data Rights permissions.

## Ownership

- Data Rights owns deadline-state evaluation, durable dispatch receipts, and
  the PII-minimal integration event.
- Operations Notifications owns recipient selection, wording, severity,
  mandatory delivery, notification payloads, and navigation metadata.
- Access Control owns generic permission evaluation. Its existing batch API is
  retained while its persistence query is optimized for many subjects sharing
  one scope and permission set.
- The web app owns live invalidation, exact request navigation, and transient
  visual focus.
- No BunkFy deadline, privacy-request, or notification policy belongs in GMA.

## Decisions

### Durable Deadline Detection

- Scan only external, property-scoped Guest Rights cases that are still open,
  have immutable deadline evidence, and fall within the configured due-soon
  horizon.
- Record one durable receipt per case and alert kind before publishing the
  corresponding outbox event. A unique database constraint is the final
  duplicate guard.
- Emit `DueSoon` while the deadline is in the future and within 48 hours. Emit
  `Overdue` once the deadline has passed. A delayed worker emits only the
  current state rather than replaying a stale due-soon alert.
- Recheck case eligibility transactionally when claiming work. Terminal cases
  do not produce deadline alerts.
- Run one recurring tenant-scoped task with bounded batches and a bounded
  number of batches per invocation.

### Secure Notification Delivery

- Resolve current tenant members and active Staff records through the existing
  Operations Notifications projection path.
- Deliver deadline alerts only to recipients authorized for
  `data-rights.read` at the affected property scope.
- Use mandatory delivery for these compliance alerts; ordinary operational
  notices continue to respect notification preferences.
- Do not include requester identity, requested rights, free text, or policy
  evidence in the event or notification payload. Keep only the property and
  case identifiers needed for authorization and navigation.

### Efficient Authorization

- Keep `IAccessAuthorizationService.AuthorizeManyAsync` as the public API.
- Optimize Access Control persistence internally by batching subjects that
  share a subject kind, scope, and permission query rather than issuing one
  database query per subject.
- Prove the optimization with query-count coverage. Do not add a BunkFy-only
  authorization reader or a new framework abstraction.

### Operator Experience

- Route notification actions to the affected Guest Rights queue and exact
  case.
- Invalidate the queue and case queries on live delivery.
- Open the referenced case and apply the existing transient resource focus so
  staff can identify it immediately without leaving a permanent highlight.

## Delivery

- [x] Optimize generic Access Control multi-subject permission reads and add
  focused coverage.
- [x] Add the Data Rights alert receipt, scheduler task, transactional claim,
  outbox event, PostgreSQL migration, and focused tests.
- [x] Add permission-filtered Operations Notifications handlers, payload,
  catalog/export alignment, and focused tests.
- [x] Add notification navigation, live invalidation, exact-case opening, and
  transient focus in the web app.
- [x] Align concise module documentation and run the coherent slice gate.

## Deferred

- Staff Rights deadlines remain blocked on an explicit employment-jurisdiction
  model and do not borrow a property deadline.
- Statutory extensions, pauses, business-day calendars, and superseding
  deadline history require explicit policy rules before they can produce new
  alert kinds.
- Escalation chains and external channels remain separate notification-policy
  work; this slice uses the existing in-product delivery infrastructure.

## Completion Criteria

- each eligible case produces at most one due-soon and one overdue alert;
- a delayed worker never emits a stale due-soon alert after the deadline;
- terminal, policy-pending, tenant-scoped, and controller-initiated cases are
  excluded;
- only current recipients with property-scoped Data Rights read permission
  receive the alert;
- notification payloads contain no guest or request-content PII;
- selecting an alert opens and briefly focuses the exact privacy request;
- multi-recipient authorization remains bounded and query-count tested;
- the PostgreSQL migration, focused Docker scenario, backend gate, and web gate
  pass once at the coherent slice boundary.

## Verification Cadence

Use focused non-Docker tests while editing. Run one exact PostgreSQL Docker
scenario because this slice changes the Data Rights schema. At the coherent
slice boundary, run one full non-Docker backend gate and one full web gate;
avoid repeating expensive gates after every change.

## Verification

- The exact PostgreSQL alert-dispatch scenario passed with transactional
  rollback, current-state dispatch, deduplication, terminal-case exclusion,
  and guarded downgrade coverage.
- Solution synchronization, source-package checks, the full build, and all
  migration drift checks passed. Every non-Docker backend test project passed;
  after the migrated command session closed, the unreported tail was rerun
  from Staff through the integration suite.
- The full web verification passed type checking, lint, 150 tests across 23
  files, and the production build.

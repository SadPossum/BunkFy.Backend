# Ingestion Sensitive History Retention Task

Status: implemented

## Goal

Bound the lifetime of normalized reservation PII in terminal Ingestion proposals and dispatches without erasing operational and decision audit history. Keep active workflow evidence available until the workflow is genuinely terminal.

## Sensitive And Permanent Fields

Sensitive bodies:

- `ChangeProposal.Diff`, because it contains the incoming normalized guest/stay snapshot;
- `ReservationDispatch.NormalizedSnapshot`, because it contains the normalized product command source.

Permanent non-sensitive audit facts:

- tenant, property, connection, receipt, reservation, source-link, trigger, and product-operation identities;
- proposal reason code, state, actor, decision reason, optimistic version, and lifecycle timestamps;
- dispatch kind, state, source revision/sequence, expected/result revisions, result version, error code, and lifecycle timestamps.

The proposal creation reason currently lives inside the sensitive diff JSON. Move it to a bounded `ReasonCode` field before redaction so audit meaning survives after the diff is removed.

## Lifecycle Decision

- Pending and applying proposals are active and must keep their diff and have no redaction deadline.
- Pending dispatches are active and must keep their snapshot.
- An accepted cancellation dispatch remains active until the correlated reservation-cancelled fact changes it to applied.
- Every other proposal or dispatch terminal transition assigns a persisted sensitive-history deadline from the configured policy.
- A terminal row is redacted only after its persisted deadline. Redaction nulls the sensitive body and records `SensitiveDataRedactedAtUtc`; it does not delete the row.
- Retention configuration changes affect future terminal transitions only. They never silently rewrite persisted deadlines.

`Ingestion:Retention:SensitiveHistoryRetention` defaults to 90 days and accepts the same one-hour through ten-year safety range as raw-payload retention. Raw payload and normalized-history deadlines remain independent.

## Redaction Processing

Add the tenant-scoped recurring `redact-expired-reservation-history` TaskRuntime payload. Each transactional batch selects due unredacted proposals and dispatches together in deterministic deadline order, invokes aggregate redaction transitions, and commits through the existing Ingestion unit of work.

Redaction is database-only, so it does not need the external-object two-phase claim used by raw payload deletion. Aggregate concurrency tokens prevent a stale retention transaction from racing a lifecycle transition. Re-running a task after commit is idempotent because redacted rows are no longer candidates.

The Admin API and confirmation-gated Admin CLI enqueue the task through the existing `ingestion.retention.manage` permission. Connection health reports claimable/protected raw evidence and due/already-redacted sensitive-history counts as factual evidence, not an inferred compliance verdict.

## Migration

The PostgreSQL migration must:

- add proposal reason, deadline, and redaction timestamps;
- add dispatch deadline and redaction timestamps;
- make sensitive body columns nullable only for redacted terminal rows;
- recover existing proposal reason codes from valid diff JSON, otherwise use `legacy-unknown`;
- backfill existing terminal deadlines from `CompletedAtUtc + 90 days` while leaving active deadlines null;
- add lifecycle check constraints and tenant/connection retention indexes;
- retain existing sensitive bodies until the redaction task processes due rows.

## Security And Reliability Invariants

- active workflow evidence cannot be redacted;
- terminal sensitive data cannot remain indefinitely without a persisted deadline;
- redacted rows retain enough non-sensitive facts to explain what happened and why;
- public reads explicitly distinguish available from redacted sensitive content;
- tenant query filters apply to candidate selection and operator counts;
- the task has bounded batches and deterministic ordering;
- no cross-module query, foreign key, or provider concern enters domain/application code;
- legal holds are not simulated with very long deadlines.

## Acceptance Checks

- every terminal proposal and dispatch transition persists a valid future deadline;
- accepted cancellations receive a deadline only after final cancellation confirmation;
- due terminal bodies redact idempotently while active or not-yet-due bodies remain;
- proposal reason, state, actor, decision reason, correlation, and timestamps survive redaction;
- PostgreSQL constraints reject active/deadline and redacted/body inconsistencies;
- migration backfill covers existing active and terminal rows;
- API/Admin proposal reads expose redaction status without leaking removed content;
- Admin API/CLI can confirmation-gate task enqueue;
- health counts due and redacted proposal/dispatch history per connection;
- build, migration drift, fast tests, complete Docker tests, dependency guards, and submodule checks pass.

## Legal-Hold Follow-Up

Explicit property-scoped holds, retention-query exclusion, dedicated Admin operations, held health counts, and the node-local quarantine decision are implemented in [Ingestion Legal Holds Task](ingestion-legal-holds-task.md).

## Completion Note

Implemented with PostgreSQL migration `20260712074124_AddSensitiveHistoryRetention`. Proposal creation reason is now permanent non-sensitive audit data, while full proposal diffs and dispatch snapshots carry persisted terminal deadlines and explicit redaction timestamps. Active proposals, pending dispatches, and accepted cancellations remain unredactable.

The tenant-scoped TaskRuntime handler redacts due rows in bounded deterministic batches. Admin API and confirmation-gated Admin CLI enqueue it through the existing retention permission, proposal list contracts omit diff bodies, detail reads expose availability/redaction state, and connection health reports due/redacted counts. Unit coverage proves lifecycle and task behavior; PostgreSQL coverage proves legacy backfill, constraints, command-driven redaction, health projection, and unchanged reservation workflows.

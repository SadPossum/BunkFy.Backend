# Retention Module

Status: retention scheduling and execution control plane implemented;
tenant-termination export and destruction owner implemented; production
termination activation remains deferred; online mutations serialized by
target, execution, and schedule coordinates

Retention owns BunkFy's schedule state and execution history for invoking
retention work in the modules that own the affected data. It does not own the
business records being retained, anonymised, or deleted.

The module persists two authoritative tenant-owned record families:

- `RetentionExecution`, the bounded attempt and outcome history for one owner,
  data class, policy version, and tenant or property target; and
- `RetentionScheduleState`, the current scheduling and health state for that
  same coordinate.

Organization and property records in Retention are rebuildable projections.
The inbox is transport state. Neither is part of Retention's tenant portability
fragment.

## Operational Surfaces

Schedule health is a derived, tenant-scoped snapshot over the registered owner
coordinates and Retention's scalar schedule-state projection. The public and
Admin lists use stable ordering, normalized page bounds, and one-record
lookahead for truthful `HasMore`; they do not issue an exact-count query or load
execution aggregates. Each page carries a whole-snapshot summary so operator
metrics do not change meaning while paging.

The health contract exposes the nullable `LastRunId` already persisted in
schedule state. That value is the GMA Task Runtime coordinate used by the Admin
retry facade; Retention does not duplicate run or retry lifecycle ownership.
Public and Admin responses are non-cacheable, Admin outcomes have explicit
contracts, and the CLI supports the same paging and run coordinate.

Contributor ordering and duplicate-coordinate validation are shared by health
reads and schedule discovery and fail closed before either surface proceeds.
Generic Task Runtime provider streaming, bounded scheduler memory, and
Retention execution-history cleanup remain separate follow-up concerns.

## Tenant Termination

Retention is a mandatory `Export` and `Destroy` contributor after Ingestion.
Its versioned tenant export contains exactly two deterministic streams:

1. retention executions; and
2. retention schedule states.

The export deliberately excludes tenant and property projections, inbox
messages, and the local tenant revision row. Tenant and property identifiers
remain elevated coordinates owned authoritatively by Organizations and
Properties.

Destruction closes local admission and removes inbox messages, schedule state,
execution history, property projections, and tenant projections in
foreign-key-safe stages. One invocation removes at most one non-empty batch of
500 records. Completion retains only the closed lifecycle row and one
immutable, PII-free destruction receipt with a versioned SHA-256 proof chain.
Personal-data catalogue version 3 classifies every retained proof member under
the dedicated tenant-destruction policy.

Relational writes use the shared BunkFy tenant-mutation transaction key, while
export selection and lifecycle transitions use its exclusive counterpart.
Every operational save re-reads the Workspaces termination fence under that key
and fails closed when admission is unavailable. Successful units of work
advance one scope-filtered monotonic tenant revision. Export uses a
repeatable-read transaction, validates the exact frozen process and epoch before
and after streaming, and returns only an unchanged revision as proof. Closing
and closed scopes are excluded from schedule discovery and inbox admission.

Online decisions also use Retention-owned transaction-key coordinates before
loading mutable state. Scheduled starts acquire shared tenant/property target
fences, then exclusive execution and schedule fences; completion acquires the
execution fence before deriving and acquiring its schedule coordinate.
Organization and property inbox consumers acquire the corresponding exclusive
target fence before merging source versions. This prevents stale aggregate
decisions and first-use projection races without adding lock rows or changing
Task Runtime ownership. The tenant revision still serializes only the final
short save/commit needed for coherent export evidence.

Migration `AddRetentionTenantExportRevision` adds the local revision table.
Migration `AddRetentionTenantDestructionLifecycle` adds lifecycle state,
resumable destruction operation state, and the receipt ledger. PostgreSQL also
enforces receipt immutability with an append-only trigger.

All 42 Retention tests pass and EF reports no pending model changes. The exact
PostgreSQL 16 mutation scenario proves same-schedule and same-property writers
wait, reload the committed winner, preserve independent projection streams,
and leave unrelated coordinates available. The earlier destruction scenario
passed on 2026-08-04 with migration,
shared/exclusive lock drain, bounded 500-row removal, schedule suppression,
deterministic replay and conflict, raw-SQL append-only enforcement, closed-scope
rejection, and tenant isolation. The earlier export scenario remains unchanged.

Production termination task and route registration remain disabled until
remaining product owners, terminal orchestration, protected replay, operator
controls, and final production admission are complete.

See [Tenant Termination Control Plane](../../../docs/planning/tenant-termination-control-plane-task.md).

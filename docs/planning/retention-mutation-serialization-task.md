# Retention Mutation Serialization Task

Status: complete

## Goal

Make Retention execution, schedule-state, and scope-projection decisions
continue from authoritative state under transaction-scoped coordinates, while
keeping unrelated targets, schedules, and runs concurrent until the existing
short tenant-revision commit fence.

## Ownership

- Retention owns execution attempts, schedule state, and its rebuildable
  organization/property projections.
- GMA Task Runtime owns generic runs, retries, leases, and scheduler execution.
  Retention uses the run id as its execution coordinate without redefining that
  lifecycle.
- GMA Framework owns the provider-neutral shared/exclusive transaction-key-lock
  primitive and command/inbox transaction boundaries. Retention resource names,
  schedule coordinates, and lock order remain BunkFy product policy.
- Owner modules continue to execute their own retention policy through
  Contracts. They do not acquire Retention locks or write Retention state.

## Invariants

1. Lock order is target, execution, then schedule. Property-target reads acquire
   the tenant target before the property target.
2. A scheduled start takes shared locks over the target projections it reads,
   then exclusive execution and schedule locks, before rechecking target
   eligibility and loading mutable state.
3. Completion takes the execution lock before loading the execution, derives
   its immutable schedule coordinate from that authoritative aggregate, then
   takes the schedule lock before loading schedule state.
4. Organization and property projection consumers take their exclusive target
   coordinate before loading or creating the projection. Topology and policy
   streams for one property therefore merge from the committed winner.
5. Command-supplied execution, schedule, target, and tenant coordinates fail
   closed when empty, malformed, cross-tenant, or unsupported.
6. Relational locks require the command or inbox transaction already supplied
   by GMA. Non-relational tests may use an explicit no-op provider adapter.
7. Tenant termination keeps its existing lifecycle/exclusive tenant fence and
   does not acquire every online coordinate.
8. The tenant revision remains the export high-water mark. Its exclusive
   advance may serialize the final short save/commit, but domain work and
   unrelated coordinate acquisition remain concurrent before that boundary.

## Efficiency

- Execution contention is scoped to one Task Runtime run id.
- Schedule contention is scoped to one owner, data class, target, and policy
  version; unrelated schedules do not block each other's decision work.
- Shared target reads permit concurrent scheduled starts while projection
  changes wait only for starts already admitted on that target.
- Transaction-key locks add no rows, migration, export, retention, or
  tenant-destruction surface.
- The existing tenant-revision row remains intentionally serialized because it
  provides a coherent export fence. Replacing it requires a separate revision
  model, not a Retention-local lock shortcut.

## Delivery

1. Add a Retention mutation-lock port and EF transaction-key adapter for target,
   execution, and schedule coordinates.
2. Add execution and scope mutation coordinators with explicit lock-before-read
   behavior and deterministic ordering.
3. Route both execution command handlers and all scope projection consumers
   through those coordinators.
4. Add writer-inventory, coordinate-validation, lock-order, authoritative-reload,
   and real PostgreSQL contention coverage.
5. Run focused checks while editing, then one coherent non-Docker gate and only
   the targeted Docker scenario at slice completion.

## Deferred

- Retention execution-history cleanup until evidence periods and hold policy
  are approved.
- Generic Task Runtime schedule-provider paging and bounded active-schedule
  memory; no Retention owner or data-class key belongs in that GMA design.
- Replacing the tenant-wide export revision with a partitioned high-water-mark
  protocol without measured commit contention.
- Moving BunkFy schedule coordinates or Retention projection policy into GMA.

## Verification

- Unit tests prove target/execution/schedule lock order, lock-before-reload,
  projection merge ordering, invalid-coordinate rejection, and complete writer
  inventory.
- One PostgreSQL scenario proves same-schedule and same-property writers wait,
  reload the committed winner, and preserve independent state while unrelated
  coordinates remain available.
- The final slice gate verifies Retention, architecture, composition, migration
  drift, and all non-Docker backend projects once.

## Result

- Scheduled starts now acquire shared target fences followed by exclusive
  execution and schedule coordinates before authoritative eligibility and
  aggregate reads. Completion derives its schedule coordinate only after the
  execution lock and reload.
- Organization and property inbox consumers now serialize projection merges by
  target. Independent topology and policy versions continue from the committed
  winner without relying on an EF concurrency exception as normal flow.
- The implementation uses GMA's existing transaction-key primitive and adds no
  table, migration, personal-data binding, export record, retention class, or
  tenant-destruction stage.
- The Retention suite passed 42 tests. The targeted PostgreSQL scenario proved
  same-schedule and same-property waiting/reload behavior plus unrelated
  coordinate progress.
- `eng/verify.ps1 -SkipRestore` passed in 404 seconds with synchronized source
  graphs, source-package checks, zero-warning build, all migration-drift checks,
  92 architecture tests, 54 non-Docker integration tests, and every composed
  module/framework suite.

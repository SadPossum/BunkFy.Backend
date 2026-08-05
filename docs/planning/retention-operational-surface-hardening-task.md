# Retention Operational Surface Hardening Task

Status: implemented
Date: 2026-08-04

## Goal

Make Retention's schedule-health and retry surfaces bounded, cache-safe, and
operationally usable without moving owner-record policy into Retention or
product scheduling rules into GMA.

## Ownership

- Retention owns contributor coordinates, active-scope projections, current
  schedule health, execution evidence, and the BunkFy operator facade.
- Owner modules own candidate selection, legal holds, mutation, and exact
  idempotency for their records.
- GMA Task Runtime owns generic runs, retries, leases, and scheduler execution.
  Retention may expose the corresponding run id but does not redefine that
  lifecycle.

## Findings

- The tenant health endpoint derives every active contributor/target
  coordinate and returns the complete result. Public API, Admin API, Admin CLI,
  and web therefore have no truthful continuation boundary.
- The persisted schedule state already records `LastExecutionId`, which is the
  Task Runtime run id, but the health contract omits it while Retention's retry
  operation requires callers to supply that id.
- Health computation reads the clock once per item and validates duplicate
  contributor coordinates only in the scheduler path. A large response can
  therefore cross a due boundary internally, and a broken registration can
  render duplicate management rows before the scheduler reports it.
- Health persistence reads are already tenant-filtered, no-tracking scalar
  projections. The derived summary must still inspect active coordinates, but
  the served page and client payload can be bounded without exact database
  counts or aggregate hydration.
- Retention public and Admin routes do not apply the established no-store
  policy. Admin routes also omit explicit success metadata and bounded error
  mappings.
- The Admin CLI cannot select later health pages and does not expose the run
  coordinate needed by its retry command.

## Decisions

- Add normalized `page` and `pageSize` inputs, stable ordering, `pageSize + 1`
  lookahead, and truthful `HasMore` to the health response.
- Return a whole-snapshot health summary with each page so workspace metrics do
  not silently become page-local. This is computed from the already-derived
  coordinate set and does not issue an exact-count query.
- Expose nullable `LastRunId` from the existing schedule-state projection.
- Share contributor ordering/uniqueness validation between scheduling and
  health reads, and capture one clock value per health snapshot.
- Apply no-store consistently, publish explicit API/Admin success contracts,
  and map retry ownership/state errors deliberately.
- Add Admin CLI paging and print the run id; return a bounded retry receipt from
  both Admin surfaces.
- Paginate the workspace retention view while preserving whole-snapshot health
  metrics and efficient running-state refresh.
- Make no schema or GMA implementation change in this slice.

## Delivery

- [x] Harden the health contract, handler, descriptor catalogue, and scalar
  state projection.
- [x] Align public API, Admin API, Admin CLI, and retry receipts.
- [x] Align generated contracts and the workspace retention view.
- [x] Add focused pagination, summary, run-coordinate, metadata, no-store, and
  web coverage.
- [x] Update the Retention development note and run the coherent slice gates
  once.

## Deferred

- A generic GMA Task Runtime scalability task for streaming or paged schedule
  providers, bounded active-schedule memory, and efficient batch admission.
  The current `IReadOnlyList` provider contract and process-local occurrence
  dictionary are generic concerns; no Retention owner/data-class key belongs in
  that design.
- Retention-owned cleanup of old terminal `RetentionExecution` evidence. The
  deletion mechanism and latest-state protection can be implemented separately,
  but enabling it requires an approved evidence period and hold policy.
- Cursor pagination, server-side health filters, and alert routing until
  measured volume or operations workflow requires them.

## Completion Criteria

- health pages are bounded, stably ordered, and report both truthful
  continuation and whole-snapshot metrics;
- operators can move directly from a failed health row to the exact retry run;
- public and Admin responses are non-cacheable and expose explicit contracts;
- duplicate contributor coordinates fail consistently in scheduler and health
  paths;
- existing scheduling, owner execution, retry, tenant isolation, termination,
  and data-governance behavior remains unchanged;
- focused Retention, architecture, composition, generated-contract, and web
  checks pass.

## Completion Evidence

Implemented on 2026-08-04. Health pages now use normalized bounds, stable
ordering, one-record lookahead, truthful `HasMore`, and a whole-snapshot
summary. The scalar schedule-state read exposes `LastRunId`; shared contributor
validation fails duplicate coordinates in both health and scheduling paths;
public/Admin responses are non-cacheable; and Admin retry returns a typed
receipt. The CLI and workspace view use the same bounded paging contract.

The coherent backend gate passed with a zero-warning build, source-package and
solution guards, all migration-drift checks, Retention 34/34, Architecture
83/83, Operations Notifications 95/95, Workspaces 290/290, and non-Docker
Integration 54/54. Web lint, 142/142 tests, typecheck, production build, and
generated-contract freshness also passed. Docker was intentionally skipped
because this slice changes no schema, provider-specific behavior, or broker
contract.

## Verification Cadence

Use focused Retention and web checks while editing. At the coherent slice
boundary, run the full non-Docker backend gate and web
lint/tests/typecheck/build/contracts check once. No Docker scenario is required
because this slice changes no schema, provider-specific behavior, or broker
contract.

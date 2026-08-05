# Reservations Operational Surface Hardening Task

Status: complete
Date: 2026-08-04

## Goal

Bound ordinary Reservations reads and management write responses so each
permission returns only the booking data required by its use case, with
truthful pagination for reservation lists and details history.

## Ownership

- GMA Pagination owns normalized page and page-size limits.
- BunkFy Reservations owns booking visibility, lifecycle semantics, response
  shapes, deterministic ordering, and personal-data classification.
- Inventory remains authoritative for allocation and no-overbooking decisions;
  this slice changes only Reservations-owned read and mutation surfaces.
- API, Admin API, Admin CLI, and web consumers must preserve the same bounded
  application contracts.

## Invariants

- Reservation list pages project only the fields required by the operational
  list and never hydrate booking aggregates or child collections.
- List and history pagination derive `HasMore` from one bounded look-ahead row;
  neither surface runs an unbounded count query.
- Details history is newest-first, bounded, and guarded by an existence-only
  ordinary-visibility check.
- Full booking detail remains available only through the explicit
  `reservations.read` detail query.
- Create, manage, Guest-link, cancel, check-in, no-show, and check-out commands
  return a minimal mutation receipt. An action permission cannot disclose
  contact details, notes, source references, or staff actor history.
- Personal-data reads prevent shared HTTP caching.
- Existing tenant, property, country-policy, lifecycle, concurrency,
  processing-restriction, and Inventory authority checks remain fail-closed.
- New response members are present in the executable personal-data catalogue.

## Delivery

1. [Completed] Introduce minimized list and mutation-receipt contracts.
2. [Completed] Add direct list projection and one-row look-ahead pagination.
3. [Completed] Add bounded newest-first details-history paging and existence-only validation.
4. [Completed] Align API, Admin API, Admin CLI, web, and generated contracts.
5. [Completed] Update the Reservations personal-data catalogue and deterministic inventory.
6. [Completed] Run focused checks, then one coherent non-Docker domain gate.

## Outcome

- The operational directory now projects summary rows directly and reports
  `HasMore` from one look-ahead row without loading aggregate collections or
  running a count query.
- Details history is newest-first, bounded, and preceded by an existence-only
  ordinary-visibility check.
- Management commands return only reservation identity, state, details
  revision, and aggregate version. Full booking detail remains an explicit
  read.
- Public and administrative personal-data reads set `Cache-Control: no-store`,
  `Pragma: no-cache`, and `Expires: 0`.
- The Admin CLI, web application, OpenAPI snapshot, generated TypeScript, and
  executable personal-data catalogue use the same contracts.

## Verification

- Reservations tests: 175 passed.
- Architecture tests: 83 passed.
- Integration host-composition build: succeeded with zero warnings and errors.
- Web lint and production build: passed.
- Web tests: 138 passed.
- Generated-contract no-build drift check: passed.
- Docker was not run because the slice changes no persistence schema, database
  provider behavior, or broker contract.

## Deferred

- Reservation search keeps its current portable substring semantics. A
  PostgreSQL-specific search strategy requires measured query evidence.
- A separate permission for audit history would be product policy and is not
  introduced by this response-hardening slice.
- Rates, folios, payments, room moves, temporary holds, and business-day close
  policy remain later domain slices.

## Verification Cadence

Use focused Reservations tests and web type checks while editing. At the
coherent slice boundary, run the full Reservations suite, architecture guards,
host-composition build, web lint/tests/typecheck/build, and generated-contract
drift check once. No Docker scenario is required because this slice changes no
persistence schema or broker behavior.

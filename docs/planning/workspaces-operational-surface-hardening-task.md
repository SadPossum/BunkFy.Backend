# Workspaces Operational Surface Hardening Task

Status: implemented
Date: 2026-08-04

## Goal

Make Workspaces-owned onboarding and access-recovery operator surfaces
truthfully bounded, cache-safe, and inexpensive without moving Organizations,
Auth, Access Control, Properties, or Staff ownership into Workspaces.

## Ownership

- Workspaces owns staff-onboarding applications, access plans, and recoverable
  staff-access lifecycle processes.
- GMA Organizations owns workspaces, memberships, invitations, enrollment
  links, and join claims. Workspaces may compose those contracts but does not
  redefine their storage or paging semantics.
- GMA Access Control owns access profiles and assignments. The existing
  Workspaces product facade remains responsible for BunkFy policy and UX.
- Staff owns durable staff profiles and employment state.

## Findings

- The actionable onboarding and open staff-access process directories are
  bounded and stably ordered, but their responses omit continuation metadata.
  A full terminal page therefore looks indistinguishable from a page with more
  data.
- The workspace settings UI guesses continuation from `itemCount == pageSize`.
  The Admin CLI always requests page 1 with 100 rows and cannot inspect later
  recovery processes.
- Open access-process listing materializes aggregate-owned profile snapshots
  only to report their count. A scalar projection can preserve the contract
  without loading those owned rows as domain state.
- Public Workspaces endpoints already mark sensitive responses no-store.
  Admin staff-access endpoints do so manually, while access-bootstrap endpoints
  omit the policy and do not publish explicit success response metadata.
- Access-profile lists already expose truthful `HasMore`. Mutation responses
  are bounded decision-oriented models used for access editing, one-time join
  source delivery, or lifecycle recovery; replacing them with generic receipts
  would remove useful immediate state.
- Join-source and workspace-membership directories depend on GMA Organizations
  list contracts, which currently lack continuation metadata. Workspaces cannot
  derive a truthful `HasMore` value from an exactly full terminal page.

## Decisions

- Add `HasMore` to Workspaces-owned onboarding and access-process list
  responses and implement deterministic `pageSize + 1` lookahead without exact
  count queries.
- Project open access-process rows directly, including a database-side profile
  count, rather than materializing aggregate snapshots for list rendering.
- Pass continuation explicitly to the workspace join-request UI.
- Add `--page` and `--page-size` to the Admin CLI staff-access list command.
- Apply the no-store policy at both Workspaces Admin route groups and declare
  explicit bootstrap and staff-access success response contracts.
- Keep existing detail and mutation DTOs. Their bounded immediate state is part
  of the workflow rather than an accidental aggregate echo.
- Make no GMA change in this slice. Organizations continuation needs its own
  generic module task and coordinated consumer alignment.

## Delivery

- [x] Add truthful continuation to Workspaces-owned operational directories.
- [x] Replace access-process aggregate materialization with a bounded scalar
  projection and focused repository coverage.
- [x] Align public API, Admin API, Admin CLI, generated contracts, and web
  consumers.
- [x] Apply Admin no-store policy consistently and publish explicit endpoint
  response metadata.
- [x] Update Workspaces development notes and run the coherent slice gates once.

## Deferred

- GMA Organizations continuation for organization memberships, invitations,
  enrollment links, and join requests. This is generic framework-module work,
  not a Workspaces-owned persistence change.
- Cursor pagination until measured offset depth warrants a contract change.
- Server-side search and virtualization for unusually large onboarding or
  recovery queues.
- Historical completed access-process browsing. The current surface is an
  operational recovery queue, not an audit-history endpoint.

## Completion Criteria

- Workspaces-owned pages report continuation accurately with no exact count or
  redundant terminal probe;
- open access-process listing does not load aggregate snapshot collections;
- Admin CLI can inspect every page deliberately;
- all Workspaces Admin responses are no-store and expose correct success
  metadata;
- existing onboarding admission, source issuance, access provisioning, data
  rights, retention, and tenant-termination behavior remains unchanged;
- focused Workspaces, architecture, composition, generated-contract, and web
  checks pass.

## Verification Cadence

Use focused contract, repository, API, CLI, and web checks while editing. At
the coherent slice boundary, run the full non-Docker backend gate and web
lint/tests/typecheck/build/contracts check once. No Docker scenario is required
unless implementation changes persistence schema, provider-specific behavior,
or a broker contract.

## Completion Note

Implemented on 2026-08-04. Workspaces-owned onboarding and access-process
queues now expose truthful `HasMore` values using stable `pageSize + 1`
lookahead. Access-process rows are scalar-projected with a database-side
profile count, the workspace settings UI consumes explicit continuation, the
Admin CLI can select any page, and both Workspaces Admin route groups apply the
shared no-store filter with explicit success metadata.

Verification passed with a synchronized backend solution, source-package
checks, a zero-warning full build, and clean migration drift for every
configured provider. The non-Docker suites included Workspaces 290/290,
Operations Notifications 95/95, Workspaces extensions 83/83, Architecture
83/83, and Integration 54/54. Web verification passed lint, typecheck, 142/142
tests across 23 files, the Vite production build, and OpenAPI/generated-contract
drift. Docker was intentionally not rerun because this slice changed no schema,
provider contract, or broker behavior.

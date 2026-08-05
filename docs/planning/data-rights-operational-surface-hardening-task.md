# Data Rights Operational Surface Hardening Task

Status: implemented
Date: 2026-08-04

## Goal

Make the controller-facing Data Rights queue bounded, cache-safe, and cheap to
read without weakening owner-module authority or reopening completed export,
restriction, correction, anonymisation, restore, or tenant-termination work.

## Ownership

- Data Rights owns case lifecycle, selected opaque coordinates, decisions,
  orchestration evidence, and the controller-facing queue.
- Owner modules continue to own discovery indexes, personal records, mutation,
  holds, retention eligibility, and operation receipts.
- GMA supplies generic pagination, CQRS, administration, tasks, and security
  primitives. No BunkFy rights vocabulary or case policy moves into GMA.

## Findings

- Case-list reads normalize page bounds and use stable ordering, but fetch only
  `pageSize` rows and expose no continuation fact. The web currently guesses
  `HasMore` from a full page and can offer an empty next page.
- The queue materializes full `DataRightsCase` aggregates and maps the detail
  DTO, including selected-subject collections and immutable approval evidence.
  The queue needs only a small case summary; detail reads are the proper place
  for full evidence.
- Discovery, execution, correction, restriction, and export responses are
  explicitly non-cacheable, while ordinary case list/detail/lifecycle
  responses are not. The complete Data Rights public surface should fail safe
  against intermediary or browser storage.
- Exact subject discovery is intentionally capped at 20 candidates, but a
  response that reaches that cap does not tell the operator that the bounded
  result may be incomplete.
- `DueAtUtc` is persisted and exposed but never assigned. Response deadlines
  were part of the original domain boundary, yet the current country-policy
  contract does not define their calculation. Production must not invent one
  fixed legal period in this operational hardening slice.
- Tenant-termination Admin status is already non-cacheable and bounded by the
  frozen-owner/phase invariant. Its lifecycle and production admission remain
  governed by the existing tenant-termination tasks.

## Decisions

- Introduce a queue-specific `DataRightsCaseSummaryDto` and return it from the
  list contract while preserving the full `DataRightsCaseDto` for detail and
  mutation responses.
- Use a no-tracking scalar projection, stable ordering, `pageSize + 1`
  lookahead, and truthful `HasMore`; do not issue an exact-count query.
- Apply one no-store endpoint filter to both property- and tenant-scoped public
  route groups so future endpoints inherit the policy.
- Add a conservative `LimitReached` signal when exact discovery fills the
  configured response bound, and explain it in the operator UI without adding
  broad or fuzzy lookup.
- Align generated web contracts, queue pagination, and focused tests.
- Make no schema, broker, Docker, GMA, or owner-module implementation change in
  this slice.

## Delivery

- [x] Add the summary/continuation contracts and scalar repository projection.
- [x] Apply public no-store policy and retain explicit success contracts.
- [x] Add the bounded-discovery limit signal.
- [x] Align the Privacy Requests queue and discovery UI.
- [x] Add focused persistence, contract, API-policy, and web coverage.
- [x] Update the Data Rights development note and run one coherent slice gate.

## Deferred

- Define response-deadline policy coordinates, jurisdiction-aware period
  calculation, deadline-change semantics, escalation, and overdue operator
  behavior in a dedicated Data Rights business-policy slice. That work may
  require a versioned country-policy contract and Properties projection event;
  it does not belong in GMA.
- Add server-side queue ordering/filtering by due state only with the deadline
  policy above or measured operator demand.
- Replace exact discovery with owner-aware continuation only if operators need
  to browse rather than refine a strong identifier. `LimitReached` preserves a
  safe bounded contract meanwhile.
- Add or change database indexes only with query-plan/volume evidence; the
  current slice changes projection shape but not filter/order semantics.

## Completion Criteria

- queue pages are bounded, stably ordered, truthful about continuation, and do
  not hydrate full case aggregates;
- detail and mutation responses retain the complete case contract;
- all public Data Rights responses are non-cacheable;
- a discovery result at the configured bound is visibly identified;
- property and tenant scope, permissions, assurance, concurrency, owner
  revalidation, and completed privacy workflows remain unchanged;
- focused Data Rights, architecture, generated-contract, and web checks pass.

## Verification Cadence

Use focused Data Rights and web checks while editing. At the coherent slice
boundary, run the full non-Docker backend gate and web
lint/tests/typecheck/build/contracts check once. No Docker scenario is required
because this slice changes no schema, provider-specific behavior, or broker
contract.

## Completion Evidence

- `BunkFy.Host.Api` builds with zero warnings and zero errors.
- The repository-wide non-Docker backend gate passes, including all migration
  drift checks, Data Rights 445/445, Operations Notifications 95/95,
  Architecture 83/83, and Integration 54/54.
- Focused Data Rights web/foundation coverage passes 28/28.
- The full web gate passes lint, 23 test files and 142 tests, TypeScript
  checking, the production Vite build, and generated-contract drift checking.
- `git diff --check` passes in both backend and web repositories; the backend
  reports only pre-existing line-ending notices.
- No Docker scenario was run because the implemented slice changes no schema,
  provider-specific behavior, or broker contract.

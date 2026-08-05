# Guests Read Surface Hardening Task

Status: complete
Date: 2026-08-04

## Goal

Bound and minimize ordinary Guests reads so property staff receive only the
personal data required by each screen, with truthful pagination for both the
guest directory and stay history.

## Ownership

- GMA Pagination owns normalized page and page-size limits.
- BunkFy Guests owns property visibility, operational response shapes,
  deterministic ordering, and personal-data classification for guest reads.
- API, Admin API, Admin CLI, and web consumers must expose the same bounded
  application contracts.

## Invariants

- Directory reads never return notes, date of birth, creation attribution, or
  other detail-only fields.
- Persistence projects directory rows directly to the summary contract rather
  than materializing full aggregates.
- `HasMore` is derived from one bounded look-ahead row; no unbounded count is
  required.
- Stay history is independently paged and deterministically ordered.
- Existing property visibility and processing-restriction gates remain
  fail-closed.
- New response members are present in the executable personal-data catalogue.

## Delivery

1. [Completed] Introduce a minimized guest-list item and truthful list envelope.
2. [Completed] Page the stay-history query, repository, API, Admin API, and Admin CLI.
3. [Completed] Align web directory, picker, detail pagination, and generated contracts.
4. [Completed] Update the Guests personal-data catalogue and deterministic inventory.
5. [Completed] Run focused checks, then one coherent non-Docker domain gate.

## Outcome

- Public and administrative endpoints publish explicit success-body schemas,
  so generated web contracts cover the minimized directory and paged stay
  history rather than silently degrading to body-less responses.
- The public API composes the exact BunkFy tenant-termination owner catalogue
  required by Data Rights, while its intentionally absent Task Runtime is
  represented by an explicit fail-closed scheduler. Admin and Worker profiles
  continue to own durable execution.
- The OpenAPI exporter now starts the host on Windows and Unix-like systems.

## Verification Evidence

- Guests module tests: 136 passed.
- Data Rights module tests: 442 passed.
- Architecture tests: 83 passed.
- Integration and complete host-composition build: succeeded with zero
  warnings and errors.
- Web lint, 138 tests, typecheck, production build, and generated-contract
  drift check: passed.
- No Docker scenario was run because this slice changes no persistence schema
  or broker behavior.

## Deferred

- Search remains bounded by page size but still uses substring matching. A
  provider-specific PostgreSQL search strategy needs measured query evidence
  before changing user-visible semantics or adding database extensions.
- Cursor pagination is unnecessary for the current staff-facing directory. It
  can replace offset paging later without moving Guests policy into GMA.

## Verification Cadence

Use focused Guests and web tests while editing. At the coherent slice boundary,
run the full Guests suite, architecture guards, host-composition build, web
typecheck/lint/tests, and production build once. No Docker scenario is required
unless the persistence schema or broker behavior changes.

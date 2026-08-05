# Staff Operational Surface Hardening Task

Status: complete
Date: 2026-08-04

## Goal

Bound ordinary Staff reads and management write responses so callers receive
only the operational data required by each use case, with truthful pagination
for tenant and property directories.

## Ownership

- GMA Pagination owns normalized page and page-size limits.
- BunkFy Staff owns staff visibility, assignment semantics, response shapes,
  deterministic ordering, and personal-data classification.
- Tenant and property directories are distinct use cases and therefore expose
  distinct contracts.
- API, Admin API, Admin CLI, and web consumers must preserve the same bounded
  application contracts.

## Invariants

- Tenant directory pages never materialize assignment collections; they expose
  only the current-property count needed by the list screen.
- Property directory pages expose exactly the current assignment for the
  requested active property, not every current or historical assignment.
- Full profile and assignment history remain available only through the
  sensitive detail read.
- Management writes return a directory-safe representation rather than the
  sensitive profile they mutate.
- `HasMore` is derived from one bounded look-ahead row; no unbounded count is
  required.
- Existing tenant, property, lifecycle, and processing-restriction gates remain
  fail-closed.
- New response members are present in the executable personal-data catalogue.

## Delivery

1. [Completed] Introduce minimized tenant and property directory contracts.
2. [Completed] Add truthful one-row look-ahead pagination in persistence.
3. [Completed] Minimize create, update, and account-link management receipts.
4. [Completed] Align API, Admin API, Admin CLI, web, and generated contracts.
5. [Completed] Update the Staff personal-data catalogue and deterministic inventory.
6. [Completed] Run focused checks, then one coherent non-Docker domain gate.

## Outcome

- Tenant directory rows project a current-property count without loading
  assignment collections; property rows project exactly one current assignment
  for the requested active property.
- Both directory envelopes expose truthful `HasMore` from a bounded look-ahead
  row, and the web list uses the shared pagination control with empty-page
  recovery.
- Create, update, and account-link commands return directory-safe receipts.
  Sensitive detail reads remain explicit and retain no-store response headers.
- Public and administrative endpoints publish explicit success schemas, and
  the Admin CLI prints the bounded list/receipt shapes appropriate to each
  permission.
- Staff personal-data catalogue v11 and its deterministic inventory classify
  every new operational response member.

## Verification Evidence

- Staff module tests: 168 passed.
- Architecture tests: 83 passed.
- Integration and complete API/Admin API/CLI/Worker composition build:
  succeeded with zero warnings and errors.
- Web lint, 138 tests, typecheck, and production build: passed.
- OpenAPI export, generated TypeScript contracts, and drift check: passed.
- No Docker scenario was run because this slice changes no persistence schema
  or broker behavior.

## Deferred

- Full profile history remains aggregate-backed and can grow with assignment
  churn. Split history behind a separately paged read only after measured load
  shows that operational aggregate hydration is material.
- Directory search remains bounded by page size but uses substring matching. A
  PostgreSQL-specific search strategy requires measured query evidence before
  changing semantics or adding extensions.
- Retiring a property does not rewrite Staff-owned assignment history. A future
  durable coordination slice should define whether current assignments close
  automatically or remain visible as historical employment evidence.
- A hard cap on concurrently current assignments is a product policy, not a
  generic framework concern, and remains deferred until workspace policy owns
  that decision.

## Verification Cadence

Use focused Staff tests and web type checks while editing. At the coherent
slice boundary, run the full Staff suite, architecture guards, host-composition
build, web lint/tests/typecheck/build, and generated-contract drift check once.
No Docker scenario is required because this slice changes no persistence schema
or broker behavior.

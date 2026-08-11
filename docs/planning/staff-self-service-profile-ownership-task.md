# Staff Self-Service Profile Ownership Task

Status: completed
Date: 2026-08-11

## Goal

Make the identity-bound Staff profile update contract expose only fields the
current Staff member is allowed to change, while preserving exact retry behavior
when management-owned profile data changes independently.

## Confirmed Gap

`PUT /api/staff/me` reuses the management profile request and accepts
`EmployeeNumber`. The account UI does not expose that field, but it echoes the
current value to satisfy the API. A direct client can therefore replace or clear
management-owned employee identity data without `staff.manage` permission.

Simply omitting the field in the browser is insufficient. If the server inserts
the current employee number into the existing full-profile fingerprint, a replay
of an already committed self-service operation can conflict after a manager later
changes that number.

## Ownership

- Staff API owns the public self-service request allowlist.
- Staff Application owns server-side field preservation, operation equivalence,
  and exact replay behavior.
- The web account and onboarding flows own stable operation ids for the reduced
  request shape.
- GMA requires no change. Field ownership and Staff mutation equivalence are
  product-domain semantics.

## Invariants

- The self-service request and command do not contain `EmployeeNumber`.
- Display name, legal name, work email, work phone, job title, and department
  remain intentionally self-editable for the current BunkFy workflow.
- The handler resolves and locks the identity-bound Staff member before copying
  the current employee number into the mutation profile.
- A new self-service update cannot replace or clear the employee number.
- Self-service request equivalence excludes the management-owned employee number
  and uses a namespace distinct from management profile updates.
- Exact replay returns the original receipt even if management later changes the
  employee number or other current profile state.
- Reusing the operation id for changed self-service input or across the management
  surface remains a conflict.
- Pre-v2 full-profile receipts fail closed instead of being replayed through the
  reduced surface. The journal has no trustworthy marker that distinguishes an
  old self-service receipt from a manager receipt with the same profile, so
  accepting that legacy fingerprint would weaken cross-surface isolation. The
  browser v2 attempt namespace creates a new operation for the reduced shape.
- Restricted, anonymised, relinked, or cross-tenant profiles remain unavailable
  before any receipt is disclosed.

## Delivery

1. Add a dedicated public self-service update request and remove employee number
   from the identity-bound application command.
2. Add a self-service fingerprint and coordinator path that preserves the locked
   member's employee number while retaining the shared mutation journal.
3. Stop browser account and onboarding flows from echoing employee number and
   align their durable-attempt fingerprint with the reduced payload.
4. Update OpenAPI/generated contracts and Staff personal-data catalogue bindings.
5. Prove field-shape, preservation, exact replay after management change, changed
   reuse, subject recheck, and existing management-update behavior.

## Verification Cadence

- Run focused Staff and web attempt/account tests while editing.
- At the slice boundary, run the complete Staff and web suites, architecture
  guards, catalogue/inventory drift, generated-contract drift, solution
  synchronization, and one solution build.
- No migration or Docker matrix is required because persistence shape, provider
  SQL, and broker behavior do not change.

## Outcome

- `PUT /api/staff/me` now accepts a dedicated allowlisted request, and the
  identity-bound command no longer exposes employee number.
- The locked member's current employee number is preserved server-side while a
  self-service-specific fingerprint keeps exact replay independent of later
  manager changes and isolated from management operations.
- Account and onboarding clients use the reduced payload and a v2 durable-attempt
  namespace. OpenAPI, generated TypeScript, and personal-data catalogue version
  18 describe the same boundary.
- No GMA, database schema, migration, or provider-specific behavior changed.

## Evidence

- Focused ownership/replay checks passed: 5 Staff tests.
- Complete Staff suite passed: 258 tests.
- Architecture suite passed: 102 tests.
- Complete web verification passed: typecheck, lint, 258 tests, and production
  build.
- Personal-data catalogue reflection and deterministic inventory checks passed
  within the Staff suite.
- Generated OpenAPI/TypeScript contract drift and solution synchronization
  checks passed.
- `dotnet build BunkFy.slnx --no-restore` completed with 0 warnings and 0 errors.
- Docker and migration checks were intentionally omitted because this slice does
  not change persistence shape or infrastructure behavior.

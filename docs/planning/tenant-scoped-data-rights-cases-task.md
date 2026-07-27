# Tenant-Scoped Data Rights Cases Task

Status: published; local and exact-commit production proof complete

## Goal

Extend the BunkFy Data Rights coordinator so it can admit and manage a
tenant-scoped staff-subject case without pretending that a tenant-wide Staff
profile belongs to one property.

This is an enabling Data Rights slice. It must preserve every existing
property-scoped guest workflow and create the narrow contract needed by the
next Staff owner slice. It does not implement Staff mutation.

## Problem

The current owner coordinates are generic, but case admission and routing are
not:

- the only public subject-rights case kind is `GuestRights`;
- every public route is nested under a property;
- discovery, selection, and export contributor requests require a non-empty
  property id; and
- irreversible execution batches, work items, approval evidence, and policy
  gates are property-routed.

Staff is tenant-owned. One profile may have current or historical assignments
at several properties and countries. Choosing a "home property" would produce
an incomplete subject view and an unsafe erasure boundary. `Guid.Empty` is not
an acceptable tenant sentinel.

## Ownership Boundary

Data Rights owns:

- case kind and tenant/property case routing;
- requester verification, review, decision, and selection lifecycle;
- contributor admission by case kind;
- tenant-level API authorization for tenant-scoped cases; and
- compatibility and migration of its own case records.

Staff will own, in the following slice:

- staff-subject discovery and selection validation;
- Staff-owned export fragments;
- employment lifecycle eligibility;
- correction, restriction, anonymisation, tombstones, and restore proof; and
- policy decisions that depend on assignments, employment history, or Staff
  retention.

GMA continues to own generic tenant context, permissions, scopes, CQRS,
messaging, tasks, clocks, identity, and persistence primitives. BunkFy case
types, subject-owner routing, and hospitality or employment policy must not
move into GMA.

## Case Model

Add `StaffRights = 3` to the public and domain case-kind enums. Preserve the
existing numeric values for `GuestRights = 1` and `TenantTermination = 2`.

Case scope is represented without a sentinel:

- `GuestRights` requires a non-empty property id;
- `StaffRights` requires a null property id and is tenant-scoped; and
- `TenantTermination` remains tenant-scoped with a null property id.

The nullable `PropertyId` already persisted by `DataRightsCase` remains the
storage representation. A small validated domain/application scope value
object should make tenant versus property intent explicit in command and
repository code. A redundant scope column is not added unless implementation
proves the existing invariant cannot be enforced safely.

The PostgreSQL case-kind/property check constraint must include `StaffRights`
as tenant-scoped. Existing rows and enum values are unchanged.

## Initial Staff Rights Capability

This enabling slice admits `StaffRights` cases for `AccessExport` only.

Correction, restriction, erasure, and anonymisation are rejected for
`StaffRights` until Staff owns the corresponding execution policy and proof.
This is a fail-closed product capability boundary, not a legal conclusion.

Tenant-scoped anonymisation execution batches are out of scope here. Existing
property-scoped Guest anonymisation execution remains unchanged.

## Contributor Routing

Every discovery and export contributor must declare the case kinds it
supports. Requests also carry the normalized case kind and nullable property
scope.

The coordinator must:

- call only contributors that support the case kind;
- reject an explicit owner key that does not support the case kind;
- preserve deterministic owner ordering and duplicate-owner rejection;
- pass `GuestRights` plus a property to existing Guests, Reservations,
  Ingestion, and Inventory contributors; and
- never expose a Staff contributor from a Guest case or a guest-data owner
  from a Staff case.

Existing owner modules reference Data Rights Contracts only. Data Rights never
reads an owner schema.

## Subject Lookup

Extend the bounded lookup contract with an optional account subject id. This
is an explicit account-correlation lookup, not a generic string escape hatch.

The shared lookup keeps:

- exact owner record id;
- account subject id;
- email;
- phone;
- name; and
- date of birth.

Each contributor accepts only criteria meaningful for its case kind and owner.
The following Staff slice should prefer exact Staff member id or exact account
subject id. Broad or fuzzy tenant-wide identity search is not introduced by
this task.

Account subject ids are sensitive request data. They must remain bounded and
must not enter logs, metrics, traces, events, notifications, task payloads, or
support bundles.

## API And Authorization

Keep the current property routes backward compatible:

`/api/data-rights/properties/{propertyId}/cases`

Those routes continue to create and manage `GuestRights` cases and use the
property access-scope resolver.

Add a tenant route group:

`/api/data-rights/tenant/cases`

The tenant group manages `StaffRights` cases and uses tenant-level permission
metadata directly. A property-scoped grant must not satisfy any tenant case
operation. The initial group supports the same non-execution lifecycle needed
for case creation, requester verification, discovery, selection, review,
decision, cancellation, and reads.

Sensitive discovery and selection responses remain `Cache-Control: no-store`.
Routes must not place subject coordinates or lookup criteria in URLs.

## Application And Persistence

Commands, queries, validation, and case repositories use the validated case
scope instead of requiring a `Guid` property id everywhere.

Repository queries must include:

- tenant scope;
- case id; and
- exact property-nullness or property id from the validated case scope.

A tenant query must not accidentally match every property case. A property
query must not match a tenant case.

Case list ordering and pagination remain deterministic and bounded. Tenant and
property cases are never combined in one unbounded read.

The migration updates the four existing kind, operation, property-scope, and
requester-scope constraints. It adds no column and rewrites no case data.
Migration coverage starts from the previous published schema and proves
existing Guest and Tenant Termination cases are unchanged.

## Security Invariants

- `StaffRights` is tenant-scoped and cannot be created or operated with only a
  property grant.
- Guest contributors cannot run in a Staff case, and Staff contributors cannot
  run in a Guest case.
- Tenant scope is represented by null property plus validated case kind, never
  by `Guid.Empty`.
- Existing Guest anonymisation approval evidence remains property-bound.
- Tenant cases cannot start the current property-bound anonymisation execution.
- Lookup criteria and selected previews stay out of durable task and event
  payloads.
- Contributor output limits and selection limits remain enforced.
- Unknown case kinds, invalid operation combinations, and mismatched route
  scopes fail closed.

## Verification

1. Domain tests cover all case-kind, scope, requester, and operation
   combinations.
2. Application tests prove tenant/property repository isolation and contributor
   filtering by case kind.
3. API authorization tests prove tenant grants succeed, property-only grants
   fail, and existing property routes are unchanged.
4. Existing Guests, Reservations, Ingestion, and Inventory contributor tests
   prove `GuestRights` and property scope are required.
5. PostgreSQL migration tests preserve prior cases and enforce the updated
   constraint.
6. OpenAPI and frontend contracts remain compatible for property cases.
7. Personal-data catalogues classify the account-subject lookup and every new
   boundary member.
8. Architecture tests preserve contracts-only module dependencies and prove
   no GMA source change.
9. Complete non-Docker, migration, Docker, package, preview, and exact-commit
   repository gates pass before publication.

## Implementation Result

- `StaffRights = 3` is a tenant-scoped, access-export-only case kind with an
  explicit application scope and exact kind/property repository predicates.
- Tenant case routes use tenant permission metadata directly. The current
  property routes and property-bound anonymisation execution remain unchanged.
- Discovery and export contracts carry case kind plus nullable property scope.
  Contributors declare supported case kinds, and malformed, duplicate, empty,
  unknown, or mismatched capability metadata fails closed.
- Existing Guests, Reservations, Ingestion, and Inventory owners accept only
  property-scoped `GuestRights`; Staff ownership remains deferred to the next
  module slice.
- The bounded lookup supports exact account subject ids, and the personal-data
  catalogue classifies the new transient Staff discovery input.
- PostgreSQL migration
  `20260727021342_SupportTenantScopedStaffRightsCases` updates the four existing
  constraints without a data rewrite.
- The checked-in web OpenAPI snapshot and generated TypeScript contracts
  include the new tenant lifecycle routes and lookup member.
- Local verification passed with a zero-warning solution build, zero migration
  drift, 2,777 non-Docker tests, 61 Docker tests, 65 architecture tests, and
  the web contract check, typecheck, lint, 114 tests, and production build.
  GMA framework, module, and extension source remains unchanged.
- Backend commit `db72d55b85eda173d5f629d55b2e0f3279748700`
  passed Windows and Ubuntu validation in run `30233901198` and Docker
  verification in run `30233901197`. Web contract commit
  `b06157bdd20c9ca20ac86ec1c2fe76243e603d61` passed run `30233920952`.
  Product root commit `4e2f2c9759323a8ea2b3f93fe9cf75200264ca4a`
  passed validation, Security Baseline, and CodeQL in runs `30234447941`,
  `30234447946`, and `30234447906`.

## Deferred

- Staff discovery and export contributor implementation;
- Staff correction and restriction;
- Staff anonymisation, tombstones, restore, and protected ledger deltas;
- multi-country employment-policy evaluation;
- coordinated Auth disablement or AccessControl revocation;
- Workspaces onboarding/access-history owner contribution;
- protected export artifact assembly;
- automatic retention scheduling, legal holds, and backup consequences; and
- legal approval of Staff purposes, periods, jurisdiction rules, and
  exceptions.

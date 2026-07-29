# Staff Data Rights Restriction Task

Status: planned; implementation in progress
Date: 2026-07-29

## Goal

Add tenant-scoped processing restriction for one exact Staff profile. An
approved `StaffRights` case must apply or release an independent Staff-owned
restriction, produce durable replay-safe proof, and make ordinary Staff
processing fail closed without deleting employment facts or silently changing
Auth credentials or AccessControl grants.

This is one Staff slice. Data Rights may change where its existing restriction
or approval contracts are still property-only, but no other data owner gains
new behavior.

## Ownership

Staff owns:

- independent apply-case restrictions for one Staff member;
- the effective reference-counted restriction projection;
- immutable apply/release receipts and exact replay behavior;
- the Staff operational enforcement matrix;
- a PII-free authoritative restriction gate and transition event; and
- exclusion of restricted Staff from optional operational audiences.

Data Rights owns:

- `StaffRights` case admission, requester verification, selection, approval,
  execution, and expiry;
- the approved `Apply` or `Release` directive;
- routing to exactly one owner contributor; and
- central completion after validating Staff owner proof.

GMA continues to own CQRS, tenancy and scope context, persistence, outbox
delivery, projection infrastructure, and runtime primitives. Staff employment
policy and BunkFy data-rights case types are product concepts. No GMA change is
required.

## Scope-Aware Restriction Prerequisite

The current BunkFy restriction path assumes every case has a property. This
slice makes it explicit:

- `GuestRights` remains property scoped;
- `StaffRights` is tenant scoped and rejects a property coordinate;
- restriction commands and approval requests carry case type plus nullable
  property id;
- the contributor contract advances version because its request shape changes;
- existing Guest behavior and proof remain compatible; and
- tenant restriction execution is exposed under
  `/api/data-rights/tenant/cases/{caseId}/restriction`.

The in-process BunkFy contracts are generalized only as far as proven by Guest
and Staff owners. They do not become a GMA workflow abstraction.

## Case Admission

`StaffRights` accepts exactly one operation:

- `AccessExport`;
- `Correction`; or
- `Restriction`.

Restriction requires exactly one directive, `Apply` or `Release`. Combined
operations and erasure/anonymisation remain denied. Valid requester
relationships remain data subject, authorized representative, or controller
initiated. Tenant-owner requests remain denied.

## Owner Model

Each apply approval creates an independent Staff restriction containing:

- tenant and Staff member coordinates;
- apply case id, approval revision, and selected Staff version;
- active or released status and optimistic version;
- apply actor and timestamp; and
- release case id, approval revision, selected Staff version, actor, and
  timestamp when released.

The effective projection is keyed by tenant and Staff member and contains:

- the supported Staff restriction contract version;
- active restriction count;
- effective restricted boolean;
- monotonic projection revision; and
- last transition timestamp.

Multiple approved apply cases compose safely. Releasing one restriction cannot
clear another. A release is executable only when exactly one active restriction
exists for the selected Staff member; ambiguous release requires explicit
operator resolution instead of guessing.

Every transition writes an append-only receipt containing bounded coordinates,
action, case and approval revision, selected Staff version, resulting owner and
projection revisions, effective state, actor, event id, and completion time.
Equivalent retries return the committed receipt. Reusing an idempotency key
with changed inputs fails.

## Enforcement Matrix

Restriction is enforced in database predicates and command boundaries, not by
filtering materialized pages.

| Staff surface | Restricted behavior |
| --- | --- |
| Tenant/property directory and detail | Exclude before paging or return not found |
| Current Staff self-service read | Return not found |
| Ordinary profile or self-service update | Reject |
| Add property assignment | Reject |
| Resume employment | Reject |
| Attach, replace, or remove Auth subject link | Reject |
| Onboarding and identity/assignment reconciliation | Reject for the existing restricted record |
| Operational notification audience | Exclude |
| Suspend employment | Allow as a safety-reducing transition |
| Depart employment | Allow as a safety-reducing transition |
| Remove property assignment | Allow as a safety-reducing transition |
| Data Rights discovery/export/correction | Allow through explicit rights paths |
| Restriction apply/release | Allow only through dedicated owner commands |
| Persistence repair and factual event delivery | Continue without exposing profile PII |

Create and uniqueness checks still consider restricted records so restriction
cannot create duplicate employee numbers or Auth links. The restriction does
not disable login, revoke permissions, or mutate Organizations membership.
Those are separate owner responsibilities and require an explicit coordinated
workflow.

## Dependent-Module Contract

Staff Contracts exposes a versioned, PII-free gate accepting tenant and Staff
member coordinates. It returns:

- allowed;
- restricted;
- unknown; or
- unsupported contract version.

Only `allowed` permits optional processing. Unknown and unsupported results
fail closed.

Staff publishes a versioned transition event with tenant/Staff coordinates,
contract version, projection revision, and effective state. It contains no
case id, actor, reason, contact, name, or employment detail.

## Security And Efficiency

- Ordinary workspace profiles receive no restriction execution permission by
  default.
- Tenant, case type, property absence, owner, record type, record id, record
  version, case, approval revision, directive, actor, and deadline are checked
  independently.
- Apply and release use separate immutable approvals.
- Ordinary reads require an indexed supported, unrestricted projection before
  count, ordering, skip, and take.
- Effective lookups are fixed-width indexed reads; active restriction and
  receipt lookup use bounded indexed coordinates.
- No request scans all Staff, restrictions, cases, or assignments.
- Events, task payloads, logs, metrics, and receipts contain no direct profile
  values.
- Data Rights and Staff commits remain independently replay-safe. A retry after
  Staff commits but before central case completion converges from the receipt.

## Persistence And Deployment

Staff adds restriction, effective projection, and immutable receipt tables plus
their tenant-visible indexes and constraints. The migration backfills one
supported baseline projection with zero active restrictions for every existing
Staff row; new Staff creation writes the member and baseline projection in the
same unit of work. Missing or unsupported projections fail closed, so
initialization and repair cannot create a fail-open window.

Data Rights advances its Staff case operation constraint to admit restriction
while preserving existing Guest and correction rows.

Deploy migrations before enabling Staff restriction case creation. All hosts
must run the same BunkFy restriction contract version.

## Verification

Focused proof must cover:

- Staff case admission for export, correction, and restriction only;
- tenant/property scope mismatch denial;
- apply/release approval binding and owner-proof validation;
- multiple active restrictions and exact release behavior;
- replay, changed-input conflict, stale Staff/projection versions, and deadline;
- directory paging, details, self-service, writes, onboarding, reconciliation,
  safety-reducing transitions, and notification audiences;
- PII-free events and append-only receipts;
- migration/model parity and personal-data catalogue completeness; and
- existing Guest restriction behavior after the scope-aware contract change.

One final PostgreSQL/NATS scenario must prove approved tenant apply, operational
suppression, allowed safety transition, exact release, visibility restoration,
and retry convergence. Docker and exact-candidate CI run only after the slice is
otherwise complete.

## Deferred

- Staff anonymisation, tombstones, protected ledger deltas, and restore;
- employment jurisdiction and country-specific restriction adjudication;
- automatic Staff retention and legal holds;
- coordinated Auth disablement, Organizations membership changes, or
  AccessControl revocation;
- assignment-history correction;
- public Staff self-service case creation; and
- payroll, identity-document, or other future Staff-domain restrictions.

## Completion Criterion

The slice is complete when a tenant-scoped approved `StaffRights` restriction
can apply or release one exact Staff-owned restriction, central Data Rights
stores validated owner proof, ordinary Staff processing fails closed according
to the matrix, Guest restriction behavior remains compatible, and the focused,
single final local, Docker, and exact-candidate gates are green.

# Staff Module

`Staff` owns BunkFy's operator employment profiles and property work assignments. It does not own credentials, sessions, roles, grants, or effective authorization.

## Boundaries

- GMA Auth owns accounts and authentication lifecycle.
- GMA AccessControl owns roles, permission grants, and scope evaluation.
- Staff owns `StaffMemberId`, profile data, employment lifecycle, optional Auth user-subject correlation, and assignment history.
- Properties owns property identity/lifecycle; Staff consumes a local monotonic projection.
- Linking an Auth subject or assigning a property never grants access.

Profiles may be `Active`, `Suspended`, `Departed`, or irreversibly
`Anonymised`. Suspension is explicitly reversible; departure closes current
assignments while retaining their history until Data Rights or automatic
retention performs the terminal scrub. A profile may be unlinked from Auth,
and an Auth user may exist without a Staff profile.

## Permissions

- `staff.read`
- `staff.sensitive-profile.read`
- `staff.create`
- `staff.manage`
- `staff.assign-properties`
- `staff.manage-lifecycle`
- `staff.employment-governance.manage`
- `staff.data-holds.manage`

`staff.read` exposes the operational directory only: display name, job title, department, status, and current assignment facts. Full profile reads require `staff.sensitive-profile.read`; the identity-bound self-service route remains available to the current Staff subject. Canonical profile/create/update/lifecycle routes require tenant scope. Property discovery and assignment routes require `tenant/property` scope. Property grants do not satisfy tenant operations.

## Processing restrictions

Approved tenant-scoped Staff data-rights cases can apply or release processing restrictions. Staff owns the reference-counted effective state and append-only transition receipts; Data Rights owns approval and orchestration. Missing or future projection contracts fail closed.

Restricted staff members are excluded from directory/detail/self-service reads, profile and assignment writes, identity reconciliation, onboarding reconciliation, and operational notification audiences. Data-rights discovery, export, correction, and restriction execution remain available. Suspend, depart, and unassign remain available as safety-reducing transitions; resume and access-link changes remain blocked. Restriction does not mutate Auth credentials or AccessControl grants.

## Data rights and retention

Staff is the tenant-scoped owner contributor for discovery, bounded export,
correction, and processing restriction. Data Rights owns cases, requester
verification, approval, orchestration, and central completion; it does not read
Staff persistence.

The
[Staff anonymisation flow](../../../docs/planning/staff-data-rights-anonymisation-task.md)
uses explicit employment-governance evidence, independently releasable Staff
holds, per-member operation serialization, immutable owner receipts, and an
authority-bound tombstone. Protected restore applies only to Data
Rights-authority tombstones.

Automatic retention uses the shared Retention control plane but keeps all
Staff identifiers and mutation decisions inside Staff. A tenant-scoped,
bounded sweep selects departed profiles by projection ordinal, evaluates the
exact `staff-employment` country-policy rule, observes holds, and revalidates
under the Staff operation lock. Before the irreversible scrub, a versioned
`Staff.Contracts` prerequisite requires Workspaces to prove the completed
departure access process and idempotently re-deny access. Staff then records
its own append-only receipt and a `Retention`-authority tombstone; Retention
receives only counts and stable outcome codes.

The executable delivery contract is
[Staff record automatic retention](../../../docs/planning/staff-record-retention-task.md).

## Runtime

The module is composed in public API, Admin API, Admin CLI, and the optional worker group. PostgreSQL migrations live in `BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations`. The worker consumes Properties lifecycle facts and runs `rebuild-staff-properties` to repair the local projection.

General integration events carry only tenant, staff, lifecycle, Auth-correlation, assignment, effective-date, and version facts. They exclude names, contact details, employee numbers, job labels, departments, and reasons.

[`personal-data-catalog.v1.json`](personal-data-catalog.v1.json) is the executable Staff data contract. [`personal-data-inventory.v1.md`](personal-data-inventory.v1.md) is generated from it and checked by reflection tests against persistence, search copies, public/admin boundaries, cross-module requests, domain events, and integration events.

Broader deferred work is tracked in
[the Staff Profiles task](../../../docs/planning/staff-profiles-module-task.md).

# Staff Module

`Staff` owns BunkFy's operator employment profiles and property work assignments. It does not own credentials, sessions, roles, grants, or effective authorization.

## Boundaries

- GMA Auth owns accounts and authentication lifecycle.
- GMA AccessControl owns roles, permission grants, and scope evaluation.
- Staff owns `StaffMemberId`, profile data, employment lifecycle, optional Auth user-subject correlation, and assignment history.
- Properties owns property identity/lifecycle; Staff consumes a local monotonic projection.
- Linking an Auth subject or assigning a property never grants access.

Profiles may be `Active`, `Suspended`, or `Departed`. Suspension is explicitly reversible; departure is terminal in this slice and closes current assignments while retaining their history. A profile may be unlinked from Auth, and an Auth user may exist without a Staff profile.

## Permissions

- `staff.read`
- `staff.create`
- `staff.manage`
- `staff.assign-properties`
- `staff.manage-lifecycle`

Canonical profile/create/update/lifecycle routes require tenant scope. Property discovery and assignment routes require `tenant/property` scope. Property grants do not satisfy tenant operations.

## Runtime

The module is composed in public API, Admin API, Admin CLI, and the optional worker group. PostgreSQL migrations live in `BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations`. The worker consumes Properties lifecycle facts and runs `rebuild-staff-properties` to repair the local projection.

General integration events carry only tenant, staff, lifecycle, Auth-correlation, assignment, effective-date, and version facts. They exclude names, contact details, employee numbers, job labels, departments, and reasons.

Deferred work is tracked in [the Staff Profiles task](../../../docs/planning/staff-profiles-module-task.md).

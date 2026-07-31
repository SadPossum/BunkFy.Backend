# Operations Notifications Staff Data Rights Task

Status: completed
Date: 2026-07-31

## Goal

Make BunkFy's Operations Notifications extension a complete Staff Rights owner
for durable operational inbox copies addressed to one Staff member. The design
must survive account relinking and concurrent owner execution without exposing
Auth identifiers in Data Rights coordinates, receipts, logs, or GMA contracts.

This is the next slice of
[Operations Notifications Data Rights Owner Capability](operations-notifications-data-rights-owner-capability-task.md).

## Audit Findings

- GMA Notifications automatically indexes each addressed copy by its recipient,
  but that generic reference is derived from the mutable Auth subject.
- BunkFy currently adds only reservation-linked producer references. Property,
  inventory, reservation, and Staff notifications addressed to a Staff member
  therefore have no stable employment-record reference.
- Data Rights owner work items can execute independently. Operations
  Notifications cannot assume that Staff or Workspaces still retains the Auth
  subject when its own anonymisation or restore work runs.
- The current Operations Notifications owner implements the property-scoped
  Guest Rights contracts. Staff Rights use tenant-scoped policy contributions,
  version 2 execution and prerequisites, and version 3 restore contracts.
- Staff is authoritative for the durable Staff profile and its optional Auth
  correlation after provisioning. Workspaces owns onboarding and access-process
  history; Auth owns identities. Neither should be queried as another module's
  persistence implementation.
- GMA's generic reference lifecycle, close receipt, replay suppression, paging,
  and recipient index already provide the required substrate. No GMA change is
  justified by this slice.

## Ownership Boundary

- Staff owns Staff-member identity, lifecycle, processing restriction, and the
  narrow contracts that resolve notification recipients and Staff Rights
  authority state.
- Workspaces owns onboarding, access plans, access closure, and the existing
  Staff-correlation companion. It does not become a notification-history index.
- Operations Notifications owns BunkFy audience policy, Staff history
  references, Staff Rights companion expansion, export, closure, restore
  adaptation, and the executable catalogue for exported inbox copies.
- GMA Notifications owns addressed-copy persistence, the reserved recipient
  reference, opaque producer references, lifecycle state, close receipts,
  delivery-lease safety, and replay suppression.
- Data Rights owns case scope, selected coordinates, required-companion closure,
  approval evidence, dispatch, protected artifacts, ledger, and restore order.

## Stable Staff Reference

Every BunkFy operational notification must carry an additional producer
reference derived from:

`bunkfy-notification-reference/v1 | normalized-tenant-id | staff | staff-member-id | staff | staff-member | staff-member-id`

The reference namespace and lowercase SHA-256 digest are stored by GMA. The raw
Staff id and Auth subject are not stored in the reference index.

Operations Notifications exposes:

- owner: `operations-notifications`
- record type: `staff-inbox-history`
- record id: the authoritative Staff member id
- record version: the GMA lifecycle-reference version

The Staff id remains stable when an account is relinked, so old and new inbox
copies stay in one Staff Rights history. Auth security notifications and generic
workspace broadcasts remain outside this product owner; their future
account-rights behavior belongs to the generic account surface.

## Recipient Resolution

Staff exposes a bounded contract that maps already-authorized Auth-subject
candidates to active, unrestricted Staff records. Operations Notifications:

1. obtains property-assigned Staff and workspace-owner candidates;
2. intersects them with authoritative active Organizations membership;
3. resolves the surviving subjects to unique Staff-member coordinates in
   bounded batches;
4. fails visibly when an authorized management recipient has no valid Staff
   correlation; and
5. projects each copy with its stable Staff reference.

The initiating-user exclusion remains before projection. Resolution never
enumerates a tenant, performs one query per recipient, or logs subject values.

## Required Companions

When an exact `staff/staff-member` coordinate is selected:

- access export ensures an open Staff inbox reference and adds its frozen
  Operations Notifications coordinate;
- anonymisation does the same before review, so later delivery makes the case
  stale instead of escaping closure; and
- duplicate expansion is idempotent and bounded by the central case capacity.

No weak lookup or notification-content search discovers Staff subjects.

## Approval And Execution

The Staff record remains the single policy authority. Operations Notifications
contributes one companion binding containing the exact lifecycle snapshot
version and canonical digest. Central policy evaluation must prove that the
companion points to the selected Staff authority coordinate.

Before execution, the Operations Notifications prerequisite revalidates the
frozen binding. The owner then closes only the exact Staff reference with the
Data Rights work-item id as operation identity. A version change blocks as
stale, an active delivery lease retries, overflow blocks visibly, and exact
replay returns the same proof.

Closing the Staff reference removes only that Staff member's BunkFy operational
copies. Cross-linked reservation reference versions advance naturally, making
concurrent Guest Rights decisions stale without affecting other recipients.

## Export

Staff Rights export pages the exact Staff reference in stable stream order and
buffers the bounded result before writing:

- notification id and immutable notification version;
- source module and notification name;
- title, body, severity, and occurrence/creation times;
- the closed typed navigation payload;
- routing tags and delivery policy.

The recipient Auth subject, read state, delivery attempts, provider addresses,
and unrelated Auth/security notifications are excluded. A dedicated Staff
export descriptor and catalogue fields keep Staff and Guest policy metadata
separate even though both use the same GMA history reader.

The Staff lifecycle uses GMA's maximum close ceiling of 10,000 records rather
than silently truncating. Exceeding it blocks export and anonymisation until an
operator resolves the retention breach.

## Restore

Tenant-scoped restore uses the version 3 prerequisite and contributor:

- a pre-close backup must expose the exact open selected version;
- a post-close backup must expose the exact resulting closed version;
- the close request reuses the original operation identity and record ceiling;
- the resulting generic receipt must reproduce the ledger hash; and
- missing, conflicting, oversized, or unrelated state fails closed.

The reference is derived only from tenant and Staff-member id, so restore never
depends on the Staff, Workspaces, or Auth mutation order.

## Legacy History

Existing notification rows do not contain the new Staff producer reference and
cannot be safely backfilled from arbitrary payload JSON or a mutable current
account link. No real-data production environment is approved yet.

Before first production admission, pre-production notification history and
pending source-event replays must be drained or reset. After admission, the
reference-presence guard and exact deployment proof are mandatory.

## Verification

- deterministic, tenant-separated Staff reference hashing;
- bounded recipient resolution, uniqueness, restriction, and missing-correlation
  failure behavior;
- every operational projection carries exactly one Staff reference in addition
  to any resource reference;
- account relinking keeps one stable Staff history;
- companion expansion is exact, idempotent, capacity-bounded, and stale-safe;
- policy contribution binds the Operations coordinate to the selected Staff
  authority and exact lifecycle snapshot;
- Staff export is deterministic, excludes recipient identity, and writes no
  partial fragment on stale or oversized history;
- scoped execution, delivery-lease retry, closure, replay suppression, and
  version conflict behavior;
- pre-close and post-close restore replay;
- Staff and Operations Notifications catalogues, output-sink partition,
  architecture boundaries, and host composition;
- focused checks while editing, one coherent non-Docker repository gate at
  slice end, and one exact PostgreSQL lifecycle scenario before publication.

## Completion Evidence

- Operations Notifications passes 54 focused tests, including tenant
  separation, recipient-correlation failure, closed notification-contract
  export, no-partial-output, overflow, delivery-lease retry, replay, and
  restore behavior.
- Staff passes 147 tests and Data Rights passes 261 tests. The 77-test
  architecture suite verifies module boundaries, catalogue coverage, output
  sinks, solution discoverability, and host composition.
- The synchronized repository graph builds with zero warnings and zero errors,
  every migration model is drift-free, and the coherent non-Docker gate passes
  all 3,536 applicable tests.
- The exact GMA Notifications PostgreSQL lifecycle scenario passes reference
  preparation, stable paging, active-delivery conflict, close, receipt replay,
  conflicting replay, late-write suppression, and close-versus-projection
  concurrency.
- Staff personal-data catalogue v9 binds the narrow recipient and authority
  contracts. Operations Notifications catalogue v4 contains 16 notification
  fields plus separate 13-field Guest and 13-field Staff export schemas.
- GMA remains unchanged: its generic lifecycle already supplies the required
  storage, concurrency, receipt, and replay guarantees.

## Deferred

- account self-service export or deletion for generic Auth/security
  notifications;
- delivered provider-side email, SMS, or push deletion;
- legal approval and activation of notification retention periods;
- histories above GMA's generic close ceiling;
- broad tenant-termination deletion and backup expiry; and
- Ingestion source-link notification coverage, which remains the next
  Operations Notifications owner slice.

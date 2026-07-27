# Staff Data Rights Owner Capability Task

Status: implemented; local verification complete; publication pending

## Goal

Make Staff the authoritative discovery and export owner for tenant-scoped
`StaffRights` cases. An authorized operator must be able to identify one Staff
profile exactly, select its current version, and obtain a bounded Staff-owned
export without treating an employment profile as property-owned data.

This slice implements access/export only. Staff correction, restriction,
erasure, anonymisation, and coordinated Auth or AccessControl changes remain
closed.

## Ownership Boundary

Staff owns:

- Staff profile and employment lifecycle data;
- current and historical property assignments;
- exact Staff subject discovery and selection revalidation;
- the Staff export schema and catalogue bindings; and
- tenant-local lookup and export efficiency.

Data Rights owns:

- case admission, requester verification, decision, and selected coordinates;
- contributor routing by `StaffRights`;
- export-fragment contracts and future protected artifact assembly; and
- tenant-level authorization for the case workflow.

GMA owns generic scope context, CQRS, dependency injection, persistence, and
runtime primitives. Staff lookup vocabulary, employment fields, record types,
and export policy remain BunkFy-specific. No GMA change is required.

## Subject Coordinate

Staff exposes one coordinate:

- owner: `staff`
- record type: `staff-member`
- record id: Staff member id
- record version: Staff member aggregate version

Discovery accepts exactly one strong coordinate:

- exact Staff member id; or
- exact Auth account subject id.

Work email and work phone are not identity keys and are not guaranteed unique.
Name and date-of-birth lookup are not introduced. `PropertyId` must be null,
the case type must be `StaffRights`, and the request tenant must match the
active scope.

Active, suspended, and departed profiles remain discoverable because departure
does not erase employment history. Candidate output contains only display name
and masked work-contact hints.

## Selection

Selection revalidation must:

- require owner `staff` and record type `staff-member`;
- enforce tenant scope and reject any property scope;
- reject empty ids or non-positive versions;
- return `NotFound` for another or missing Staff record; and
- return `Stale` when the aggregate version has changed.

No selected preview, lookup criterion, or Auth subject id enters events, tasks,
logs, metrics, traces, notifications, or durable Data Rights case storage.

## Export

The profile export contains reviewed Staff-owned subject data:

- Staff member id and version;
- display and legal names;
- work email and phone;
- employee number, job title, and department;
- Auth subject id;
- employment status;
- profile creation and last-change timestamps;
- suspension and departure timestamps; and
- departure effective date.

Each historical property assignment is a separate deterministic record with:

- assignment id and Staff member id;
- property id and property-specific job title;
- primary and current flags;
- effective dates;
- assignment and unassignment timestamps; and
- assignment and unassignment Staff aggregate versions.

Tenant scope ids, normalized search columns, operator actor ids, and
unstructured unassignment reasons are excluded. Those fields either add no
subject value or can disclose another person or unrelated operational detail.

The contributor loads at most 1,001 assignments to enforce a 1,000-assignment
ceiling before writing anything to the sink. Export order is profile first,
then assignments by id. A missing, stale, cross-tenant, wrong-case, or
property-scoped coordinate fails closed.

## Catalogue

Add a transient Staff export-fragment retention policy and classify every
internal export record member on the `data-rights-export` surface.

The executable schema must prove:

- every exported member is explicitly catalogue-approved;
- every approved export member is emitted exactly once;
- field ids and values stay within Data Rights contract limits;
- rights policy is `include-in-authorized-staff-export`;
- the authoritative owner is `staff`; and
- the boundary is cross-module only through the Data Rights export sink.

The deterministic Staff personal-data inventory is regenerated from the
catalogue.

## Composition

Register Staff discovery and export contributors from Staff Persistence as
enumerable Data Rights contract implementations. Staff references only
`BunkFy.Modules.DataRights.Contracts`; Data Rights never references Staff
internals or reads the Staff schema.

## Verification Cadence

During implementation run only focused Staff and affected architecture tests.
At the completed-slice gate run:

1. Staff discovery tests for exact id/account lookup, masking, tenant and case
   isolation, departed profiles, and bounded criteria.
2. Staff selection tests for valid, missing, stale, and malformed coordinates.
3. Staff export tests for deterministic profile/assignment records, catalogue
   completeness, stale and cross-scope rejection, and pre-write overflow.
4. Composition coverage proving only Staff handles `StaffRights`.
5. One real PostgreSQL integration path for tenant isolation and export.
6. The complete non-Docker verifier once.
7. The complete Docker suite once.
8. Preview and exact-commit GitHub gates once before publication.

After a gate failure, use the narrowest affected tests while fixing the batch,
then repeat the full gate once.

## Deferred

- protected export artifact assembly and delivery;
- Staff correction and processing restriction;
- Staff anonymisation, tombstones, protected ledger deltas, and restore;
- legal-hold and country-specific employment retention execution;
- redacted access to operator audit attribution or free-text reasons;
- coordinated Auth disablement or AccessControl revocation; and
- public Staff self-service data-rights requests.

## Implementation Notes

- Staff Persistence registers one discovery contributor and one export
  contributor through `BunkFy.Modules.DataRights.Contracts`.
- Exact Staff id and Auth subject lookup, masked hints, departed-profile
  discovery, stale selection, scope rejection, deterministic export, catalogue
  completeness, and pre-write overflow are covered by focused tests.
- Profile and assignment history are read as one bounded database snapshot so
  a concurrent assignment change cannot mix Staff aggregate versions.
- A PostgreSQL integration test covers DI composition, Auth-subject discovery,
  and profile plus assignment export. It runs only in the final Docker gate.
- No migration, public API, generated client, GMA, Auth, or AccessControl
  change is required for this access/export slice.

## Verification Evidence

- Focused Staff discovery, export, and catalogue checks: 12 passed.
- Complete non-Docker verifier: synchronized solution and package boundaries,
  zero-warning build, all migration drift checks, and 2,783 tests passed.
- Complete Docker gate: 62 integration tests passed in 7 minutes 36 seconds,
  including the Staff PostgreSQL discovery/export path.
- The final evidence applies to the implementation; the status and evidence
  notes added afterward are documentation-only and do not require repeating
  unchanged expensive gates.

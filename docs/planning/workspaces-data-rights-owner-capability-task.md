# Workspaces Data Rights Owner Capability Task

Status: current Workspaces owner capabilities complete and locally verified; publication pending

## Goal

Make Workspaces an explicit owner in the BunkFy Data Rights workflow without
moving onboarding, access coordination, or retention-proof semantics into the
Data Rights module or GMA.

The capability began with tenant-scoped discovery, selection validation, and
protected export for Workspaces-owned Staff records. Subsequent bounded slices
added correction, restriction, required-companion expansion, anonymisation,
restore proof, retention correlation scrubbing, and tenant termination without
moving Workspaces semantics into Data Rights or GMA.

## Ownership Boundary

- GMA Organizations owns invitations, enrollment links and claims,
  organization memberships, and their authoritative lifecycle.
- GMA Auth owns account identities and account-subject identifiers.
- GMA Access Control owns profiles, grants, and effective assignments.
- Staff owns durable staff profiles and employment lifecycle.
- Workspaces owns onboarding applications, access plans, access coordination
  processes, and workspace retention-correlation receipts.
- Data Rights owns cases, requester verification, approval, selected subject
  coordinates, protected artifacts, execution orchestration, and the canonical
  processing ledger.
- GMA owns only generic CQRS, scoping, persistence, messaging, task, and
  authorization mechanics. No Workspaces record type, field, lookup rule, or
  rights behavior belongs in GMA.

Data Rights must never read or mutate the Workspaces schema. Workspaces
implements the existing contributor contracts and remains the only writer of
its records.

## Subject Coordinates

Workspaces exposes the owner key `workspaces` for `StaffRights` cases at tenant
scope. Property-scoped cases are rejected.

| Record type | Record id | Record version |
| --- | --- | --- |
| `staff-onboarding` | onboarding application id | onboarding version |
| `staff-access-process` | access process id | access process version |
| `staff-access-plan` | access plan/source id | access plan version |
| `staff-retention-correlation-receipt` | receipt id | receipt contract version |

The coordinates stay separate because these records have independent
lifecycles and concurrency. A synthetic workspace-staff coordinate would hide
stale selection, make partial export ambiguous, and couple unrelated
aggregates.

## Discovery

Discovery accepts exactly one strong lookup:

- an exact record id; or
- an exact Auth account-subject id.

Email, phone, name, date-of-birth, and mixed lookups are rejected. This matches
the Staff owner lookup contract so an owner-agnostic Staff Rights search can
call both contributors without one weakening the other.

An exact record-id lookup may match either a Workspaces record id or a linked
Staff member id. This lets an authorized case find all remaining Workspaces
records after the Staff record is known. An account-subject lookup may match:

- `WorkspaceStaffOnboarding.SubjectId`;
- `WorkspaceStaffAccessProcess.SubjectId`;
- `WorkspaceStaffAccessProcess.RequestedBy`; or
- `WorkspaceStaffAccessPlan.CreatedBySubjectId`.

Receipt-local `retained:` pseudonyms are not accepted as account-subject
lookups. A retention receipt is discoverable by its own id or linked Staff
member id, but the scrubbed pseudonym must not become a new correlation key.

Results are deterministic, tenant-filtered, de-duplicated by coordinate, and
bounded by the shared Data Rights candidate limit. Candidate labels expose
only enough context to distinguish record type and lifecycle; contact hints
are masked and appear only for a matching onboarding record.

Selection validation re-reads the exact tenant-owned record and compares its
current version. Missing, cross-tenant, malformed, and stale coordinates fail
closed.

## Protected Export

Workspaces implements one catalogue-driven export contributor for the four
record types. The export contains only the currently retained owner state. It
does not reconstruct applicant fields already redacted by onboarding
completion or original subject identifiers removed by Staff retention.

### Onboarding Record

Export includes:

- source, claim, subject, and Staff correlation;
- applicant fields still retained on the selected record;
- lifecycle status, bounded failure code, version, and timestamps; and
- workspace scope.

### Access Process

Export includes:

- Staff and subject correlation;
- target state, Staff version, effective date, and requesting actor;
- process state, bounded failure code, version, and timestamps; and
- ordered access-profile snapshots.

Profile snapshots are emitted as separate records with stable deterministic
UUIDs derived from the selected process and snapshot identity. This avoids an
unbounded structured field and gives each child record an unambiguous export
identity.

### Access Plan

Export includes:

- source kind and source id;
- profile id and profile key;
- attributed creating subject;
- lifecycle status, source-expiry observation, version, and timestamps; and
- ordered property assignments.

Property assignments are emitted as separate records with stable deterministic
UUIDs derived from the selected plan and property id.

### Retention Correlation Receipt

Export includes the immutable receipt id, execution id, linked Staff member,
selected Staff version, scrub counts, completion time, contract version, and
canonical hash. Export never changes or expands the proof.

All export fields require an explicit `data-rights-export` catalogue binding.
The descriptor is loaded from the embedded Workspaces catalogue, validates the
catalogue identity and rights policy, and rejects an unmapped model member.
The owner fails closed before writing when the selected record is missing,
stale, malformed, outside tenant scope, or exceeds an owner record bound.

## Correction

Correction is complete for the Workspaces-owned staged applicant profile while
the onboarding record remains editable. Authority handoff, immutable proof,
idempotency, export, and operator behavior are specified in
[Workspaces Data Rights Correction](workspaces-data-rights-correction-task.md).

## Restriction

Restriction is complete for exact Workspaces-owned onboarding records. The
owner-local projection and receipts suppress optional processing while keeping
access-safety, legal, rights, retention, and expiry paths available. See
[Workspaces Data Rights Restriction](workspaces-data-rights-restriction-task.md).

## Anonymisation And Restore

Required-companion expansion, commutative Staff/Workspaces execution,
idempotent owner proof, protected tombstones, and pre-ready restore replay are
complete. The product contract remains in BunkFy Data Rights and contains no
Workspaces-specific vocabulary. See
[Workspaces Data Rights Anonymisation](workspaces-data-rights-anonymisation-task.md).

## Persistence And Efficiency

The discovery/export baseline adds no tables or business-state columns. One
tenant-leading onboarding index is added for linked Staff-member discovery;
existing indexes cover subject, access-process Staff member, actor,
plan-creator, and retention-receipt lookups. Queries are `AsNoTracking`,
projection-only where practical, deterministically ordered, and capped before
materialization.

Later correction, restriction, anonymisation, retention-correlation, and
tenant-termination slices own their bounded state, indexes, locking, and
append-only proof in Workspaces persistence; their task ledgers define those
contracts and evidence.

Export performs one indexed aggregate read plus one bounded child read for the
selected coordinate. It never scans another tenant, another module's schema,
or the complete Workspaces history.

## Catalogue

Update the executable Workspaces personal-data catalogue and generated
inventory for:

- the four subject coordinates;
- discovery request and candidate surfaces;
- catalogue-approved export models and child records;
- the transient protected-export fragment retention policy; and
- explicit cross-module `data-rights-export` boundaries.

No Workspaces personal data may be added to logs, metrics, traces,
notifications, or support bundles.

## Implementation Slices

1. [Complete] Audit Workspaces ownership, authority, record lifecycles,
   existing retention behavior, and Data Rights contract fit.
2. [Complete] Add tenant-scoped discovery, selection validation,
   catalogue-driven protected export, and focused tests.
3. [Complete] Define and implement active-onboarding correction with exact
   authority routing and immutable owner proof.
4. [Complete] Define and implement processing restriction without weakening
   access-safety or legal obligations.
5. [Complete] Add generic BunkFy Data Rights required-companion selection,
   then implement Workspaces anonymisation and restore proof.
6. [Complete] Add retention-correlation scrubbing and Workspaces-owned tenant
   termination export/destruction with terminal proof.

## Verification

1. Owner-agnostic Staff Rights discovery succeeds with both Staff and
   Workspaces contributors for the same exact lookup shape.
2. Weak, mixed, cross-tenant, property-scoped, and retained-pseudonym lookups
   fail closed.
3. Record-id discovery finds both direct Workspaces ids and linked Staff ids,
   with deterministic bounded output.
4. Selection validation detects stale aggregate versions and validates the
   immutable receipt contract version.
5. Export writes only the selected tenant record and its owned children in
   deterministic order.
6. Redacted and scrubbed values are exported only in their current minimized
   form.
7. Export rejects malformed, stale, missing, cross-scope, and oversized owner
   state before reporting success.
8. The catalogue schema rejects any export model member without an approved
   field binding.
9. Dependency and architecture tests prove that Workspaces references only
   Data Rights Contracts and that Data Rights does not reference Workspaces
   persistence.
10. Focused Workspaces tests, architecture tests, migration drift, the
    coherent non-Docker gate, one exact final Docker scenario if required,
    and exact-commit GitHub checks pass.

## Verification Evidence

- Workspaces domain, persistence, catalogue, and API tests: 147 passed.
- Complete `eng/verify.ps1 -SkipRestore` gate: synchronized solution and
  source-package guards, zero-warning build, every PostgreSQL and mounted GMA
  provider migration at zero drift, and 3,389 non-Docker tests passed.
- Exact
  `WorkspacesDataRightsExportIntegrationTests.Workspaces_discovery_and_export_use_authoritative_postgresql_records`
  PostgreSQL scenario: 1 passed. It proves a populated migration upgrade,
  partial Staff lookup index, shared Staff/Workspaces discovery shape, tenant
  isolation, stale selection, catalogue registration, bounded child-query
  translation, all four Workspaces exports, and deterministic replay.
- GMA framework, modules, and extensions remain unchanged. The existing
  Data Rights contributor contracts, GMA scoping, and persistence primitives
  were sufficient.

## Deferred Product And Legal Decisions

- approved legal bases and jurisdiction-specific response obligations;
- founder or counsel approval of engineering-default retention policies;
- any cross-workspace subject search.

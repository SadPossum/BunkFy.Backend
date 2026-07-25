# Reservations Data Rights Workflow Task

Status: implementation in progress; scoped discovery and catalogue-driven
export complete, transactional correction next

## Outcome

Make Reservations a complete owner in the BunkFy DataRights workflow without
weakening booking, stay, provider-reconciliation or audit correctness.

An authorized controller operator must be able to discover a reservation,
select an exact version, export every Reservations-owned subject record,
correct or restrict permitted processing, and execute an approved irreversible
redaction when current policy and operational obligations allow it.

Archive, cancellation, unlinking a Guest Record and deleting a reminder are not
erasure. A reservation remains Reservations-owned even when it has a canonical
Guest link or originated in Ingestion.

## Boundary Decision

DataRights owns:

- case, verification, decision, approval and execution lifecycle;
- selected owner coordinates and operation revisions;
- PII-free work items, completion state and the authoritative ledger;
- protected artifact assembly and restore orchestration.

Reservations owns:

- booking identity/contact snapshots and notes;
- stay, source, lifecycle and allocation facts;
- active and historical Guest links;
- before/after details history;
- adapter operation receipts and reminder state;
- Reservations-specific correction, restriction, hold and redaction rules;
- immutable owner receipts, local tombstones and restore replay.

Guests owns durable Guest Records and their rights behavior. A Guest
anonymisation event prevents new links but does not silently rewrite a
historical reservation. Ingestion owns raw and normalized provider evidence.
Inventory owns physical allocation truth. Each owner receives an explicit
coordinate and produces its own receipt.

GMA remains unchanged. Contributor registration, Cqrs, tasks, messaging,
scoping, access control, recent-authentication policies, persistence retries,
health checks and runtime primitives already support this workflow. Reservation
rights vocabulary and disposition rules are BunkFy product concerns.

## Subject Coordinate And Discovery

The owner coordinate is:

- owner: `reservations`;
- record type: `reservation`;
- record id: the Reservations aggregate id;
- record version: the aggregate `Version`.

Discovery is property scoped and accepts exactly one strong coordinate:

- exact reservation id;
- normalized email; or
- normalized phone.

An optional normalized name may refine a strong coordinate. Name-only,
date-only, source-reference-only and fuzzy searches are rejected. Results are
bounded, deterministically ordered and expose only the reservation coordinate,
primary name and masked contact hints.

The current generic record-id lookup is owner-record specific. Bounded
cross-owner expansion from a selected Guest Record is a later DataRights
contract slice; it must be explicit rather than interpreting one id as both a
Guest and Reservation id.

Retired properties remain discoverable when their projection is known.
Unknown property projection, wrong tenant, disabled scope, malformed lookup or
unsupported coordinate fails closed.

Selection revalidates property ownership and exact aggregate version. It does
not use a candidate preview as authority.

## Catalogue-Driven Export

Reservations streams a versioned export fragment through
`IDataRightsSubjectExportContributor`. It never places export payloads in
tasks, messages, logs, metrics, traces, notifications or receipts.

One selected reservation can produce:

- one reservation record containing current booking, stay, source, allocation
  and lifecycle facts;
- one record per active or historical Guest link;
- one record per details-history revision;
- one record per subject-linked adapter operation receipt;
- one record per arrival-reminder state.

Transport journals, projection checkpoints and rebuild copies are excluded.
Local property, Inventory and Guest projections are other owners' data or
rebuildable copies, not additional Reservations subject records.

Every exported member has a `data-rights-export` catalogue binding, approved
cross-module boundary, transient export-fragment retention and exact field id.
Composition fails when a DTO member lacks a binding, an unrelated field is
bound, an export policy differs, a record exceeds its bound, or the embedded
catalogue identity/version is wrong.

The contributor:

- validates tenant/property scope and known property projection;
- validates owner, record type, id and version;
- projects only the selected aggregate and independently linked records;
- orders child records by stable keys;
- streams records without loading all tenant data;
- returns `Stale`, `NotFound` or `ScopeUnavailable` without partial success.

The caller discards every partially written record unless the final result is
successful.

## Correction

Correction continues through Reservations aggregate commands; DataRights never
submits a generic patch or writes the Reservations schema.

An approved correction binds:

- tenant, property, case and approval revision;
- reservation id and selected aggregate version;
- a bounded semantic field set;
- expected details revision;
- actor, idempotency key and completion time.

The aggregate mutation, details-history entry, domain event, outbox event and
PII-free owner receipt commit atomically. Equivalent retries return the same
receipt; a changed key payload or stale revision fails closed.

Historical facts are superseded through normal history semantics. Correction
does not erase the previous snapshot.

## Processing Restriction

Reservations owns an explicit case-scoped restriction projection independent
from the existing Guest-link eligibility projection.

Restriction suppresses non-required:

- list, detail and history reads outside the rights workflow;
- guest-detail edits and canonical linking;
- adapter guest/detail amendments;
- reminders and operational notifications containing subject context;
- new processing derived from the reservation.

Policy-approved stay, safety, fraud, chargeback, statutory or controller
obligations may continue through narrowly named paths. Unknown restriction
state or a future contract version fails closed. The rights export and
controller review path remain available.

Restriction changes are idempotent, versioned and PII-free. DataRights records
the owner receipt; Reservations remains the authority that enforces it.

## Holds And Destructive Eligibility

Reservations owns independently releasable holds keyed by tenant, property and
reservation. A hold stores stable reason code, lifecycle actors/times and
optimistic version only; free-form legal advice is prohibited.

Irreversible redaction is blocked when:

- the reservation has an active or future operational stay;
- allocation or release work is pending or unresolved;
- a current policy or retention binding is unknown, stale or denies erasure;
- a Reservations hold is active;
- selected aggregate/details versions are stale;
- a provider reconciliation receipt still requires a direct reference;
- another destructive operation is unresolved.

Cancelled, no-show and checked-out records are not automatically eligible.
Current country and retention policy decides which minimum facts must remain.

## Irreversible Redaction

When approved, Reservations preserves only policy-approved non-identifying
booking facts needed for operational, financial and audit correctness.

The owner operation:

- replaces the primary guest name with a fixed non-identifying label;
- clears email, phone and free-text notes;
- removes or pseudonymizes the source reference according to current provider
  reconciliation policy;
- removes pending guest/contact/free-text amendment values;
- removes active and historical direct Guest links;
- redacts direct identity/contact/free text from every details-history
  snapshot while preserving revision and changed-field facts;
- reduces adapter receipts to approved reconciliation proof;
- suppresses pending reminders that no longer have an allowed purpose;
- writes an immutable owner receipt and local tombstone;
- emits one PII-free versioned event.

It does not fabricate dates, status, guest count, allocation facts or staff
audit attribution. Staff rights handle staff actor identity separately.

Ordinary reservation reads and writes exclude a terminally redacted record
unless a narrowly named policy-approved operational surface requires its
minimum facts.

## Ledger And Restore Safety

The generic DataRights anonymisation contributor is used with Reservations
coordinates only after eligibility and owner proof are implemented.

Reservations commits redaction, local tombstone, immutable receipt and outbox
event first. DataRights then durably flushes the protected external ledger delta
before appending its database ledger and completing the work item.

The Reservations restore contributor:

- accepts only verified external ledger proof;
- re-redacts a restored pre-operation aggregate and child records;
- verifies or recreates PII-free owner proof without fabricating the original
  receipt;
- writes an immutable restore receipt and advances the local tombstone;
- remains idempotent under crash/replay.

API, Admin API, Admin CLI and Worker readiness remain false until the existing
DataRights restore gate confirms the Reservations replay. Migrations remain
ungated.

## Efficiency

- Discovery uses tenant/property-first indexes and bounded paging.
- Export projects one selected reservation and streams child rows in stable
  order.
- Restriction, hold, receipt and tombstone indexes start with tenant/property
  and reservation coordinates.
- One work item mutates one reservation and one operation revision.
- Events, tasks, receipts and ledger records are constant-size and PII-free.
- No workflow scans a tenant, hashes every reservation, or copies another
  module's tables.

## Delivery Slices

1. [Complete] Add scoped discovery, exact selection revalidation and
   catalogue-driven export for current and historical Reservations-owned data.
2. [Next] Add approved transactional correction and immutable owner receipts through
   existing aggregate/history semantics.
3. Add Reservations-owned processing restriction, enforcement and rebuildable
   PII-free state.
4. Add holds and fail-closed destructive eligibility.
5. Add irreversible aggregate/history/link/receipt redaction, owner proof and
   terminal ordinary-surface enforcement.
6. Add DataRights work-item dispatch, protected ledger completion,
   Reservations tombstone and pre-ready restore replay.
7. Add operator API/UI flows, migration/architecture/Docker/browser evidence
   and exact-commit publication gates.

Only one numbered slice is implemented at a time. Existing modules may be
touched through contracts, projections and tests, but Reservations remains the
only owner of its data.

## Completed First Slice

- Reservations registers `reservations/reservation` discovery and export
  contributors through the existing DataRights Contracts protocol.
- Exact reservation id, normalized email or normalized phone lookup is
  property scoped, bounded and indexed. Optional exact name refinement is
  supported; name-only, multiple strong coordinates and unknown scope deny.
- Current and pending adapter-amendment name/contact search copies are
  synchronized by the aggregate. Migration
  `20260725172015_AddReservationDataRightsDiscovery` backfills existing rows
  before enforcing the required current-name search value.
- Selection revalidates property ownership and exact aggregate `Version`.
- Export schema `reservations.subject-export` version 1 streams the current
  reservation, pending amendment, Guest-link rows, details-history revisions,
  subject-linked adapter receipts and reminders in stable order.
- Staff actor fields remain outside this subject fragment. Partial sink writes
  cannot return a successful result.
- Personal-data catalogue version 3 binds every exported member and derived
  search copy. Composition fails when code, policy, boundary, retention or
  catalogue identity drifts.
- Focused unit tests cover current/pending lookup, masking, bounds,
  tenant/property isolation, retired/unknown properties, stale selection,
  complete child export, unrelated-row exclusion and sink failure.
- A PostgreSQL container test proves migration, contributor registration,
  provider query translation and export streaming against the real provider.
- GMA is unchanged.

## First-Slice Acceptance Evidence

- Two-tenant/two-property tests prove discovery, selection and export isolation.
- Name-only and malformed multi-coordinate lookup fail closed.
- Exact email/phone lookup is normalized, bounded and deterministically
  ordered with masked hints.
- Retired known properties remain accessible; unknown properties deny.
- A stale aggregate coordinate cannot export.
- Export includes current aggregate, every Guest-link row, every details
  history revision, subject-linked adapter receipt and reminder exactly once.
- No unrelated reservation, projection copy, transport row or another
  property's record is exported.
- Every export DTO member is catalogue-bound and the generated inventory is
  deterministic.
- Contributor payloads remain absent from messaging, tasks, notifications,
  logs, metrics, traces and support bundles.
- Architecture tests preserve contracts-only module dependencies and GMA
  remains unchanged.

## Non-Goals

- Guest-profile, Ingestion-source, Inventory, Staff or Workspaces erasure.
- Automatic SP-003 retention scheduling.
- Payment, tax, chargeback or identity-document semantics not yet modeled.
- Public guest self-service or requester verification.
- Private counsel decisions, immutable object-store implementation or hosted
  deployment procedures.

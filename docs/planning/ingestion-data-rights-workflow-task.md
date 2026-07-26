# Ingestion Data Rights Workflow Task

Status: first implementation slice complete

## Outcome

Make Ingestion a complete owner in the BunkFy DataRights workflow without
turning provider evidence into a central identity index or weakening adapter,
retention, legal-hold and reconciliation guarantees.

An authorized controller operator must eventually be able to include linked
raw and normalized provider evidence in an access export and execute an
approved irreversible reduction only when Ingestion's current legal-hold,
retention, reprocessing and reconciliation state allows it.

## Boundary Decision

DataRights owns:

- case, verification, decision, approval and execution lifecycle;
- selected owner coordinates and operation revisions;
- PII-free work items, completion state and the authoritative ledger;
- protected artifact assembly and restore orchestration.

Ingestion owns:

- adapter source links and provider identifiers;
- observation receipts and retained raw payload objects;
- normalized proposal, dispatch and operational-baseline history;
- reprocessing lineage and outputs;
- Ingestion-specific legal holds, retention and destructive eligibility;
- irreversible reduction, owner receipts, local tombstones and restore replay.

Reservations remains authoritative for the product reservation. Ingestion uses
an exact product reservation id only as an indexed lookup input; DataRights
stores the resulting Ingestion-owned source-link coordinate. DataRights never
reads Ingestion tables, parses provider payloads or treats a Reservations
version as authority for provider evidence.

GMA remains unchanged. The existing DataRights contributor contracts, GMA
scoping, Files, tasks, messaging, access control and persistence primitives are
sufficient. Provider evidence, rights policy and Ingestion reduction semantics
remain BunkFy product concerns.

## Subject Coordinate And Discovery

The owner coordinate is:

- owner: `ingestion`;
- record type: `reservation-source-link`;
- record id: the Ingestion `ReservationSourceLink` id;
- record version: the source link `Version`.

Discovery accepts only one exact product reservation id. Email, phone, name,
date of birth, source reference and fuzzy criteria are not supported:

- Ingestion does not duplicate reservation contact search indexes;
- provider references are strings and must not be forced into the generic Guid
  lookup;
- raw payloads are never scanned or parsed during discovery;
- an unlinked provider record cannot be selected through this coordinate.

The lookup uses the existing tenant-first reservation-link index, filters the
authorized property, is bounded and deterministically ordered. Multiple source
links may be returned when more than one provider connection owns evidence for
the same reservation.

Candidate previews contain only the source-link coordinate and a bounded
provider-system label. They expose no source reference, raw value, contact hint
or normalized guest value. Selection revalidates tenant, property, source-link
id and exact version. Unknown property projections, wrong scope, malformed
criteria and stale coordinates fail closed.

Unlinked evidence requires a later explicit source-coordinate workflow. It is
never included by broad tenant scans or inferred identity matching.

## Catalogue-Driven Export

One selected source link streams only its reachable Ingestion-owned evidence:

- the source link and normalized operational baseline;
- observation receipts referenced by the link, proposals or dispatches;
- proposals and dispatches for the source link or linked reservation;
- reprocessing attempts and outputs reachable from those receipts;
- raw payload content for reachable receipts whose local retention state is
  still `Available`.

Property legal-hold rows, adapter credentials, connection configuration,
unrelated runs, projection checkpoints and transport journals are excluded.
Staff actor fields are not guest-subject data and are excluded from this
fragment.

The export is driven by the checked Ingestion personal-data catalogue:

- every export DTO member has one `data-rights-export` binding;
- every binding has the approved cross-module boundary, export policy and
  transient export-fragment retention;
- composition fails when code, catalogue identity, policy or field membership
  drifts;
- records and child collections use stable ordering;
- no tenant-wide collection is loaded into memory.

Raw payloads can be up to 4 MiB while the neutral export contract bounds each
field value. The contributor therefore streams deterministic payload-chunk
records. Each chunk:

- is no larger than the serialized field limit;
- has a deterministic id derived from receipt id and chunk ordinal;
- carries receipt id, ordinal, count, total bytes and encoding;
- is discarded with the entire owner fragment unless the contributor returns
  success.

A receipt marked available whose protected object cannot be read makes the
fragment unavailable. Purged payloads export their receipt and retention
facts, but no fabricated content. Export payloads never enter tasks,
inbox/outbox messages, notifications, logs, metrics, traces or receipts.

## Legal Holds And Destructive Eligibility

The following slice will add one exact, fail-closed Ingestion owner operation.
It must re-evaluate current state under an owner-local operation fence and
block with stable codes when:

- any applicable Ingestion property legal hold is active;
- raw-payload purge or sensitive-history redaction is in progress;
- a reachable observation has an active reprocessing reservation or attempt;
- a proposal or dispatch is pending, applying or otherwise non-terminal;
- provider reconciliation still requires direct source evidence;
- current country or retention policy is missing, stale or denies reduction;
- the selected source-link coordinate is stale;
- a prior destructive result cannot be proven equivalent.

Property-wide legal holds remain Ingestion-owned. DataRights records only the
stable blocker and owner proof.

## Irreversible Reduction And Restore Safety

The destructive slice must define policy-approved behavior separately for:

- raw payload objects and their retention metadata;
- source references, external ids and normalized snapshots;
- operational facts required for idempotency, audit and reconciliation;
- reprocessing lineage;
- staff attribution owned by later staff-rights work.

Deleting a MinIO object, purging by age, unlinking a reservation or redacting a
terminal proposal alone is not complete anonymisation.

Ingestion must commit its reduction, immutable owner proof and local tombstone
before DataRights completes the protected ledger. Restore and re-ingestion
must consult that tombstone before evidence becomes readable or publishable.
API, Worker and management readiness remain gated until protected-ledger replay
has re-applied every Ingestion tombstone.

No destructive contributor is registered until owner proof, tombstone,
re-ingestion denial and restore replay are implemented together.

## Delivery Slices

1. Add exact reservation-linked discovery, selection revalidation and
   catalogue-driven streaming export for reachable raw and normalized evidence.
2. Add legal-hold, retention, reprocessing and reconciliation eligibility with
   stable blockers and an owner-local operation fence.
3. Add approved irreversible reduction, immutable owner proof and ordinary
   surface enforcement.
4. Add DataRights work-item dispatch, local tombstone, protected-ledger
   completion and pre-ready restore/re-ingestion replay.
5. Extend the operator workflow for explicit multi-owner selection and run
   migration, architecture, Docker, browser, security and exact-commit gates.

Only one numbered slice is implemented at a time. Existing modules may be
touched through contracts, projections and tests, but Ingestion remains the
only owner of its evidence.

## First-Slice Acceptance Plan

- Unit tests prove exact reservation-id lookup, multiple-link ordering,
  candidate bounds, tenant/property isolation, unknown-property denial, stale
  selection and rejection of every unsupported criterion.
- Export tests prove the exact source-link graph, unrelated-row exclusion,
  stable ordering, retained raw-payload chunk reconstruction, purged-payload
  omission, missing-object failure and partial-sink failure.
- Catalogue tests prove every export member is approved and the generated
  inventory is deterministic.
- Architecture tests prove Ingestion references only DataRights Contracts and
  no Ingestion or BunkFy rights vocabulary enters GMA.
- A real PostgreSQL and protected-file-adapter test proves provider query
  translation, contributor registration, property isolation and streamed raw
  plus normalized export.
- Add one receipt index starting with tenant, connection and exact external id
  so historical provider evidence is complete without scanning an adapter
  connection. The remaining graph queries use existing tenant-first
  source-link, receipt-lineage, proposal, dispatch and reprocessing indexes.
  Migration upgrade and drift evidence must remain clean.
- The current Reservations-only operator UI remains unchanged in this slice.
  Browser verification is limited to proving no navigation or generated
  contract regression; explicit multi-owner selection belongs to slice 5.
- Full non-Docker, Docker, generated-contract, dependency and exact-commit
  publication gates must pass before the slice is marked complete.

## First-Slice Implementation

- Ingestion resolves only exact Reservations ids through its tenant-first
  source-link projection and returns versioned Ingestion-owned coordinates.
- The registered export contributor streams only the selected source-link
  graph, with deterministic ordering, a hard 1,000-row graph bound and no
  tenant-wide materialization.
- Retained protected raw payloads are split into deterministic 12,000-byte
  records and remain below the neutral export field limit. A missing retained
  object fails the owner fragment; purged evidence exports metadata without
  fabricated content.
- The executable catalogue and generated inventory cover the transient export
  DTOs, cross-module boundary and raw-payload chunk metadata.
- PostgreSQL now indexes exact historical receipt lookup by tenant,
  connection, external id and receipt time.
- Focused tests cover exact and multiple-link discovery, stale coordinates,
  unrelated-row exclusion, chunk reconstruction, purged evidence and missing
  retained objects. A Docker integration test resolves the composed
  contributors against PostgreSQL and local protected file storage, while the
  migration upgrade test proves the new receipt index exists on an upgraded
  schema.
- No API, operator navigation or generated HTTP contract changes are part of
  this slice. The current Reservations-oriented operator workflow therefore
  remains unchanged.
- The complete repository verifier passed with a zero-warning workspace build,
  synchronized solution, source-package checks, every composed migration model
  and all eligible non-Docker suites. Ingestion has 151 focused tests and the
  architecture suite has 64 guards.
- The full Docker suite passed 53 tests with no failures or skips. The existing
  privacy-request operator route remained healthy with an HTTP 200 response.

## Non-Goals

- Fuzzy or raw-payload identity search.
- Treating provider references as generic Guid subject ids.
- Automatic selection of every owner from one reservation candidate.
- Ingestion anonymisation before blocker, tombstone and restore semantics are
  complete.
- Exporting adapter secrets, staff identity or unrelated property operations.
- Moving BunkFy DataRights or Ingestion semantics into GMA.

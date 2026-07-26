# Ingestion Data Rights Workflow Task

Status: first and second implementation slices complete and published; third
safety-foundation slice planned

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

The second slice adds one exact, fail-closed Ingestion eligibility operation.
It re-evaluates current state under an owner-local optimistic operation fence
and blocks with stable codes when:

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

The eligibility operation does not register an anonymisation contributor and
does not mutate provider evidence. It is an Ingestion-owned prerequisite used
by the later destructive contributor in the same owner transaction.

## Second-Slice Architecture Decision

The eligibility coordinate remains the selected reservation source-link id and
version. No email, phone, name, provider reference, raw-payload search or
cross-module query is accepted.

Eligibility loads the same bounded evidence graph as export:

- the selected source link and its adapter connection;
- exact receipts and reprocessing descendants;
- proposals and dispatches associated with the source link;
- reprocessing attempts and outputs;
- the property projection, governance policy and retention-fence version;
- active Ingestion legal holds.

The export and eligibility paths share one Persistence graph loader so graph
reachability, bounds and fail-closed behavior cannot drift. The loader uses
existing tenant-scoped indexes, excludes raw object content and returns
unavailable when the graph exceeds 1,000 records or has incomplete lineage.

The owner operation fence is a SHA-256 digest over a deterministic, ordered
snapshot of every graph record id, lifecycle state and optimistic version,
together with source-link, connection, policy and property retention-fence
versions. It is not an authorization token or a substitute for locking.

The later destructive handler must re-run eligibility inside its own
transaction, compare this digest and use optimistic database concurrency while
committing reduction, owner proof and tombstone. Any concurrent receipt,
proposal, dispatch, reprocessing, retention, legal-hold, connection or policy
change alters the current eligibility result or operation fence.

Stable blocker precedence is:

1. contract, request and tenant validation;
2. missing, stale, oversized or inconsistent owner graph;
3. property projection, current country policy and approved routing evidence;
4. active legal holds and retention work in progress;
5. active reprocessing or non-terminal observation, proposal or dispatch work;
6. provider reconciliation state that still requires direct source evidence.

The current model has no durable sensitive-history `Redacting` state because
redaction is an atomic database mutation. Its proposal or dispatch version and
the property retention-fence version change in the same transaction, so the
operation-fence digest detects completion. The destructive slice must still
re-evaluate under transaction before writing.

## Irreversible Reduction And Restore Safety

The destructive workflow must define policy-approved behavior separately for:

- raw payload objects and their retention metadata;
- source references, external ids and normalized snapshots;
- operational facts required for idempotency, audit and reconciliation;
- reprocessing lineage;
- staff attribution owned by later staff-rights work.

Deleting a MinIO object, purging by age, unlinking a reservation or redacting a
terminal proposal alone is not complete anonymisation.

PostgreSQL and object storage cannot be changed atomically. The later
destructive execution therefore uses an idempotent owner-local state machine:

1. Acquire the exact source-identity operation locks, re-run eligibility in the
   transaction and compare its current operation fence.
2. Commit a reducing barrier, redact database-owned sensitive values, mark
   retained raw objects unavailable and persist the bounded deletion plan.
3. Delete every planned raw object idempotently outside the database
   transaction.
4. In a second transaction, prove every object is absent, finalize the immutable
   owner receipt and completed tombstone, and only then report completion to
   DataRights.

A retry with the same idempotency key resumes the recorded stage and deletion
plan. It never rebuilds a broader graph from current tenant data. A conflicting
idempotency key, owner coordinate, operation revision, policy digest or graph
fence fails closed.

Source references and adapter external ids cannot remain in a tombstone merely
to prevent re-ingestion. Ingestion instead owns versioned keyed-HMAC
fingerprints over canonical tenant, connection and provider-record identities.
The stored fingerprint is purpose-separated, contains no plaintext provider
identifier and can be matched across configured key-rotation candidates.
Fingerprint semantics and key configuration stay in BunkFy Ingestion; they are
not DataRights contracts and do not move into GMA.

The reducing barrier denies raw reads, normalization, dispatch, reprocessing
and new matching observations before object deletion begins. The completed
tombstone continues that denial after owner completion. Ordinary API and export
surfaces expose neither the original values nor the fingerprints.

Restore does not restore erased provider data. The DataRights protected ledger
replays the immutable owner proof into Ingestion, which re-applies reduction to
the exact source-link graph and reconstructs required fingerprints from the
restored pre-reduction rows. API, Worker and management readiness remain gated
until protected-ledger replay has re-applied every Ingestion tombstone.

No destructive contributor is registered until the tombstone, fingerprint,
ordinary-surface denial, restore replay and staged owner proof are implemented
and verified in that dependency order.

## Delivery Slices

1. Add exact reservation-linked discovery, selection revalidation and
   catalogue-driven streaming export for reachable raw and normalized evidence.
2. Add legal-hold, retention, reprocessing and reconciliation eligibility with
   stable blockers and an owner-local operation fence.
3. Add the owner-local tombstone and keyed source fingerprints, enforce the
   barrier at ingress, dispatch, reprocessing, raw reads, discovery and export,
   and add protected-ledger restore replay. Do not register a destructive
   contributor.
4. Add staged irreversible reduction, immutable owner proof and DataRights
   work-item dispatch. Register the destructive contributor only when begin,
   object deletion, finalize and idempotent recovery are verified together.
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

## Second-Slice Acceptance Plan

- Contract tests prove every eligibility status and blocker code is stable,
  bounded and represented in the personal-data catalogue.
- Evaluator tests prove request, tenant, selected-version, property-policy and
  approved-routing-evidence validation fails closed.
- Graph tests prove active legal holds, raw purge, reprocessing reservations,
  queued or running attempts, pending observations, non-terminal proposals,
  non-terminal dispatches and unreconciled source links each block with the
  expected precedence.
- Operation-fence tests prove deterministic results and a changed digest after
  any reachable lifecycle, version, connection, policy or retention-fence
  change.
- PostgreSQL tests prove exact indexed graph loading, tenant/property
  isolation, descendant lineage, graph bounds and no raw object reads.
- Architecture tests prove the evaluator remains Ingestion-owned, references
  only BunkFy module contracts and shared data-governance primitives, and does
  not add a DataRights anonymisation contributor.
- Migration drift, focused tests, complete repository verification, Docker
  integration and exact-commit publication gates pass before the slice is
  marked complete.

## Third-Slice Architecture Decision

The safety foundation is an Ingestion-owned persistence and application
capability. DataRights supplies only its existing restore request and owner
proof contracts. GMA supplies the existing scope, transaction, task, readiness
and persistence primitives. Neither layer learns provider identity,
fingerprinting, evidence-graph or object-retention semantics.

One tombstone is keyed by tenant and reservation source-link id. It stores:

- a versioned lifecycle state and optimistic revision;
- property, connection and source-link coordinates;
- the selected and resulting source-link versions;
- the owner receipt identity and canonical digest when completion is proven;
- the protected-ledger entry and latest replay time after restore;
- only bounded counts and policy/fence digests required to verify owner state.

Child fingerprints cover the source-link identity and every reachable receipt
provider identity. They store purpose, key version and a fixed-length digest,
never source references, external ids, normalized values or raw content. A
unique tenant-purpose-key-version-digest index makes ingress checks exact and
bounded.

An Ingestion-specific HMAC service owns canonical length-prefixed encoding,
purpose separation and key rotation. Production configuration requires an
explicit active key and rejects the development key. Matching computes
candidates for every configured key version so rotation does not reopen an old
tombstone. Logs, metrics, traces, errors and notifications never contain keys,
digests or source values.

The restore contributor revalidates the DataRights owner proof, acquires the
same source-identity locks used by normal ingestion, loads the bounded current
graph, reapplies redaction and persists the completed tombstone atomically.
Replay is idempotent for the same ledger entry and fails readiness on missing,
ambiguous, oversized or conflicting owner state.

The normal receive path checks the provider-record fingerprint before storing
an object or adding a receipt. The normalized reservation dispatcher checks
again before creating or updating a source link, closing restore and
receive/dispatch race windows. Reprocessing, raw reads, subject discovery and
owner export reject a reducing or completed tombstone.

Slice 3 may register `IDataRightsAnonymisationRestoreContributor`; it must not
register `IDataRightsAnonymisationContributor`, expose an operator command or
create a new destructive API.

## Third-Slice Acceptance Plan

- Domain tests prove tombstone lifecycle, immutable owner proof, restore replay
  idempotency, conflict rejection and lower-case fixed-length fingerprint
  invariants.
- Cryptography tests prove deterministic tenant/purpose/key separation,
  candidate matching across key rotation, missing-key denial and production
  rejection of development defaults.
- Application tests prove direct and reprocessed ingress is denied before
  object storage, dispatch cannot recreate a tombstoned source link, and raw
  reads, reprocessing, discovery and export fail closed.
- Restore tests prove an exact bounded graph is reduced and fingerprinted,
  owner proof is revalidated, repeated replay is idempotent and incomplete or
  stale graphs fail readiness without partial mutation.
- PostgreSQL tests prove unique fingerprint lookup, tenant isolation,
  optimistic tombstone concurrency, transactional graph redaction and no
  plaintext provider identifiers in tombstone tables.
- Architecture tests prove only the restore contributor is registered, no
  destructive command or API is reachable, Ingestion references DataRights
  Contracts only, and GMA remains unchanged.
- The personal-data catalogue classifies the tombstone, fingerprints and
  restore proof with explicit access, retention and rights behavior.
- Migration upgrade/drift, focused tests, complete verification, Docker and
  exact-commit publication gates pass before staged irreversible execution
  begins.

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

## Second-Slice Implementation

- Ingestion exposes one versioned, cross-module eligibility evaluator for an
  exact source-link id and version. The request carries approved DataRights
  case and routing-policy evidence; the result returns only stable status,
  blocker, current owner versions, graph count and policy/fence digests.
- The export and eligibility paths use one bounded Persistence graph loader.
  It accepts a complete graph at the documented 1,000-record limit, rejects an
  oversized graph and fails closed when required receipts, reprocessing
  attempts or output lineage are incomplete.
- Eligibility revalidates tenant scope, source-link version, property
  projection, current country and retention policy, approved routing evidence,
  active Ingestion legal holds, raw-payload purge state, reprocessing
  reservations and attempts, pending observations, proposals and dispatches,
  and provider reconciliation state.
- A deterministic SHA-256 operation fence covers source-link, connection,
  property policy, retention fence, legal-hold count and every reachable graph
  record id, lifecycle state and optimistic version. Tests prove ordering does
  not affect the digest and each protected scope changes it.
- Repository snapshots are internal Application-to-Persistence collaboration,
  not public module contracts. The executable personal-data catalogue and
  generated inventory classify the intentional transient cross-module
  request, policy evidence and result.
- No raw payload object is read by eligibility, no schema migration is needed,
  and no `IDataRightsAnonymisationContributor` is registered. Irreversible
  reduction remains blocked on owner proof, tombstone, re-ingestion denial and
  restore replay in slices 3 and 4.
- Focused verification passes 164 Ingestion tests, 64 architecture guards and
  the real PostgreSQL/local-file data-rights scenario. The complete verifier
  passes solution synchronization, source-package checks, a zero-warning
  build, every migration drift check and 2,705 non-Docker tests. The complete
  Docker suite passes 53 tests with no failures or skips.

## Non-Goals

- Fuzzy or raw-payload identity search.
- Treating provider references as generic Guid subject ids.
- Automatic selection of every owner from one reservation candidate.
- Ingestion anonymisation before blocker, tombstone and restore semantics are
  complete.
- Exporting adapter secrets, staff identity or unrelated property operations.
- Moving BunkFy DataRights or Ingestion semantics into GMA.

# Guest Data Rights And Lifecycle Workflow Task

Status: implementation in progress; Guests discovery, subject selection,
export preparation, transactional correction and processing restriction
complete; DataRights decision lifecycle and approval gate complete

## Outcome

Implement company-readiness control SP-002. An authorized hostel operator must
be able to open a scoped case, locate relevant records, export them, coordinate
correction or restriction, execute approved erasure or irreversible
anonymisation, and prove completion across BunkFy without bypassing module data
ownership.

This is a management workflow. BunkFy does not expose a guest portal or disclose
hostel-controlled data directly to an unverified requester. The authenticated
workspace remains the controller-facing decision maker; private hosted
operations may route BunkFy-controller requests and complaints separately.

## Boundary Decision

Create one new product module named `DataRights`. It owns:

- the rights case lifecycle and optimistic concurrency;
- controller routing, requester-verification status, due dates and decisions;
- the exact country-policy coordinates used for each decision;
- PII-free module work items and their progress;
- completion receipts and the authoritative deletion/anonymisation ledger;
- task orchestration, retry and operator-facing status;
- export-manifest assembly and short-lived download authorization.

Existing modules continue to own all personal data and every mutation of it.
Guests, Reservations, Ingestion, Inventory, Operations Notifications, Staff,
Workspaces and later modules each own:

- discovery of their own candidate records;
- export rendering from their own executable personal-data catalogue;
- correction through their existing domain commands;
- restriction enforcement on their own read/write surfaces;
- legal-hold and retention checks;
- erasure, redaction or anonymisation semantics for their own records;
- idempotent module receipts and local tombstone projections.

`DataRights` never reads another module's tables, writes another module's schema,
or treats a projection as the authoritative source for a destructive decision.
Cross-module work uses versioned contracts, outbox/inbox delivery and durable
tasks. Large or personal payloads do not travel through NATS or task metadata.

## GMA Boundary

No new GMA feature is required for the first slice.

Reuse:

- GMA AccessControl for tenant/property-scoped permissions;
- GMA Auth assurance policies for recent-authentication and MFA requirements;
- GMA Administration for optional audited CLI/Admin API execution;
- GMA Tasks for PII-free, idempotent and resumable work;
- GMA messaging for work requests and bounded result facts;
- GMA Files for protected, expiring export artifacts;
- GMA runtime time and id generation abstractions.

Do not move BunkFy subject types, rights vocabulary, country-policy decisions,
legal holds, export schemas, deletion ledgers or anonymisation rules into GMA.
A generic contributor protocol is considered only after at least two products
prove the same contract without BunkFy vocabulary.

## Case Model

Use a small aggregate plus append-only execution records instead of one giant
aggregate.

`DataRightsCase` owns:

- tenant id and optional property scope;
- case type: guest rights or tenant termination;
- requested operations: access/export, correction, restriction, erasure or
  anonymisation;
- controller actor and requester relationship;
- verification and routing status;
- selected opaque subject/record coordinates;
- decision state, due time, version and terminal outcome;
- policy id/version/digest, retention policy coordinates and acknowledgement
  evidence used for the decision.

Separate persistence owns:

- module work items keyed by case, module, operation and revision;
- append-only decision records;
- artifact manifests and expiry metadata;
- module completion/blocker receipts;
- deletion/anonymisation ledger entries;
- delivery-safe outbox/inbox state.

The aggregate does not contain exported guest payloads, provider source bodies,
free-form legal advice or copied module records.

## Lifecycle

Initial states:

1. `Draft`: an authorized operator selects the workspace/property, operation
   and known subject coordinates.
2. `Discovery`: modules search only their own indexed scope and return bounded
   candidate coordinates with minimal authorized previews.
3. `ReviewRequired`: the operator resolves duplicates, unlinked records,
   controller routing, verification and requested corrections.
4. `DecisionPending`: the system resolves current country-policy, retention and
   hold evidence; unresolved or contradictory evidence fails closed.
5. `Approved` or `Denied`: the decision is explicit, versioned and attributable.
6. `Executing`: PII-free module work items run idempotently and can be retried.
7. `Blocked`: at least one owner reports a legal hold, retention requirement,
   stale policy, unresolved identity or other stable blocker.
8. `Completed`, `PartiallyCompleted` or `Canceled`: terminal state is supported
   by immutable receipts rather than inferred from task status.

Archive remains an ordinary product lifecycle state and is never represented as
erasure or anonymisation.

## Discovery And Subject Coordinates

Do not create a central copy of guest identity to make discovery convenient.

- Operators begin with an existing Guest Record when one exists.
- Each owner exposes a rights-specific, permission-protected discovery surface
  over its own indexed data.
- Discovery accepts bounded criteria only through sensitive API/application
  requests and does not persist raw criteria in GMA task payloads.
- The case stores selected opaque record coordinates and stable subject links,
  not search text or broad response snapshots.
- Unlinked Reservations or Ingestion evidence must be explicitly selected or
  linked before destructive execution; fuzzy matches never authorize deletion.
- Candidate previews are non-cacheable, bounded and omitted from integration
  events, notifications, logs, metrics and completion receipts.

## Module Work Protocol

Versioned DataRights contracts contain only:

- case id, tenant/property scope and operation revision;
- target module and opaque module-owned record coordinate;
- requested operation and country-policy coordinates;
- idempotency key, correlation id and deadline;
- artifact handle or stable blocker/completion code where required.

An owner may complete, no-op, block or fail a work item. It must reject:

- another tenant or property;
- an unknown or superseded case revision;
- a stale/disabled country-policy decision;
- a record coordinate outside the selected subject set;
- erasure while an applicable hold or approved retention rule requires data;
- a duplicate request whose prior outcome cannot be proven equivalent.

Module events carry counts, coordinates and stable codes only. Export fragments
are streamed to protected file storage and referenced by opaque handles.

## Operation Semantics

### Access And Export

- Each owner renders only catalogue-approved fields and records the catalogue
  and policy versions used.
- The final manifest identifies module, schema version, period, omissions and
  stable exception codes without embedding legal advice.
- Artifacts are encrypted in transit and at rest through the deployment's file
  adapter, expire automatically, and require a fresh high-assurance permission
  check for every download.
- Export generation and download are separately audited.

### Correction

- The owning module validates and performs correction through normal domain
  commands.
- DataRights records the requested field policy key and owner receipt, not a
  generic patch document.
- Historical facts are amended or superseded according to owner semantics;
  immutable operational history is not silently rewritten.

### Restriction

- Each owner maintains and enforces its local restriction projection.
- Restriction blocks non-required processing while preserving data required for
  the case, legal hold or approved operational obligation.
- Reads and writes fail closed when restriction state is missing or ahead of a
  consumer's supported contract version.

### Erasure And Anonymisation

- Hard deletion is used only where the owner can preserve domain and referential
  correctness.
- Records that must remain for operational or legal reasons are irreversibly
  anonymised or reduced to approved non-identifying facts.
- Free text is redacted or removed through owner-specific logic; replacing only
  structured identity fields is not accepted as complete erasure.
- Provider credentials, staff audit attribution and hostel obligations are
  evaluated separately from guest data and cannot be erased by association.

## Ledger And Restore Safety

The authoritative ledger is append-only and contains no names, contacts, raw
payloads or exported values. An entry records:

- case and operation revision;
- tenant/property scope;
- owner module and opaque record pseudonym;
- disposition, policy coordinates and stable reason;
- completed time and module receipt digest;
- superseded/reapplied state where applicable.

Every owner consumes ledger events into a local tombstone projection. Rebuild,
restore, re-ingestion and adapter reprocessing must consult that projection
before personal data becomes readable or publishable.

A database restore can also roll back the in-database ledger, so the public
design must support an append-only ledger checkpoint/delta artifact stored
outside the restored database. Production readiness requires a restore gate
that imports every post-backup ledger delta before API and Worker readiness.
Private deployment supplies immutable storage and recovery procedures; public
tests use a local protected file adapter to prove the sequence.

## Authorization And Abuse Controls

Define scoped permissions for case read/create, sensitive discovery, decision,
execution, export generation, export download, restriction and destructive
completion.

- Ordinary operational roles receive none by default.
- Sensitive discovery and export require explicit property scope.
- Export download, erasure/anonymisation and tenant termination require the
  configured recent-authentication/MFA assurance.
- Stale membership, permission revocation or subject scope denies immediately.
- A workspace may require a second distinct approver for destructive actions;
  the domain records the policy and enforces actor separation when enabled.
- URLs, notifications and task/admin output never contain guest identity or
  artifact credentials.
- Every state transition uses optimistic concurrency and a stable idempotency
  key.

## Efficiency And Availability

- Discovery uses tenant-first indexes and bounded paging; never scan every
  tenant or load all matches into memory.
- Execute independent module work in bounded parallel batches while preserving
  owner-level ordering for one record.
- Stream export fragments and final archives; do not build them as one in-memory
  object.
- Keep task payloads small and PII-free. TaskRuntime history retention does not
  become a hidden copy of a rights request.
- Persist progress per module/work item so retries resume instead of restarting
  completed owners.
- Evaluate policy and hold state at approval and again immediately before a
  destructive owner operation.
- Emit bounded metrics by operation/status/module only; never tag tenant,
  subject, case, record or artifact identifiers.

## Delivery Slices

Develop only the one new `DataRights` module. Existing modules may be changed as
needed, one owner capability at a time.

1. Scaffold DataRights contracts/domain/application/persistence, permissions,
   case lifecycle, decision records and PII-free task model.
2. Deliver a complete Guests vertical slice: scoped discovery, catalogue-driven
   export, correction receipt, restriction, approved anonymisation, ledger and
   restore replay.
3. Add Reservations, including unlinked booking discovery and historical
   reservation/anonymisation semantics.
4. Add Ingestion raw/normalized evidence with legal-hold and retained-source
   blockers.
5. Add Inventory and Operations Notifications, then Staff/Workspaces where
   guest or tenant-termination data requires them.
6. Assemble protected export artifacts and the operator UI; add fresh-assurance
   download and destructive-action flows.
7. Add tenant-termination export, credential revocation, active-store deletion
   and external ledger-delta restore gating.
8. Run full migration, architecture, Docker, browser, security and exact-commit
   publication gates before any real guest data is allowed.

### Completed Slice: Guests Discovery And Subject Selection

The first internal increment of the Guests vertical slice is deliberately
synchronous and non-durable because its request and response contain sensitive
identity hints.

- `DataRights.Contracts` defines a bounded contributor contract shared with
  owner modules. It contains product rights vocabulary and therefore remains
  outside GMA.
- Discovery requires exactly one strong coordinate: an exact Guest Record id,
  normalized email or normalized phone. Name and date of birth may refine a
  strong coordinate but never authorize a name-only scan.
- A request is tenant- and property-scoped, capped at 20 candidates and
  deterministically ordered. It returns no total count.
- Guests searches only its own projection and includes profiles whose origin
  property or any historical stay matches the requested property. Unknown or
  unprojected properties fail closed.
- Candidate previews contain an opaque owner coordinate, record version and
  minimal masked contact hints. Raw criteria and candidate snapshots are never
  persisted, logged, published, measured or placed in task/outbox payloads.
- Discovery responses are non-cacheable. Selecting a candidate revalidates the
  coordinate with the owner before the case stores it.
- `DataRightsCase` owns a bounded collection of selected child coordinates,
  deduplicated by owner, record type and record id. Selection records the actor
  and time but no identity preview.
- Selected coordinates can be resumed through a separate non-cacheable,
  `Discover`-permission read surface; ordinary case readers receive only the
  selection count.
- Review cannot begin until at least one subject coordinate is selected.
- Select and unselect operations use optimistic concurrency and the existing
  property-scoped DataRights permissions.
- Tests cover strong-criteria validation, candidate bounds, tenant/property
  isolation, unknown-property denial, masked previews, selection revalidation,
  deduplication, selection bounds, no-store headers and review-without-subject
  denial.

### Completed Slice: Guests Catalogue-Driven Export Preparation

- `DataRights.Contracts` owns a neutral in-process contributor/sink contract.
  It contains product rights vocabulary and remains outside GMA.
- The export envelope is a subject-scoped, cross-module personal-data surface
  in the DataRights catalogue. It is transient for one hour and is never placed
  in tasks, inbox/outbox messages, notifications, logs, metrics or traces.
- Guests embeds its checked personal-data catalogue and validates all export
  bindings during composition. Missing, extra, non-exportable or wrongly
  retained bindings prevent startup.
- The contributor revalidates tenant, property, owner, record type and selected
  profile version before writing. Unknown scope, invisible profiles and stale
  coordinates write no records.
- A successful fragment contains one canonical guest profile and only stay
  history belonging to the authorized property. Staff audit attribution,
  search-index copies, projection ordinals, tenant routing identifiers and
  unrelated-property stays are excluded.
- Records are streamed through a caller-owned sink with bounded field counts
  and serialized values. A caller must discard every partial record when the
  contributor throws or returns anything except success.
- This increment does not persist fragments, run a case operation or expose an
  export/download API. Protected artifact assembly and fresh-assurance download
  remain later DataRights slices.
- Tests cover catalogue/schema versions, exact export fields, property
  isolation, stale and unavailable scope, and partial-sink failure.

### Completed Slice: Guests Correction Outcome Preparation

- Guest profile correction continues to use the aggregate's existing
  normalization, validation, optimistic-concurrency and domain-event path.
- The update path also returns a bounded semantic field set, previous/current
  record versions, event id and occurrence time. It contains no old or new
  personal values.
- Ordinary profile updates keep their existing contract and delegate to the
  same outcome-producing mutation, preventing correction semantics from
  drifting into a second patch implementation.
- This increment does not expose a correction endpoint, accept generic patch
  documents, persist a receipt or allow DataRights to mutate Guests. Execution
  remains closed until an approved case revision can be validated and an
  owner-local idempotent receipt can be committed with the profile update.

### Current Verification Evidence

- DataRights focused tests: 41 passed.
- Guests focused tests: 31 passed.
- Architecture tests: 64 passed.
- Integration tests: 29 passed.
- Fast repository verification: all composed projects built with zero warnings
  and all non-Docker tests passed.
- Migration drift verification passed for every composed PostgreSQL and SQL
  Server model.
- Scoped formatting verification passed for every C# file changed by this
  slice.
- PostgreSQL upgrade coverage starts at the initial DataRights migration,
  upgrades an existing case, round-trips a selected subject coordinate and
  proves cascade cleanup.
- PostgreSQL Guests coverage resolves the export contributor through module
  composition, streams its catalogue-approved profile and includes only stay
  history from the authorized property.
- PostgreSQL Guests correction coverage exercises the permission-protected API,
  exact approved revision, atomic profile/event/receipt commit, byte-stable
  receipt replay and conflicting idempotency-key rejection.
- Full Docker suite: 36 passed with no skips.

Restriction enforcement is implemented. Anonymisation is limited to a verified
approval and immutable prepared work item; owner mutation, anonymisation
receipts, ledger completion and restore replay are not yet implemented and must
not be inferred from this foundation.

### Completed Slice: DataRights Decision And Approval Gate

- Complete the explicit `ReviewRequired` to `DecisionPending` to `Approved` or
  `Denied` lifecycle with optimistic concurrency and bounded reason codes.
- Persist an immutable decision revision, actor and timestamp. The approval
  revision identifies the exact case scope accepted for execution even after
  later case-state changes.
- Expose a PII-free in-process Contracts gate for owner modules. Authorization
  requires an exact tenant, property, case, approval revision, single
  operation, restriction directive where applicable, owner, record type,
  record id and selected record version match.
- Keep correction execution closed. This slice neither changes a guest profile
  nor creates an owner receipt; it only establishes the prerequisite decision
  authority.

### Completed Slice: Guests Transactional Correction Receipt

- The property-scoped correction endpoint requires the DataRights execute
  permission and derives its actor from the authenticated subject.
- The Guests application revalidates country policy and the exact tenant,
  property, case, immutable approval revision, correction operation, owner,
  record type, Guest id and selected record version through the DataRights
  Contracts gate immediately before mutation.
- Corrections use the existing Guest aggregate normalization, validation,
  optimistic-concurrency and domain-event path. DataRights never writes the
  Guests schema.
- An owner-local, PII-free receipt is committed in the same Guests transaction
  as the profile update and outbox event. It binds the idempotency key, case,
  approval revision, property, Guest coordinate, semantic field set,
  prior/current profile versions, event id and completion time without storing
  old or new personal values.
- Equivalent retries return the previously committed receipt. Reusing the key
  with changed coordinates or normalized final values fails closed with a
  stable conflict.
- A command-specific one-retry persistence behavior resolves optimistic races
  and idempotency-key insert races without changing generic GMA behavior.
- The executable personal-data catalogue is version 2 and classifies every new
  request, command, response and persisted receipt member. The checked-in
  inventory is generated deterministically from that catalogue.
- PostgreSQL migration
  `20260724141436_AddGuestDataRightsCorrectionReceipts` creates the scoped
  receipt table, uniqueness/indexes and version/field-mask constraints.

The owner-local Guests restriction projection and dependent Reservations gate
are complete. Their detailed implementation and acceptance plan is
`guest-data-rights-restriction-task.md`.

The next increment implements Guests-owned destructive eligibility and holds.
Owner anonymisation, authoritative ledger entries and restore replay remain
closed until the prerequisites in
`guest-data-rights-anonymisation-and-ledger-task.md` are delivered in order.

## Acceptance Evidence

- Two-tenant/two-property tests prove no cross-scope discovery, export,
  restriction, correction or destructive mutation.
- A seeded subject traverses every implemented owner and produces a
  catalogue-versioned export with no unrelated records.
- Restriction immediately removes prohibited reads/processing while permitted
  obligations continue.
- Legal holds, retention decisions, stale policies and unresolved matches block
  erasure with stable codes.
- Duplicate and out-of-order work requests remain idempotent.
- A completed case removes, redacts or anonymises every approved active copy,
  search projection, notification payload and retained source covered by the
  selected scope.
- A restore test imports a newer ledger delta and reapplies tombstones before
  API/Worker readiness.
- Logs, traces, metrics, task rows, outbox/inbox messages and receipts contain no
  guest identity, export contents or artifact credentials.
- Architecture tests prove module ownership, contracts-only dependencies and no
  BunkFy rights vocabulary in GMA.

## Non-Goals

- Giving legal advice or choosing country-specific exception outcomes in code.
- Exposing a public guest self-service portal.
- Implementing SP-003 automatic retention schedules inside this module.
- Treating archive, unlinking, soft delete or encryption-key rotation alone as
  erasure.
- Building private complaint email delivery, immutable cloud storage, counsel
  approvals or hosted support routing in the public repository.

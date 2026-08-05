# Operations Notifications Data Rights Owner Capability Task

Status: complete; production activation remains blocked pending private
approval and deployment evidence
Date: 2026-07-31

## Goal

Make BunkFy's Operations Notifications extension a complete Data Rights owner
for product-linked durable inbox copies without writing directly to GMA
Notifications tables or moving hospitality and legal policy into GMA.

This domain consumes the generic indexed history lifecycle capability described
in GMA Notifications' `notification-history-lifecycle-task.md`.

## Ownership Boundary

- Source modules own their authoritative records and publish PII-minimized
  operational events.
- Operations Notifications owns BunkFy wording, typed navigation payloads,
  recipient policy, product reference keys, Data Rights adaptation, and the
  personal-data catalogue for addressed notification copies.
- GMA Notifications owns generic inbox persistence, delivery state, indexed
  opaque references, bounded history reads, lifecycle closure, and generic
  receipts.
- Data Rights owns case scope, selected coordinates, approval, dispatch,
  protected artifact assembly, canonical ledger, and restore orchestration.
- GMA Framework remains unchanged.

## Product Reference Keys

Operations Notifications derives a lowercase SHA-256 digest from a versioned
canonical tuple:

`bunkfy-notification-reference/v1 | tenant | property | property-id | owner | record-type | record-id`

The GMA reference namespace identifies the BunkFy owner and record family but
contains no personal value. The digest is the only coordinate stored in
Notifications. Raw email, phone, name, provider reference, free text, or
identity hints are never used as lifecycle keys.

Typed payloads declare their exact references when projected:

- reservation notifications reference the Reservations reservation;
- provider-attention notifications reference the authoritative Ingestion
  reservation source link and the Reservations reservation when one is
  present;
- every BunkFy-addressed copy references the recipient's stable Staff record;
  and
- every notification retains GMA's generic recipient reference.

Property, room, inventory-block, and workspace coordinates remain operational
account context. They are not silently treated as guest identity. Staff
and Ingestion source-link subject coverage are complete.

## Data Rights Coordinate

The extension exposes three exact coordinate families:

- property-scoped `reservation-history` keyed by the authoritative
  Reservations reservation id;
- property-scoped `ingestion-source-link-history` keyed by the authoritative
  Ingestion reservation source-link id; and
- tenant-scoped `staff-inbox-history` keyed by the authoritative Staff record
  id.

Each uses owner `operations-notifications` and the GMA lifecycle reference
version.

Required-companion expansion is owner-specific and bounded. It may add an
Operations Notifications coordinate only when an already selected authority
record deterministically resolves the exact product reference. No notification
content search is used to discover a subject. Access-export and anonymisation
reviews prepare an empty positive-version GMA reference when no copy exists
yet. That installs the exact lifecycle coordinate before approval, so a later
projection advances the version and makes the frozen decision stale instead of
escaping export or closure.

## Export

One selected reference streams the matching notification copies in stable
order:

- notification id and version;
- source module and notification name;
- title, body, severity, and occurred time;
- normalized typed navigation payload;
- immutable routing tags and delivery policy needed to explain the copy.

The contributor is scope exact, cursor based, capped at 1,000 records, and
fails before writing when the cap is exceeded or the selected version is stale.
It buffers and revalidates the full reference snapshot before writing, never
exports the staff recipient identifier, unrelated recipient history, or broad
broadcasts, and writes no partial fragment on a stale or oversized result.
Recipient read activity and mutable delivery-attempt state are intentionally
excluded: they describe staff interaction rather than the guest, and they can
change without changing immutable notification membership.

## Anonymisation

Operations Notifications participates as a companion to an approved authority
owner. It does not independently decide whether a Reservation, Ingestion, Staff,
or account-holder record may be erased.

Execution revalidates the frozen reference and calls GMA's generic close
operation with the Data Rights work-item id as the stable operation identity.
It maps the generic immutable receipt into a PII-free owner proof. A closed
reference removes the addressed copies, suppresses unfinished delivery work,
and prevents future replay of old source events from recreating them.
The 1,000-record close ceiling is part of owner receipt contract v1, so restore
replay uses the same request fingerprint even if later export policy changes.

Actively leased delivery work returns a retryable owner outcome. The extension
does not delete a row while a provider adapter may still be reading its
content.

## Restore

Data Rights restore replay invokes the same generic close operation before
readiness:

- a pre-close backup is closed again and receives a new equivalent GMA receipt;
- a post-close backup returns the existing receipt;
- conflicting operation fingerprints or reference state fail closed; and
- readiness advances only after the owner proof and canonical ledger agree.

No BunkFy code writes a Notifications tombstone, receipt, or history row
directly.

## Delivery Slices

1. Consume the GMA reference contract and emit version 3 notifications with
   exact reservation references.
2. Add reservation companion discovery, export, anonymisation, and restore
   adaptation.
3. Add Staff companion coverage through Staff-owned, bounded recipient
   correlation without exposing Auth internals. Completed.
4. Add Ingestion graph resolution for source-link-related receipt and
   connection notifications. Completed.
5. Align retention policy, catalogue, production admission, and operator
   workflow. Completed; exact deployment proof remains private rollout work.

Only Operations Notifications is the active product domain. Existing owner
modules may expose bounded contract readers needed to resolve their coordinates,
but their persistence remains private.

The completed implementation for delivery slice 3 is tracked in
[Operations Notifications Staff Data Rights](operations-notifications-staff-data-rights-task.md).
Staff is the post-provisioning authority for the Staff-to-account correlation;
Workspaces remains the owner of onboarding and access-process history.

The completed implementation for delivery slice 4 is tracked in
[Operations Notifications Ingestion Data Rights](operations-notifications-ingestion-data-rights-task.md).
Ingestion remains the authority for source-link identity and exact dispatch
correlation.

## Verification

- canonical reference hashing is deterministic, scoped, typed, and rejects
  invalid coordinates;
- unrelated tenants, properties, owners, record types, and recipients never
  match;
- companion expansion is bounded, idempotent, and fails closed when GMA
  lifecycle support is unavailable;
- empty access-export and anonymisation histories receive a positive frozen
  version so later notifications make the reviewed case stale;
- export is deterministic and does not partially write on overflow or stale
  state;
- close execution uses exact approval and work-item evidence and returns stable
  retry/conflict/completion outcomes;
- old source-event replay cannot recreate a closed subject-linked notification;
- restore replay closes a pre-operation PostgreSQL backup before readiness;
- executable catalogue, architecture, migration drift, focused extension,
  Data Rights, and integration tests pass;
- one coherent non-Docker repository gate and one exact PostgreSQL lifecycle
  scenario covering projection, delivery-lease conflict, close, replay, and
  concurrent late-event suppression pass before publication.

## Completion Evidence

The counts below record the original capability publication gate. Subsequent
tenant-termination and production-admission slices extend this owner and carry
their own current verification evidence.

- GMA Notifications `bd6c8af` is published with green Security Baseline,
  Ubuntu, Windows, and relational-provider validation.
- The synchronized 283-project backend graph builds with zero warnings and
  zero errors, and all PostgreSQL and SQL Server migration models are
  drift-free.
- Operations Notifications passes 68 tests, Ingestion passes 250 tests, Staff
  passes 147 tests, and Data Rights passes 261 tests; the privacy and
  module-boundary architecture suite passes 78 tests.
- The exact PostgreSQL lifecycle scenario passes projection, active-delivery
  conflict, retry, close, replay suppression, and deterministic close-versus-
  late-projection concurrency coverage.
- The executable catalogue contains 16 notification-surface fields, 13 Guest
  export fields, and 13 Staff export fields, with the closed output-sink guard
  enforcing the complete 42-field partition. Ingestion catalogue version 7
  also declares the contracts-only source-link projection result and its
  transient retention policy.

## Deferred

- provider-side deletion after an email, SMS, or push has been delivered;
- public account self-service export or deletion;
- hosted backup-expiry evidence for tenant termination;
- legal approval of notification retention periods;
- parsing or backfilling producer references from legacy arbitrary payload JSON;
- product-specific notification semantics in GMA; and
- a generalized cross-product data-rights framework.

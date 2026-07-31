# Operations Notifications Ingestion Data Rights Task

Status: implemented and verified
Date: 2026-07-31

## Goal

Make provider-operation attention notifications participate in the same Guest
Rights lifecycle as their authoritative Ingestion reservation source link.
Operations Notifications must resolve that relationship through a narrow
Ingestion contract, add an opaque indexed history reference before fan-out,
and support exact export, closure, replay suppression, and restore without
reading Ingestion persistence or moving hospitality policy into GMA.

This is delivery slice 4 of
[Operations Notifications Data Rights Owner Capability](operations-notifications-data-rights-owner-capability-task.md).

## Audit Findings

- The current provider-attention notification is produced from
  `ExternalReservationOperationCompletedIntegrationEvent`. Its payload carries
  property, receipt, connection, and optional reservation ids.
- Reservations owns the product operation outcome, while Ingestion owns the
  durable `ReservationSourceLink`, dispatch, receipt, proposal, reprocessing,
  and raw-evidence graph.
- A reservation id is absent for some failed create operations, so a
  reservation-history reference alone cannot cover every guest-linked provider
  notification.
- The exact Ingestion dispatch already correlates operation, receipt,
  connection, property, and source-link ids before Reservations publishes its
  outcome. That relationship can be resolved with one bounded indexed query.
- Source-link anonymisation preserves dispatch and source-link ids while
  reducing provider identifiers. Late outcome replay must therefore still
  resolve the source-link id and hit a closed notification reference.
- GMA Notifications already provides the required reference indexing, paging,
  close receipt, delivery-lease conflict, and replay suppression. No GMA
  change is justified.

## Ownership Boundary

- Ingestion owns source-link identity, the exact dispatch correlation, source
  evidence, anonymisation eligibility, and the narrow resolver contract.
- Reservations owns reservation operation outcomes and reservation state.
- Operations Notifications owns notification wording, typed navigation
  payloads, the source-link history reference, companion expansion, export,
  closure, and restore adaptation.
- Data Rights owns case scope, selected authority coordinates, approval,
  required-companion closure, protected artifacts, the canonical ledger, and
  restore order.
- GMA Notifications owns generic addressed-copy persistence and opaque
  reference lifecycle mechanics.

## Correlation Contract

Ingestion exposes one projection-style contract that accepts:

- normalized tenant id;
- property id;
- connection id;
- operation id; and
- receipt id.

It returns only the matching reservation source-link id. The implementation:

- performs one indexed, no-tracking join before notification audience fan-out;
- requires every supplied coordinate to match the same dispatch and source
  link;
- returns no source system, source reference, payload, guest data, or
  reservation snapshot;
- does not enumerate a connection or tenant; and
- deliberately resolves anonymised or tombstoned links so late notification
  replay is suppressed by the already closed GMA reference.

An expected provider-attention outcome with no exact correlation fails visibly
and retries. It is never projected without its source-link reference.

## Stable Source-Link Reference

The canonical coordinate is:

`bunkfy-notification-reference/v1 | normalized-tenant-id | property | property-id | ingestion | reservation-source-link | source-link-id`

GMA stores only the namespace and lowercase SHA-256 digest:

- namespace: `bunkfy-ingestion-source-link-history`
- owner: `operations-notifications`
- record type: `ingestion-source-link-history`
- record id: the authoritative Ingestion source-link id
- record version: the GMA lifecycle-reference version

The notification keeps its existing typed payload. The source-link id is not
added to user-visible JSON merely to support lifecycle indexing.

## Projection

The provider-operation attention handler resolves the source link once and
adds its reference to the internal notification request. Each addressed copy
then carries:

- GMA's generic recipient reference;
- the recipient's stable Staff history reference;
- the Ingestion source-link history reference; and
- the Reservations history reference when a reservation id exists.

Reference count remains small and independent of audience size. Closing any
one subject reference advances the other affected reference snapshots, making
concurrent reviewed cases stale instead of silently diverging.

## Required Companions

For a selected `ingestion/reservation-source-link` coordinate:

- access export ensures an open source-link notification reference and adds
  the frozen Operations Notifications coordinate;
- anonymisation does the same before review;
- duplicate expansion is idempotent and capacity bounded; and
- closed history adds no executable companion.

Ingestion remains the policy authority. Operations Notifications validates the
approved Guest Rights property scope and exact companion coordinate but does
not independently decide whether source evidence may be anonymised.

## Export

The existing property-scoped Operations Notifications export contributor is
extended to accept `ingestion-source-link-history`. It pages and buffers the
exact reference before writing and permits only the current closed provider
attention contract:

- Reservations source module;
- `provider-reservation-operation-needs-attention`;
- notification version 1; and
- the exact property, receipt, connection, and optional reservation payload
  shape.

The export retains the existing 13-field guest notification-copy schema.
Recipient identity, read state, delivery attempts, provider credentials, raw
payloads, and unrelated notification copies remain excluded. Oversized,
stale, unknown-contract, or malformed history writes no partial fragment.

## Anonymisation And Restore

The property-scoped Operations Notifications owner closes only the exact
source-link reference with the Data Rights work-item id as operation identity.
It uses the existing 1,000-record guest-history ceiling and a distinct
source-link removal reason.

Restore has a record-type-specific contributor but reuses the generic GMA close
receipt proof:

- pre-close backup state is closed again;
- post-close backup state replays the existing receipt;
- the resulting version and receipt hash must match the protected ledger; and
- missing, conflicting, busy, oversized, or unrelated state fails closed.

Neither execution nor restore depends on provider identifiers that Ingestion
has already anonymised.

## Catalogue And Architecture

- Ingestion catalogues the source-link resolver result as a transient
  cross-module projection of its existing pseudonymous source-link id.
- Operations Notifications records Ingestion as an approved source of opaque
  history references and source-link Guest Rights lifecycle processing.
- The extension references only `BunkFy.Modules.Ingestion.Contracts`.
- Architecture tests reject references to Ingestion Application, Persistence,
  Domain, or migrations.
- No database schema or GMA contract change is expected.

## Legacy History

Existing provider-attention copies do not contain the new source-link
reference. They cannot be safely backfilled from mutable payload JSON after
Ingestion provider identifiers are anonymised.

Before first production admission, pre-production notification history and
pending outcome replays must be drained or reset. After admission, exact
reference-presence tests and deployment evidence are mandatory.

## Verification

- exact, tenant-separated source-link correlation and reference hashing;
- one resolver query per relevant event, never per recipient;
- wrong tenant, property, connection, operation, or receipt returns no match;
- anonymised source links still resolve to the stable id;
- provider-attention projection fails rather than omitting the reference;
- source-link companion expansion is exact, idempotent, capacity bounded, and
  stale safe;
- deterministic export, strict notification/payload whitelist, overflow, and
  no-partial-output behavior;
- closure, delivery-lease retry, replay suppression, cross-reference staleness,
  and pre/post-close restore;
- executable catalogue, architecture boundary, host composition, solution
  graph, build, and migration-drift checks;
- focused checks while editing, one coherent non-Docker repository gate at
  slice end, and one exact PostgreSQL lifecycle scenario before publication.

## Completion Evidence

- Ingestion exposes only the stable source-link id through a contracts-only
  resolver backed by one exact, no-tracking dispatch/source-link query. The
  existing primary and scoped foreign keys cover the query, so no new index or
  migration was required.
- Provider-attention projection resolves the source link once before audience
  fan-out, fails visibly when exact correlation is missing, and is composed
  only by hosts that also compose Ingestion.
- Access-export and anonymisation companions, strict buffered export, exact
  closure, replay suppression, and record-specific restore cover the
  `ingestion-source-link-history` coordinate without reading Ingestion
  persistence from the extension.
- Operations Notifications passes 68 tests, Ingestion passes 250 tests, Data
  Rights passes 261 tests, and the architecture suite passes 78 tests.
- The complete solution builds with zero warnings and errors, every relational
  migration model is current, the repository non-Docker gate passes, and the
  exact PostgreSQL notification-history lifecycle scenario passes 1/1.
- Ingestion catalogue version 7 declares the transient projection result;
  Operations Notifications catalogue version 5 declares Ingestion as an
  approved opaque-reference source and source-link lifecycle purpose.

## Deferred

- new product notifications for every accepted receipt or connection-health
  transition;
- provider-side deletion after email, SMS, or push delivery;
- histories above the generic guest-history ceiling;
- legacy arbitrary-payload backfill;
- production retention activation and operator runbooks; and
- generic account-notification rights.

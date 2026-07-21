# Executable Personal-Data Catalogue Task

Status: in progress

## Goal

Complete company-readiness control SP-001 one domain at a time. BunkFy must have a versioned, machine-readable catalogue that makes undocumented personal data fail verification instead of silently entering persistence, contracts, messages, logs, or other processing surfaces.

The first delivery slice covered Guests because it is the canonical owner of durable guest profiles. Reservations now applies the same executable contract to booking records, change history, adapter ingress, and operational events.

## Ownership

- BunkFy owns product field definitions, hospitality purposes, access rules, country-policy keys, retention policy keys, rights behavior, and allowed processing boundaries.
- Each module owns the catalogue entries and bindings for data it authoritatively stores or deliberately transmits.
- A dependency-free BunkFy shared library owns only the catalogue document model, strict parser, validation rules, and deterministic inventory rendering.
- GMA continues to own generic transport, authorization, tenancy, tasks, and observability primitives. No hospitality fields or BunkFy retention decisions belong in GMA.
- A GMA extraction is considered only after at least two BunkFy modules prove a genuinely domain-neutral marker or redaction primitive.

## Catalogue Contract

Every field declaration must resolve all of the following metadata:

- stable field identifier, data class, and sensitivity;
- product purposes and accepted sources;
- authoritative module and controller/processor context;
- scope, reader permissions, and writer permissions;
- country-policy key and approval state;
- retention policy key, start event, end event or duration, and legal-hold behavior;
- export, correction, restriction, and erasure/anonymisation behavior;
- allowed processing surfaces and boundary crossings;
- concrete code bindings for persisted, indexed, accepted, returned, exported, or messaged members.

Unknown document properties, duplicate identifiers, unknown policy references, inconsistent bindings, empty policy values, and unapproved production policy states are invalid. Catalogue validation proves engineering completeness; it does not approve legal periods or country launches.

## Guests Slice

Status: implemented and verified

The Guests catalogue classifies:

- profile identity, contact, demographic, preference, notes, lifecycle, audit, and search data;
- linked property, reservation, and stay-history identifiers and operational dates;
- EF Core persistence and search copies;
- API/application commands, queries, responses, projection exports, and integration events;
- prohibited direct identity/contact/demographic/free-text use in operational events, notifications, logs, metrics, traces, and support bundles.

Executable guards must prove:

1. every mapped `GuestProfile` and `GuestStayHistoryEntry` member has a binding;
2. every public member in the selected Guests command, query, response, export, and event contracts has a binding;
3. every binding resolves to a real member and declares a surface allowed by its field policy;
4. direct identity, contact, demographic, preference, and free-text fields do not appear in Guests integration events;
5. the generated resolved inventory is deterministic and contains no unresolved policy references.

## Reservations Slice

Status: implemented and verified

The Reservations catalogue classifies:

- current and pending guest details, stay dates and times, lifecycle state, and booking-source provenance;
- reservation-to-Guest links, retained unlink snapshots, reminder state, and Inventory allocation projections;
- staff actor attribution, adapter connection/receipt/operation provenance, correlation keys, and request fingerprints;
- before/after details snapshots, changed-field payloads, revisions, and history persistence;
- commands, queries, API requests and responses, approved adapter-ingress contracts, internal domain events, and payload-minimized integration events.

Executable guards prove all selected EF Core members and public boundary members are classified, every binding resolves to a real member, adapter ingress is limited to the four explicit external-reservation request contracts, direct or unstructured guest data cannot enter operational outputs, rejection reasons remain bounded, legacy reminder JSON cannot reintroduce a guest name, and the checked-in inventory is deterministic.

## Delivery Slices

1. Add the shared catalogue model, strict parser, validator, and focused adversarial tests.
2. Add and verify the Guests v1 catalogue and deterministic resolved inventory.
3. Align Reservations with its existing PII-minimisation guard. Completed.
4. Continue one module at a time through Ingestion, Staff and Workspaces, Notifications extensions, Inventory, and Properties.
5. Add runtime ingress enforcement only where a catalogue policy can meaningfully reject unknown or prohibited fields; static internal contracts remain build-time guarded.
6. Add log, trace, metric, and notification test sinks after the relevant module catalogues exist.

## Non-Goals For The Guests Slice

- Choosing final legal bases, retention periods, country packs, or data-subject exceptions.
- Implementing SP-002 export/erasure workflows or SP-003 retention scheduling.
- Treating tests as production configuration approval.
- Moving BunkFy product semantics into GMA.
- Adding document, payment-card, biometric, health, or accessibility data. Those surfaces remain prohibited.

## Verification

- Shared parser tests cover unknown JSON properties, malformed documents, oversized input, duplicate identifiers, invalid policy references, invalid surfaces, and unapproved production activation.
- Guests tests cover EF Core mapped fields and all selected contract bindings by reflection.
- Architecture tests keep the shared library dependency-free and prevent module-boundary erosion.
- Solution synchronization, warning-free build, fast tests, and migration drift verification pass before publication.

Completed Guests evidence:

- 33 field definitions resolve 188 concrete code bindings across persistence, search, application, API, projection-export, and integration-event surfaces.
- Shared catalogue tests pass, including strict parsing, adversarial validation, canonical keys, production approval gates, and deterministic rendering.
- Guests reflection guards pass for mapped entities and selected public contracts.
- The complete repository verification passes with a warning-free build, all PostgreSQL and GMA migration models at zero drift, and the full non-Docker fast suite green.

Completed Reservations evidence:

- 71 field definitions resolve 645 concrete bindings across nine Reservations-owned persistence types and 55 selected boundary types.
- Adapter ingress is separated from ordinary integration events; internal domain-event snapshots are transient and remain intra-module.
- Direct identity, contact, free text, search input, and structured payloads are prohibited from integration events, notifications, logs, metrics, traces, and support bundles.
- Focused Reservations verification and the complete repository verifier pass with a synchronized solution, warning-free build, zero migration drift, and the full non-Docker fast suite green. Exact-commit CI remains required when the slice is published.

## Deferred Dependencies

- Founder/counsel approval of controller/processor allocation, field purposes, legal bases, retention periods, country rules, and rights exceptions.
- SP-002 rights orchestration across modules and deletion ledger.
- SP-003 automatic retention provisioning, monitoring, legal holds, and backup consequences.
- Production evidence and independent review for the completed product-wide control.

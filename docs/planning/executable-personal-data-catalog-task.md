# Executable Personal-Data Catalogue Task

Status: in progress

## Goal

Complete company-readiness control SP-001 one domain at a time. BunkFy must have a versioned, machine-readable catalogue that makes undocumented personal data fail verification instead of silently entering persistence, contracts, messages, logs, or other processing surfaces.

The first delivery slice covered Guests because it is the canonical owner of durable guest profiles. Reservations applies the same executable contract to booking records and operational events, and Ingestion now covers source evidence, normalized history, adapter credentials, operator audit, and adapter/parser boundaries.
Staff now applies the same control to employment profiles, assignment history, directory/sensitive response boundaries, onboarding contracts, and PII-minimized events.
Workspaces now applies the control to onboarding copies, account correlation,
access-process history, join-source responses, and the Organizations and Staff
events it consumes.
Operations Notifications now applies the control to addressed inbox envelopes,
typed navigation payloads, and authoritative active-member recipient filtering.

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

## Ingestion Slice

Status: implemented and verified

The Ingestion catalogue classifies raw source payloads, normalized reservation history, adapter configuration and credential material, operator/legal-hold audit, durable processing state, API/admin-facing models, adapter and parser ingress, and the minimal receipt-accepted integration event. Proposal list access remains operational, while proposal detail uses the separate `ingestion.sensitive-history.read` permission.

Executable guards prove every Ingestion-owned mapped persistence member and selected public command, query, port, API, task, adapter, parser, and event member is classified. Raw source evidence is limited to explicit adapter ingress, file ingress, application command, and API response surfaces. Direct, contact, free-text, search, and structured data cannot enter events, notifications, logs, metrics, traces, or support bundles. Persisted and remote failures expose stable bounded error codes instead of provider messages, raw downloads are non-cacheable opaque sandboxed attachments, and the resolved inventory is deterministic.

## Staff Slice

Status: implemented and fully locally verified; exact-commit CI pending

The Staff catalogue classifies employment identity and contact fields, Auth subject correlation, lifecycle and audit facts, current and historical property assignments, search copies, directory and sensitive profile responses, public/admin inputs, cross-module onboarding and identity reconciliation contracts, domain events, and integration events.

Executable guards cover every mapped `StaffMember` and `StaffPropertyAssignment` property and every selected command, query, API/admin contract, cross-module request, domain event, and integration event member. Directory DTO shapes are allow-listed so legal name, work contact details, employee number, Auth correlation, lifecycle timestamps, and assignment history cannot drift into `staff.read`. Direct identity, contact, free text, search input, and structured data cannot enter operational outputs, and the checked-in inventory is deterministic.

## Workspaces Slice

Status: implemented and locally verified; exact-commit CI pending

The Workspaces catalogue classifies copied onboarding profile data, verified
account email, Auth subject correlation, onboarding/source/claim lifecycle,
staff access-process and access-plan history, actor attribution, scoped profile
and property assignments, one-time join tokens, public/admin response shapes,
and the person-linked Organizations and Staff integration events consumed by
the module.

Executable guards cover every mapped Workspaces-owned onboarding, access
process, profile snapshot, access plan, and plan-property member. Selected
commands, queries, API inputs, API/admin outputs, and consumed event members are
reflection-checked. Direct identity, contact, free text, and structured payloads
cannot enter operational outputs, terminal onboarding states redact copied
applicant data, sensitive responses are non-cacheable, and the resolved
inventory is deterministic.

## Operations Notifications Slice

Status: implemented and locally verified; exact-commit CI pending

The Operations Notifications catalogue classifies the addressed notification
envelope and all typed navigation payload members. Payloads retain only resource
identifiers and bounded dates needed for navigation; guest identity, operator
free text, actor ids, provider errors, versions, and duplicate audit details are
not copied into recipient inboxes. Staff and workspace projections nominate
candidates, while Organizations authoritatively removes inactive access in
bounded batches immediately before projection.

Executable guards discover every payload type and member, require a closed
sealed payload set, prohibit string/object payload escape hatches and direct
identity/contact/free-text classifications, verify exact minimal JSON schemas,
exercise actor exclusion and stale-membership denial, and keep the generated
inventory deterministic.

## Delivery Slices

1. Add the shared catalogue model, strict parser, validator, and focused adversarial tests.
2. Add and verify the Guests v1 catalogue and deterministic resolved inventory.
3. Align Reservations with its existing PII-minimisation guard. Completed.
4. Continue one module at a time through Inventory and Properties. Operations
   Notifications is implemented and locally verified. Workspaces is published;
   Staff publication evidence remains to be reconciled.
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

Completed Ingestion evidence:

- 232 field definitions resolve 630 concrete bindings across Ingestion persistence, application/API contracts, adapter/parser ingress, raw evidence, credentials, and the single PII-minimized cross-module event.
- Raw evidence has a dedicated permission and constrained download response; normalized proposal history has a separate sensitive-history permission from ordinary operational reads.
- Stable ASCII error codes replace free-form remote and persisted run errors, with PostgreSQL lifecycle/format constraints and a compatibility migration for existing rows.
- Focused Ingestion, adapter, architecture, migration, frontend, complete repository, and exact-commit CI evidence must be green on publication.

Current Staff evidence:

- 44 field definitions resolve 366 concrete bindings across Staff persistence, search, application, public/admin API, cross-module, domain-event, and integration-event surfaces.
- Directory contracts are separate from sensitive profile contracts; ordinary persistence queries project only directory fields and current assignments.
- Focused Staff reflection, privacy-boundary, route-metadata, persistence, application, and contract tests pass. The synchronized solution, warning-free repository build, zero-drift migration checks, 2,177 non-Docker tests, 30 Docker tests, and the frontend typecheck, lint, 95 tests, production build, and generated-contract checks are green locally. Exact-commit CI remains required on publication.

Current Workspaces evidence:

- 64 field definitions resolve 235 concrete bindings across onboarding and
  access persistence, public/application boundaries, admin responses, and five
  consumed Organizations/Staff integration events.
- Reflection guards cover every selected EF and boundary member; terminal-state
  redaction tests and non-cacheable API/Admin API response tests pass.
- The synchronized warning-free build, all migration drift checks, all 2,312
  non-Docker tests, and all 33 Docker integration tests are green locally.
  Exact-commit CI passed for the published Workspaces slice.

Current Operations Notifications evidence:

- 15 field definitions resolve 22 concrete bindings across the addressed GMA
  envelope and seven sealed BunkFy navigation payload types.
- The generic Organizations candidate filter is published as `dec1732`; its
  exact validation and PostgreSQL workflows pass, including real revocation
  proof.
- All 21 focused extension tests, 58 architecture tests, and the Worker
  composition test pass locally. The synchronized warning-free build, every
  migration drift check, all 2,323 non-Docker tests, and all 33 Docker tests are
  green. Exact-commit BunkFy CI remains required on publication.

## Deferred Dependencies

- Founder/counsel approval of controller/processor allocation, field purposes, legal bases, retention periods, country rules, and rights exceptions.
- SP-002 rights orchestration across modules and deletion ledger.
- SP-003 automatic retention provisioning, monitoring, legal holds, and backup consequences.
- Provider-neutral object-storage inventory plus bounded orphan reconciliation for the crash window between evidence storage and receipt transaction commit.
- Production evidence and independent review for the completed product-wide control.

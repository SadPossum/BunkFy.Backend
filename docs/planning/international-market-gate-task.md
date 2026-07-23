# International Market Gate Task

Status: implementation and local acceptance proof complete; exact-commit CI pending

## Objective

Implement the public BunkFy portion of company-readiness control SP-010. A
property may be configured as operational topology, but no property-scoped guest
or reservation data may be accepted for it until an exact, currently valid
jurisdiction-policy binding is explicitly activated.

The implementation must fail closed. Time zone, locale, postal address, billing
country, workspace settings, adapter origin and guest nationality are context;
none of them may select or silently repair the property's jurisdiction.

Implemented in the core slice:

- strict, bounded, dependency-free country-pack parsing, validation, allowlisting
  and per-operation evaluation;
- explicit Properties-owned activation, suspension, immutable binding and
  revision history;
- event-driven local projections and fail-closed admission in Ingestion,
  Reservations and Guests;
- exact policy evidence on accepted Ingestion receipts;
- conservative migrations that preserve topology knowledge while leaving every
  existing processing binding unconfigured;
- empty public API and Worker defaults that enable no market.

Still pending in later slices are executable field/category rules, durable
rights and retention orchestration, export/deletion decisions, effective-state
product UI, denial signals and private counsel-approved pack content.

## Ownership

### BunkFy.DataGovernance

Own a dependency-free, strict and versioned country-pack schema, parser,
validator, immutable in-memory registry and deterministic decision engine. The
engine owns hospitality policy vocabulary and therefore remains in BunkFy.

It must not contain an enabled production country or legal conclusion by
default. Public examples are non-authoritative and disabled. A deployment
supplies the exact reviewed pack bytes and an allowlist containing the expected
policy id, immutable version and SHA-256 digest.

### Properties

Own the authoritative property processing lifecycle and current policy binding.
The aggregate records explicit operating country, policy id/version, data
region, transfer profile, retention policy id/version, acknowledgement set,
activation time and policy digest. Properties publishes versioned, PII-free
governance events and exports so other modules can build their own projections.

Property topology lifecycle and data-processing admission are separate:

- active topology may be edited before a market is enabled;
- unconfigured or suspended processing rejects personal-data operations;
- retired topology remains terminal;
- policy expiry or deployment allowlist removal denies new processing even if
  the persisted binding was previously activated.

### Consuming Modules

Ingestion, Reservations, Guests and later rights/retention workflows own local
property-policy projections. They combine the projected binding with the local
immutable registry and evaluate the operation at the point where their data is
accepted or released. They must not query Properties tables or call Properties
synchronously.

### GMA

No GMA change is currently required. GMA continues to own generic authorization,
messaging, task, time and configuration primitives. Country packs, hotel rules,
guest categories, BunkFy purposes and launch decisions must not move into GMA.
Only a reuse-proven generic gap discovered during implementation may be proposed
separately in its owning GMA repository.

### Workforce And Account Identity

Staff profiles are workspace-scoped and may be assigned across properties in
different countries. GMA account identity is broader still. Neither may infer a
workforce or account jurisdiction from one Property binding. Their controller,
workforce, residency and launch rules belong to the production authentication
and workforce/privacy review; this slice gates only property-scoped hospitality
processing.

### Private Deployment

The future hosted deployment owns counsel-approved pack content, the enabled
market allowlist, regional/subprocessor mappings, detached signatures, customer
notices and operational approval evidence. Missing private inputs leave the
public product disabled for real data.

## Country-Pack Contract

Each strict JSON document has a bounded size/depth and declares at least:

- schema version, policy id, immutable policy version and ISO 3166-1 alpha-2
  operating country;
- supported accommodation and guest categories;
- required, optional and prohibited field policy keys by guest category;
- purpose and legal-rule reference keys;
- retention trigger and period policy keys by data class;
- registration, export, correction, restriction and erasure rule keys;
- minors, document and special-category restrictions;
- permitted data regions and transfer profiles;
- required acknowledgement/notice identifiers and versions;
- policy owner/reviewer references, approval state, effective time and exclusive
  expiry/review time;
- bounded source references and optional detached-signature metadata.

Unknown JSON members, duplicate keys, unsupported schema versions, malformed
country codes, empty policy values, invalid intervals, duplicate policy
identities and internally inconsistent references are rejected. Production
resolution additionally rejects non-approved packs, digest mismatches, disabled
allowlist entries and packs outside their effective interval.

## Binding And Decision Contract

An activation request supplies every selector explicitly:

- `OperatingCountryCode`;
- `JurisdictionPolicyId` and `JurisdictionPolicyVersion`;
- `DataRegionId`;
- `TransferProfileId`;
- `RetentionPolicyId` and `RetentionPolicyVersion`;
- accepted acknowledgement ids/versions;
- expected property version.

The decision engine receives the binding, operation purpose, processing surface,
source provenance and observed UTC time. It returns either an immutable approved
decision or a denial with a stable reason code. Approved decisions include only
policy coordinates, digest, purpose/surface, approval/effective metadata and
evaluation time; they contain no guest or staff data.

Initial denial reasons include missing binding, unknown/disabled country,
unknown policy, version mismatch, digest mismatch, not-yet-effective policy,
expired policy, unapproved policy, unsupported accommodation type, disallowed
region/transfer/retention profile, missing acknowledgement, unsupported purpose,
unsupported surface and invalid source provenance.

## Properties Lifecycle

1. Existing and newly created properties begin with data processing
   `Unconfigured`; migration must not infer a country or auto-activate them.
2. An authorized workspace operator selects an exact configured pack and all
   binding coordinates.
3. The application resolves the request through the country-policy engine at a
   supplied UTC instant before invoking the aggregate.
4. The aggregate stores the approved binding and moves processing to `Enabled`,
   incrementing its concurrency version.
5. A durable Properties-owned revision record captures the previous and new
   policy coordinates, stable decision reason, actor coordinate and timestamp.
6. Material rebinding requires every acknowledgement required by the new pack;
   no implicit upgrade to a newer version is allowed.
7. Suspension is explicit and prevents new processing without erasing historical
   policy evidence. Retirement also disables processing.

Pack expiry or allowlist revocation is evaluated on every bounded decision. It
does not mutate the aggregate opportunistically; operators see the persisted
binding plus its current effective decision. This keeps reads deterministic and
ensures a revoked pack denies immediately in every process using the same
registry snapshot.

## Enforcement Slices

### Slice 1 - Engine And Properties

- Add country-pack schema/parser/validator/registry/decision tests.
- Add property processing state, binding value objects, revision history,
  activation/suspension commands and read contracts.
- Publish versioned integration events and topology exports with policy
  coordinates; never publish free-form legal advice or acknowledgement text.
- Add API endpoints for configured pack metadata, activation and suspension.
- Migrate existing properties to `Unconfigured` and prove no inferred values.

### Slice 2 - Ingestion

- Extend the Ingestion property projection with processing state and immutable
  policy coordinates.
- Deny connection creation/enabling, local and remote run start, lease grant,
  observation receipt, reprocessing and normalized dispatch unless a current
  decision permits the exact ingress purpose/surface/provenance.
- Persist the resolved policy coordinates and digest on accepted receipts so
  later review does not depend on mutable deployment configuration.
- Re-check policy at dispatch and reprocessing boundaries; revocation cannot be
  bypassed by an already-running adapter.

### Slice 3 - Reservations And Guests

- Build module-owned property-policy projections from the same versioned events
  and rebuild export.
- Gate direct/external reservation writes and guest-profile creation/update by
  purpose and surface. Field/category-level admission remains Slice 4 work.
- Preserve accepted policy coordinates on durable records that need historical
  proof without copying an entire country pack into module tables.

### Slice 4 - Rights, Retention And Output

- Make SP-002 rights decisions and SP-003 retention schedules resolve the same
  exact policy version rather than independent configuration values.
- Gate exports, correction, restriction, erasure and deletion exceptions through
  the same decision vocabulary.
- Reject unknown policy-field keys against the executable personal-data
  catalogues.

### Slice 5 - Product And Operations

- Expose clear `Unconfigured`, `Enabled`, `Suspended`, `Expired` and `Revoked`
  effective states without presenting legal advice as product copy.
- Require explicit confirmation and acknowledgements for activation/rebinding.
- Add startup/configuration evidence showing API and Worker processes use the
  same allowlisted digests. AdapterHost remains an untrusted client; server-side
  Ingestion admission is authoritative.
- Emit PII-free audit/security signals and bounded metrics for denials,
  expiries and configuration drift.

## Efficiency And Availability

- Parse and validate packs once into an immutable dictionary keyed by policy id
  and version; do not parse JSON or query a policy table per request.
- Resolve a property binding in constant time after the module-owned projection
  lookup. A bounded cache may key by binding coordinates, purpose, surface,
  provenance class and registry generation.
- Each process builds one immutable registry before serving work. Pack changes
  require a coordinated process restart; invalid startup configuration never
  installs a partially valid registry. Generation-based hot reload is deferred
  until deployment operations establish a concrete need for it.
- Denial is the safe behavior when a projection is missing, stale, unknown or
  ahead of a consumer's supported event version.
- Policy documents and decisions are small bounded metadata, not copies of guest
  records. Projections add fixed-width governance columns rather than JSON
  snapshots.

## Migrations And Compatibility

- Add nullable binding columns plus a non-null processing state whose migration
  default is `Unconfigured` for every existing property.
- Add database checks for valid state/binding combinations and bounded policy
  coordinates.
- Add a Properties-owned revision table with tenant/property foreign keys and
  append-only semantics.
- Version changed integration contracts; retain explicit compatibility handlers
  only where an in-flight old event can be safely interpreted as unconfigured.
- Projection rebuild exports carry the current binding and processing state.
- Rollback never activates a property or discards the revision ledger.

## Security And Abuse Cases

- A caller cannot activate a property by choosing a timezone or changing guest
  nationality.
- A stale client cannot overwrite a newer policy binding.
- A valid pack cannot be used under another country, region, transfer profile or
  retention profile.
- A copied public example cannot pass production approval or allowlist checks.
- Expired/revoked policy denies adapters that already hold credentials or leases.
- Missing/out-of-order projection events deny rather than falling back to the
  old `IsActive` flag.
- Error responses and telemetry expose stable coordinates/reason codes only, not
  legal advice, acknowledgement text or personal data.
- Cross-tenant policy activation, projection application and policy evidence are
  covered by adversarial tests.

## Acceptance Evidence

- Strict engine tests cover malformed, duplicate, oversized, unknown and
  contradictory documents plus every production denial reason.
- Property aggregate, command, API, persistence and migration tests prove the
  explicit lifecycle, concurrency, acknowledgement and history invariants.
- Ingestion tests prove every ingress/run/lease/reprocessing boundary fails
  closed for missing, expired, revoked and mismatched policy.
- Reservations and Guests tests prove direct and external writes cannot bypass
  the same decision contract.
- Architecture tests prove modules consume Properties only through contracts and
  own their projections; BunkFy policy types do not appear in GMA.
- Configuration tests prove public defaults enable no production country and
  production processes reject invalid or divergent pack digests.
- Rebuild, migration drift, warning-free build, complete non-Docker suite,
  PostgreSQL integration suite, frontend checks and exact-commit CI are green.

Local acceptance proof completed on 2026-07-23:

- synchronized 260-project solution and source-package ownership checks passed;
- the full solution built with zero warnings and zero errors;
- all 19 PostgreSQL/SQL Server migration drift checks passed;
- 2,413 non-Docker tests, including 63 architecture tests and 29
  repository-level integration tests, passed;
- all 33 Docker tests passed against real PostgreSQL and NATS services;
- the complete direct/transitive NuGet vulnerability audit reported no known
  vulnerable packages;
- frontend type checking, linting, all 109 tests, production build, generated
  contract drift check and production dependency audit passed;
- browser runtime proof covered explicit activation, suspension, denied guest
  creation while suspended, reactivation and successful guest creation;
- desktop and mobile property-governance layouts remained usable without
  horizontal overflow, and the supported `localhost` session survived a hard
  reload without console warnings or errors;
- every mounted GMA checkout used by the backend is clean and its current,
  recorded and `origin/dev` commit match.

Exact backend, frontend and root-composition CI remain publication gates; they
must be attached to the published commits rather than inferred from this local
proof.

## Non-Goals

- Choosing an initial launch country or inventing legal policy content.
- Encoding legal prose or regulator guidance directly in application branches.
- Storing private legal advice, production signatures, subprocessors or customer
  contracts in the public repository.
- Building SP-002 rights orchestration, SP-003 scheduling or private cloud
  deployment inside the engine slice.
- Treating policy activation as evidence that all production gates have passed.

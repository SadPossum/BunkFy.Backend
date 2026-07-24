# Guest Data Rights Restriction Task

Status: implementation in progress; DataRights approval-intent binding complete,
Guests owner projection not started

## Outcome

Implement the Guests owner capability for an approved processing restriction.
An authorized operator must be able to apply or release a restriction without
letting DataRights write the Guests schema, without turning archive into a
rights outcome, and without exposing restricted Guest Records through ordinary
operational surfaces.

The capability is not complete until it is fail closed, idempotent, attributable
and safe under retries, projection lag and concurrent cases.

## Boundary Decision

DataRights owns the case, decision and immutable approval scope.

Guests owns:

- the active restriction associated with one approved case revision;
- the effective property-and-Guest restriction projection;
- immutable owner transition receipts;
- enforcement on Guests-owned operational reads, writes and processing;
- the authoritative in-process restriction gate exposed through Guests
  Contracts;
- PII-free restriction events and projection-rebuild exports needed by
  dependent modules.

Reservations and later consumers own their local eligibility projections and
must use only versioned Guests contracts. They do not read Guests tables.

GMA is unchanged. Restriction directives, Guest coordinates, rights cases and
owner receipts are BunkFy product concepts. Existing GMA access control, CQRS,
messaging, outbox/inbox, projection rebuild, runtime and persistence primitives
are sufficient.

## Approval Intent

`DataRightsOperation.Restriction` alone is too ambiguous because it must not
authorize both applying and lifting a restriction.

Add a bounded DataRights restriction directive:

- `Apply`
- `Release`

The directive is required when a case requests Restriction and prohibited
otherwise. It becomes part of the persisted case request, immutable decision
scope and approval-gate request.

An apply command must present an approved `Apply` directive. A release command
must present a different currently approved `Release` directive and identify
the active owner restriction it intends to release. An operator cannot reuse an
apply approval to release state or reuse a release approval to create state.

Correction approvals continue to use no restriction directive.

## Owner Model

Use three small records rather than adding restriction state to `GuestProfile`.

### Approved Restriction

One Guests-owned aggregate represents one approved restriction:

- tenant and property scope;
- Guest Record id;
- apply case id, approval revision and selected Guest version;
- active or released state and local optimistic version;
- applied and released actor/time;
- release case id, approval revision and selected Guest version when released.

The same apply case revision cannot create the same restriction twice. Multiple
independent active cases may restrict the same Guest at the same property.
Releasing one must not lift another.

### Effective Projection

One projection per tenant, property and Guest stores:

- supported restriction contract version;
- monotonic projection revision;
- whether processing is currently restricted;
- last transition time.

The effective state is restricted while any approved owner restriction remains
active. Every apply or release increments the revision, including transitions
that leave the effective boolean unchanged because another case is still
active.

Projection absence, an unknown contract version or a version newer than the
consumer supports means restricted. It never means unrestricted.

New Guest Records initialize an unrestricted projection for their origin
property in the same transaction. Reservation stay events initialize the
projection before making a Guest operationally visible at another property.
The migration backfills origin-property and current-stay visibility before the
new filtering behavior is enabled.

### Immutable Receipt

Every successful apply or release writes an immutable receipt in the same
Guests transaction as the restriction and effective-projection change.

The receipt contains:

- receipt and idempotency ids;
- restriction id and action;
- case id and approval revision;
- property and Guest coordinates;
- selected Guest version and resulting restriction/projection revisions;
- effective restricted state;
- stable completion time and actor attribution.

It contains no name, contact value, requested correction value, free text or
legal advice.

Equivalent retries return the committed receipt. Reusing an idempotency key
with any changed coordinate, directive or expected version fails with a stable
conflict.

## Command Surfaces

Add permission-protected, typed customer API operations:

- apply an approved Guest restriction;
- release one active restriction using an approved release decision;
- list bounded active restrictions for one selected Guest.

Apply and release require the property-scoped DataRights Restrict permission,
an authenticated actor, exact optimistic versions and a current allowed country
policy. The application rechecks the exact tenant, property, case, immutable
approval revision, directive, owner, record type, Guest id and selected Guest
version through the DataRights approval gate immediately before mutation.

The routes publish explicit success response schemas. Their request and response
members are added to the executable Guests personal-data catalogue.

## Enforcement Matrix

The effective projection is enforced in database predicates, not by loading a
page and filtering it in memory.

Ordinary Guests operations while restricted:

| Surface | Behavior |
| --- | --- |
| Guest list | Exclude the Guest before paging |
| Guest detail | Return not found |
| Stay history | Return not found before reading stays |
| Ordinary profile update | Reject as not operationally visible |
| Archive | Reject as not operationally visible |
| Admin API and CLI | Same application behavior as customer API |
| DataRights discovery | Allowed for case management |
| DataRights export | Allowed for the approved rights workflow |
| Approved DataRights correction | Allowed and still exact-version checked |
| Restriction apply/release | Allowed only through the dedicated commands |
| Reservation stay projection maintenance | Continue for factual obligations |
| Rebuild/restore preparation | Preserve restriction state and fail closed |

Profile creation is not blocked by an unrelated existing coordinate. Duplicate
identity detection must not become a broad PII scan.

## Dependent-Module Contract

Guests Contracts exposes a versioned, PII-free restriction gate accepting only
tenant, property and Guest coordinates. It returns one of:

- allowed;
- restricted;
- unknown;
- unsupported contract version.

Only `allowed` permits optional processing. Unknown and unsupported responses
fail closed.

Guests also publishes a versioned transition event and a bounded projection
rebuild export containing Guest/property coordinates, contract version,
projection revision and effective boolean. No case id, actor, reason or
personal value crosses this boundary.

Reservations consumes these contracts into its own eligibility projection and
prevents a newly restricted Guest from being linked to a reservation. The
authoritative gate is rechecked at the command boundary so outbox lag cannot
create a fail-open window. Existing factual reservation and stay records are
not silently deleted or rewritten by this Guests slice.

## Security And Privacy

- Ordinary workspace roles receive no restriction permission by default.
- Tenant, property, owner and record coordinates are checked independently.
- Apply and release use different immutable approval directives.
- Stale Guest, case, approval, restriction or projection versions fail closed.
- Restricted Guests do not appear in counts, pages or search results.
- DataRights-only bypasses are explicit commands/contributors, not repository
  flags supplied by a caller.
- Events, task payloads, logs, metrics and receipts contain no Guest identity
  values or free text.
- Country-policy absence, digest mismatch, suspension or unsupported purpose
  denies both transitions.
- Multiple active restrictions are reference-safe: one release cannot clear
  another case.

## Efficiency

- Operational list/detail queries use indexed `EXISTS` predicates against the
  fixed-width effective projection.
- Active restriction lookup uses tenant/property/Guest and case-revision
  indexes.
- Idempotency keys and apply coordinates are unique in the database.
- No request scans all Guests, restrictions or cases.
- Projection events are constant-size and ordered by one monotonic revision per
  Guest/property.
- Retry is limited to the restriction commands and one optimistic/unique-race
  replay. Generic GMA command behavior is not changed.

## Delivery Steps

1. Bind Apply/Release intent into the DataRights case and approval gate.
2. Add Guests restriction aggregates, projection, receipts and repositories.
3. Add apply/release/list commands, typed API routes and permission metadata.
4. Enforce the effective projection on every ordinary Guests surface.
5. Add the versioned Guests gate, event and rebuild export, then update
   Reservations guest-link eligibility through contracts only.
6. Add PostgreSQL migrations, conservative backfill, indexes and constraints.
7. Advance the executable personal-data catalogue and deterministic inventory.
8. Run focused, architecture, migration-drift, Docker, generated-contract,
   browser where applicable and exact-commit GitHub gates.

## Acceptance Evidence

- Apply approval cannot release and release approval cannot apply.
- Wrong tenant, property, case revision, owner, record type, Guest id or Guest
  version cannot mutate state.
- The first active restriction removes the Guest from ordinary detail, stay and
  paged list results in the committing transaction.
- Ordinary update/archive and a new reservation link fail while restricted.
- DataRights discovery, export and approved correction still work.
- Two active cases remain restricted after only one is released.
- Equivalent apply/release retries return byte-equivalent receipts.
- Concurrent apply/release races converge without duplicate receipts or a
  fail-open projection.
- Missing, stale and future projection contracts deny optional processing.
- PostgreSQL upgrade tests prove existing visible Guests receive initialized
  projections before enforcement.
- Personal-data catalogue validation, architecture guards and payload tests
  prove no PII leaks into cross-module or infrastructure surfaces.

## Non-Goals

- Erasure, anonymisation, legal-hold disposition or retention scheduling.
- Rewriting existing Reservations history.
- Public Guest self-service.
- Generic restriction orchestration in GMA.
- Treating archive, unlinking or hiding a UI row as a rights completion.

# Properties Personal-Data Boundary Catalogue Task

Status: implementation and local production proof complete; exact-commit proof pending

## Goal

Make the Properties privacy boundary executable without misclassifying physical
topology as personal data. The slice must fail when a person-linked field is
added to Properties persistence or a selected boundary without an explicit
catalogue decision, while keeping ordinary property, room, and bed topology out
of the personal-data inventory.

This completes the module-level Properties part of SP-001. It does not approve
country policy, legal bases, retention periods, rights exceptions, or a hosted
production launch.

## Ownership Boundary

- Properties owns tenant-scoped property, room, bed, building, floor, status,
  version, lifecycle timestamp, and time-zone topology.
- Properties does not own guest identity, reservations, staff profiles,
  accounts, credentials, roles, or access grants.
- Property, room, building, floor, and bed labels are facility labels. Product
  guidance and guards must not turn them into guest/staff identity, notes, or
  arbitrary payload fields.
- Properties currently carries two related but distinct personal-data elements:
  the authenticated `AccessSubject` used transiently to resolve visible property
  scopes, and the initiating actor reference used when a property is retired.
  The latter flows through the application command, transient domain event, and
  versioned integration event so Operations Notifications can suppress a
  notification to the initiating user.
- Properties must not persist that actor reference or expose it through public,
  admin, projection-export, log, metric, trace, notification-payload, cache, or
  support-bundle surfaces.
- Non-notification consumers of `PropertyRetiredIntegrationEvent` must ignore
  `ActorId` and must not copy it into their topology projections.

These are BunkFy hospitality and product-policy decisions. No Properties field,
country key, retention value, or rights behavior belongs in GMA.

## Threat And Drift Cases

The executable guard must detect at least:

1. an actor, subject, guest, reservation, staff, account, contact, document,
   note, comment, free-text reason, message, or payload field entering a mapped
   Properties entity without classification;
2. a person-linked member entering selected public/admin requests, commands,
   queries, responses, exports, domain events, or integration events without a
   binding;
3. `Subject` or `ActorId` becoming durable Properties state or leaking into
   broad outputs;
4. a second Properties integration event carrying person attribution without an
   explicit catalogue revision;
5. unbounded or inconsistently bounded actor attribution causing a transaction
   or outbox projection failure; and
6. a checked-in inventory drifting from deterministic catalogue rendering.

## Catalogue Decision

Add `src/Modules/Properties/docs/personal-data-catalog.v1.json` with two current
field definitions:

- `properties.authorization-subject-reference`: an elevated pseudonymous
  account-holder coordinate sourced from the authenticated access context and
  used transiently to resolve the caller's granted property scopes; and

- `properties.staff-actor-reference`: standard-sensitivity audit attribution
  sourced from the authenticated access subject, used only for property
  lifecycle audit correlation and self-notification suppression.

The authorization subject binds exactly:

- `ListVisiblePropertiesQuery.Subject` as a transient application query.

The actor field binds exactly:

- `RetirePropertyCommand.ActorId` as a transient application command;
- `PropertyRetiredDomainEvent.ActorId` as a transient domain event; and
- `PropertyRetiredIntegrationEvent.ActorId` as a bounded integration-message
  journal member.

The engineering-default rights policy includes the actor reference in an
authorized staff audit export, corrects it through an appended corrective
action, restricts use to minimum audit/notification processing, and
pseudonymizes it when approved retention permits. Final policy values remain a
founder/counsel decision.

## Implementation

1. Add the strict v1 JSON catalogue and deterministic Markdown inventory.
2. Add a Properties module README that states the topology and privacy
   boundaries.
3. Add reflection-backed tests that resolve every binding and discover selected
   boundary types from their owning assemblies rather than maintaining a loose
   list of member names.
4. Inspect the EF Core model for `Property`, `Room`, and owned `Bed` members and
   require classification for any person-linked drift while explicitly proving
   current topology remains outside personal-data persistence.
5. Require the exact authorization and actor bindings and prohibit persistence,
   response, export, notification, observability, cache, and support-bundle
   bindings.
6. Keep actor limits aligned between domain, application validation, and the
   versioned integration contract. Reject oversized values before transaction
   work and retain constructor-level guards.
7. Link the task/module documents from the backend docs index, master SP-001
   plan, and synchronized solution.

## Verification

- focused Properties catalogue and module tests;
- Operations Notifications tests that consume the retirement actor;
- Workspaces, Staff, Guests, Reservations, Ingestion, and Inventory tests that
  consume Properties topology events where affected;
- architecture tests and solution synchronization;
- warning-free build and every PostgreSQL/GMA migration drift check;
- complete non-Docker and Docker suites;
- direct and transitive dependency vulnerability audit; and
- exact-commit Windows, Ubuntu, and Docker GitHub proof before publication.

Local production proof is complete:

- all 54 focused Properties tests pass;
- all 21 Operations Notifications tests and all 58 architecture tests pass;
- the synchronized solution builds with zero warnings and zero errors;
- every BunkFy PostgreSQL and mounted GMA PostgreSQL/SQL Server migration model
  has zero drift;
- all 2,343 non-Docker tests and all 33 Docker integration tests pass;
- the changed-file formatter and whitespace gates pass; and
- the direct and transitive package audit reports no known vulnerable packages.

The slice is not published until the exact backend commit passes Windows,
Ubuntu, and Docker GitHub workflows.

## Deferred

- SP-002 rights orchestration and deletion receipts across actor audit records.
- SP-003 approved retention execution and backup consequences.
- SP-010 operating-country and country-pack fields, which are facility policy
  rather than personal data but remain a separate production gate.
- A reusable typed access-subject coordinate if the cross-module actor-format
  and length audit proves the current repeated string convention needs a shared
  GMA primitive.
- Founder/counsel approval of controller/processor allocation, country keys,
  retention periods, rights exceptions, and launch policy.

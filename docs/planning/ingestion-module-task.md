# Ingestion Module Task

Status: first reservation ingestion workflow implemented
Date: 2026-07-12

Build Ingestion as the tenant- and property-scoped control plane for receiving external observations, normalizing them, and proposing or applying changes to BunkFy-owned product records. Source adapters may poll APIs, monitor email, parse websites, receive webhooks, or read files. Product modules must remain unaware of those mechanisms.

## Goal

The first complete slice should prove:

- local adapters run as live worker processes while using a boundary that can move out of process later;
- adapter connections, runs, cursors, receipts, raw payload references, retries, and health are durable;
- at-least-once adapter delivery is idempotent and out-of-order source revisions are rejected safely;
- normalized reservation observations create, cancel, or propose changes without direct database access;
- unchanged adapter-owned reservation details may update automatically;
- staff-edited reservation details are never silently overwritten;
- staff can inspect, accept, reject, or supersede proposed changes;
- accepted and automatic changes appear in Reservations-owned history with their origin.

## Ownership

Ingestion owns:

- adapter type and protocol-version metadata;
- host-composed adapter descriptors with configuration schema, supported execution modes, and optional polling guidance;
- tenant/property connection configuration and secret references;
- execution policy, run assignments, leases, cursors, checkpoints, health, and retry state;
- durable inbound receipts, source identity/revision, content hashes, and raw payload references;
- normalized observations, dispatch attempts, outcomes, and change proposals;
- staff decisions on proposals and links to the resulting product operation.

Reservations owns:

- reservation state and all applied booking-detail changes;
- optimistic lifecycle `Version` and a separate editable `DetailsRevision`;
- the origin of each applied details change;
- reservation change-history entries and final validation of expected revisions;
- Inventory reallocation protocols required by accepted date/unit changes.

Adapters own only source-specific acquisition and parsing. They never write Ingestion, Reservations, Inventory, or other product tables directly.

## Adapter Boundary

Create a small dependency-light `BunkFy.Adapter.Abstractions` project under `src/Shared`. It owns transport-neutral records and local runtime interfaces that can later move to GMA without carrying PMS rules.

The boundary should model:

- adapter descriptor and stable adapter type key;
- adapter protocol/config schema versions;
- execution modes such as polling, continuous daemon, and push/webhook;
- run assignment with connection, tenant/property scope, lease, and opaque checkpoint;
- observed record envelope with source record type, external id, optional source revision/time, content hash, and payload;
- durable receipt acknowledgement;
- run completion/failure and next checkpoint.

Local adapters implement the runtime interface and are selected by an explicit registry in a worker host. A future remote adapter uses HTTP or NATS endpoints that map the same wire records to the same Ingestion application commands. No adapter receives arbitrary tenant/property authority from its submitted payload; scope comes from its run assignment or authenticated connection.

## Execution Modes

- Polling adapters run as repeatable TaskRuntime tasks. This covers OTA APIs, mailbox polling, file drops, and scheduled web parsing.
- Continuous adapters may run as dedicated worker-host daemons when IMAP idle, streaming, or another long-lived protocol is justified.
- Push adapters submit webhook observations through an authenticated gateway.

All modes converge on the same durable observation receipt path. Ingestion does not encode how the source was reached.

## Durable Receipt And Cursor Rule

Delivery is at least once.

1. An adapter receives or resumes a run assignment.
2. It submits one or more observations with stable operation ids and source identities.
3. Ingestion stores receipt metadata and the raw payload reference atomically enough to acknowledge durable ownership.
4. The adapter may advance its source cursor only after that acknowledgement.
5. Normalization, proposal decisions, and product dispatch continue asynchronously from the durable receipt.

The source cursor must not depend on successful product application. A poison record must not block later source retrieval after Ingestion has durably accepted it.

Deduplicate primarily by `(connection, source record type, external id, source revision)` when the source provides a stable revision. Otherwise use a content hash plus observed cursor/sequence. Identical retries return the original receipt outcome.

## Reservation Change Authority

Do not use the aggregate lifecycle `Version` alone to detect staff edits. Allocation confirmation, cancellation compensation, and other system transitions legitimately change it.

Reservations adds:

- `DetailsRevision`, incremented only when editable reservation details change;
- `LastDetailsChangeOrigin`, initially `Staff`, `Adapter`, `Admin`, or `System`;
- an optional external operation id for idempotent adapter application;
- versioned details-change events carrying origin and correlation metadata;
- a Reservations-owned history projection suitable for operator timelines.

Ingestion stores, per external reservation link:

- last observed source revision/fingerprint;
- last successfully applied source revision;
- last applied reservation details revision;
- last normalized source snapshot/hash;
- last operation and outcome.

For an incoming change:

1. Ignore it as stale when its source revision is older than or equal to the last terminally handled revision, unless it is an explicit replay.
2. Auto-create when no linked reservation exists and the connection policy allows creation.
3. Auto-apply when policy allows it and the current `DetailsRevision` still equals the revision produced by the last accepted adapter operation.
4. Create a pending proposal when staff/admin changed details after the adapter baseline, no trusted baseline exists, or the final Reservations command rejects the expected revision because of a race.
5. Never infer availability or confirmation in Ingestion. Reservations and Inventory keep their existing authority.

The safe initial policy is `AutoApplyWhenAdapterBaselineUnchanged`. Connections may also use `SuggestionsOnly`. Broader field-level merge policies are deferred.

## Change Proposals

A proposal records:

- tenant, property, connection, receipt, reservation, and external source identity;
- source revision and normalized incoming snapshot;
- reservation details revision and snapshot used as the comparison base;
- a field-level diff for operator display;
- status and optimistic proposal version;
- automatic/staff decision reason, actor, timestamps, and resulting product operation id.

Initial statuses:

- `Pending`;
- `Applying`;
- `Applied`;
- `Rejected`;
- `Superseded`;
- `Stale`;
- `Failed`.

Accepting a proposal dispatches the normal Reservations-owned external amendment command with the current expected details revision. Mark the proposal `Applied` only after the correlated Reservations outcome. A further staff edit may make an accepted proposal stale before application; it must not overwrite that edit.

Rejecting a proposal requires a reason such as outdated source data, already handled locally, invalid mapping, or other. Repeated identical source input remains terminally handled; a newer source revision may create a new proposal.

## History

Keep two linked histories:

- Ingestion history explains what the source reported, how it was normalized, whether it was automatic or proposed, and the proposal decision.
- Reservations history explains what actually changed in the reservation, including origin, actor/adapter connection, before/after details revision, changed fields, and correlation id.

Ingestion may project reservation history facts for its UI, but it does not become the owner of applied reservation truth.

## Storage And Security

- Store raw payload bodies in MinIO through GMA Files; PostgreSQL stores hashes, metadata, and file references.
- Store secret references, never mailbox passwords, OTA credentials, cookies, API tokens, or raw secret values. Treat references as write-only: reads expose presence only, omission on update keeps the current reference, and clearing is explicit.
- Treat adapter payloads as untrusted input with bounded sizes, content validation, parser timeouts, and safe logging.
- Require tenant/property-scoped permissions for connection management, run control, raw payload access, proposal reads, and proposal decisions.
- Give remote adapters service identities scoped to assigned connections; adapter submissions cannot choose broader scopes.
- Define raw payload and normalized PII retention independently from permanent reservation history.

## First Slice

1. Add this task and the adapter-boundary architecture decision.
2. Add `BunkFy.Adapter.Abstractions` with protocol records, guards, and tests.
3. Scaffold Ingestion Contracts, Domain, Application, Persistence, PostgreSQL migrations, API, Admin API, Admin CLI, inbox/outbox, tests, and worker composition.
4. Implement connection, run, receipt, and proposal state machines.
5. Add Reservation source lookup, details revision/origin, history projection, and external create/cancel/change request outcomes. (implemented)
6. Implement a deterministic fake HTTP polling adapter and local worker bridge.
7. Prove durable receipt, restart recovery, deduplication, stale input, automatic application, staff conflict proposal, accept/reject, and race-to-stale behavior with PostgreSQL, MinIO, and JetStream.

## Current Checkpoint

Completed in the foundation checkpoint:

- adapter-boundary decision and dependency-light protocol SDK;
- module scaffolding and PostgreSQL initial migration;
- connection, task-linked source-run, receipt, and proposal domain state machines;
- scoped EF relationships, operation/source deduplication indexes, concurrency tokens, and granular permissions;
- focused protocol, domain, persistence-model, and architecture tests.

The follow-up foundation pass corrected execution ownership and added the first application receipt path:

- GMA TaskRuntime owns worker leases, retry, cancellation, timeout, and daemon lifecycle;
- Ingestion runs reference the GMA task run and attempt while retaining only source outcome, counters, and checkpoints;
- push receipts may use the same path without a worker run;
- raw payloads are stored through `Gma.Framework.FileManagement` using the host-selected MinIO/local adapter;
- operation and source-revision duplicates are acknowledged without rewriting payloads;
- assignment-bound sinks reject run/lease mismatches and advance checkpoints only after durable accepted/duplicate results;
- the default composition profile requires tenant context and concrete file storage.
- a GMA `RunAdapterTaskPayload` and task handler bridge create task-linked source executions, select explicit local runners, and record completion/failure without duplicating TaskRuntime ownership.

The deterministic adapter checkpoint now also provides:

- an ephemeral configuration-material contract whose secret buffers are cleared after every run;
- an Ingestion application resolver port over connection-owned opaque configuration and secret references;
- a strict local `configuration://` / `secret://` provider with adapter schema-version checks;
- a `fake.http` polling adapter outside product modules with bounded JSON responses, deterministic operation ids, replay-safe checkpoint handling, redirect denial, and optional authorization material;
- a `json.file-drop` polling adapter with host-owned connection inboxes, strict single-record envelopes, aggregate-bounded batches, deterministic replay identity, permanent-input quarantine, partial-run evidence, and acknowledgement-gated archival;
- focused tests for reference validation, schema mismatch, secret disposal, stable replay identity, and checkpoint failure.

Reservation `DetailsRevision`, latest provenance, durable before/after history, and safe management updates are now implemented. Lifecycle-only changes do not move the details revision, and management callers cannot claim adapter provenance.

Reservations also now owns versioned external create, guest-change, cancellation, and outcome contracts. Its durable operation ledger provides independent request fingerprinting, exact-replay behavior, and conflicting-operation rejection. Expected details revisions protect staff edits, while an accepted cancellation remains distinguishable from a completed cancellation.

The Ingestion half of that protocol now includes:

- strict `reservation.v1` canonical JSON normalization from bounded, hash-verified raw payload reads;
- a connection-qualified source namespace, so identical provider references from two adapter connections cannot collide in Reservations;
- durable reservation source links with source revision/sequence/timestamp ordering, accepted adapter baselines, active operations, and latest-receipt deferral;
- durable create, guest-change, and cancellation dispatches with deterministic operation ids and normalized snapshots;
- same-transaction receipt-accepted outbox facts, self-consumption, Reservations request publication, and correlated outcome consumption;
- non-terminal cancellation acceptance completed only by the later `ReservationCancelled` fact;
- automatic proposal creation for `SuggestionsOnly`, unverifiable source ordering, and Reservations details-revision conflicts.

The proposal decision checkpoint is also complete:

- accepting a proposal creates a distinct, deterministic proposal-triggered product attempt rather than reusing the receipt's observation dispatch;
- exact decision retries are idempotent, incompatible retries conflict, and a later staff revision makes the attempt stale instead of overwriting staff data;
- rejection is a local optimistic decision with an auditable actor and reason;
- successful outcomes advance the accepted adapter baseline, while accepted cancellation remains applying until Reservations publishes terminal cancellation;
- property-scoped proposal list/get/accept/reject surfaces are available in the authenticated API, Admin API, and confirmation-gated Admin CLI;
- API, Admin API, Admin CLI, and Worker hosts compose Ingestion, with MinIO-backed raw payload storage and explicit adapter/projection worker groups.

The PostgreSQL, MinIO, JetStream, Reservations, and restart-recovery path is covered by a focused Docker scenario. It proves durable push receipt and exact deduplication while the worker is offline, recovery into an automatically created and allocated reservation, automatic allocation-affecting amendment, atomic conflict rejection, fresh worker instances between stages, and conversion of a later staff-conflicting adapter revision into a pending proposal without overwriting the staff edit.

The allocation-amendment checkpoint is complete as an atomic Inventory reallocation, never a release followed by a new allocation. Inventory owns request/confirmed/rejected contracts, exclusion-aware conflict checks, active-allocation mutation, and a durable retry decision ledger. Reservations retains its current details while a candidate is pending, applies details/history only after confirmation, and clears the candidate without changing the booking after rejection. Ingestion classifies allocation-affecting observations against its last applied canonical snapshot and advances that baseline only after a confirmed outcome. Missing or corrupt historical baselines are classified conservatively as amendments.

Inbox-driven Reservations operations dispatch their aggregate domain events inside the module inbox transaction. This keeps allocation requests, cancellation requests, and details history atomic with the external operation ledger instead of relying on the CQRS unit-of-work used by management commands.

The operator-control checkpoint is also implemented:

- authenticated property-scoped API and Admin API/CLI surfaces create, inspect, configure, enable, and disable adapter connections without exposing secret material;
- adapter type and connection identity remain immutable, while execution mode, conflict policy, and opaque configuration/secret references are versioned settings for future runs;
- secret references are absent from connection read contracts and operator output; updates explicitly keep, replace, or clear them so routine edits cannot reveal or silently detach credentials;
- checkpoint reset is allowed only while disabled and requires explicit confirmation on admin surfaces;
- paged connection, source-run, and receipt views remain Ingestion-owned and never query another module's tables;
- Admin API/CLI enqueue, retry, and cancel the linked GMA TaskRuntime run after first resolving the Ingestion connection/run in the requested property scope;
- a live and rebuildable local Properties projection prevents connections for missing or retired properties and can recover missed property events through a one-shot projection task.
- API and Admin API expose hash-verified raw payloads only through the dedicated sensitive-data permission, force attachment delivery, and disable caching/content sniffing;
- Admin CLI raw-payload retrieval requires confirmation, uses atomic same-directory promotion, and refuses replacement unless overwrite is explicit.
- accepted receipts persist a configured raw-payload retention deadline, while terminal due receipts use concurrency-protected purge claims and idempotent object deletion;
- a dedicated tenant-scoped TaskRuntime job owns bounded purge batches, same-run retry, stale-claim recovery, and final receipt tombstones;
- Admin API/CLI enqueue purge work behind a separate retention permission and explicit confirmation; automatic per-tenant schedule provisioning waits for an authoritative tenant enumeration contract.
- property-scoped API, Admin API, and CLI health reads derive only from durable latest-run, last-success, observation, receipt-backlog, and retention-backlog facts;
- polling connections own versioned interval/retry schedules that a dynamic provider maps to tenant-scoped TaskRuntime occurrences; disable pauses emission while retaining cadence, and clear is explicit;
- one active Ingestion source run per connection is enforced in application flow and by a filtered PostgreSQL unique index;
- health exposes persisted cadence, next expected run time, and a due flag while keeping its operational state free of inferred provider or scheduler-health verdicts;
- API, Admin API, and CLI list provider-neutral adapter capabilities; connection create/update/start reject unknown or unsupported type/mode combinations before work is scheduled;
- descriptor providers are composed independently from local runners, and workers reject descriptor/runner version or capability drift before resolving configuration material;
- provider polling limits/recommendations validate the separately persisted connection schedule, while TaskRuntime continues to own occurrence deduplication, retry, lease, and worker execution.
- push connections remain direct-ingress only and are rejected by Admin task enqueue and task-linked run start.
- push connections can issue bounded, expiring, independently revocable ingress credentials; token material is returned once, digests and lifecycle are database-constrained, and a dedicated permission controls rotation;
- the direct HTTP ingress accepts only tenant/connection-bound `BunkFy-Adapter` credentials, rejects member JWT substitution, applies aggregate payload limits, and reuses durable receipt deduplication without exposing run/lease/checkpoint authority;
- real PostgreSQL/MinIO coverage proves issuance secrecy, wrong-tenant/connection denial, acceptance, exact replay, overlapping rotation, revocation, disabled-connection rejection, digest/slot constraints, and confirmation-gated Admin CLI revocation.
- dedicated legal-hold Admin API/CLI operations preserve independently releasable property audit records, while candidate SQL and an optimistic property fence stop held raw and normalized evidence from entering retention;
- health separates claimable, active-workflow-protected, legally-held, due, and redacted backlogs without exposing hold reasons;

The first real protocol connector is now implemented as `imap.reservation-json`: read-only MailKit acquisition, strict HMAC-authenticated reservation JSON attachments, password/OAuth transport authentication, TLS enforcement, UIDVALIDITY-aware checkpointing, separate trusted-unparsed and untrusted evidence, local/standalone composition, and real GreenMail SMTP/IMAP coverage. It deliberately proves an authenticated integration mailbox contract rather than claiming support for unstable vendor HTML templates or treating a spoofable sender address as authority.

Retained rejected evidence can now be reprocessed without mutating its source receipt. Ingestion owns versioned attempts, per-output audit, derived receipt lineage, deterministic replay identities, bounded raw-evidence reservations, retention fencing, scoped controls, and TaskRuntime orchestration. `BunkFy.Ingestion.Parsing.Abstractions` stays product-local; `BunkFy.Parsers.ReservationMail` proves executable host composition and strict RFC822-to-`reservation.v1` replay, including disabled source connections. Provider-specific OTA/template parsers and any later remote parser proxy plug into the same boundary.

The node-local file-drop lifecycle is bounded independently of Ingestion retention: adapter-stamped processed archives default to 7 days, strict failed raw/sidecar pairs default to 30 days, each category has an incremental deletion budget, and unsafe maintenance adds count-only run evidence without blocking source acquisition. Deployment can pause this local cleanup for external evidence preservation, but only a future centralized quarantine owner can provide audited holds or cross-node operations.

Federated workload identities, vendor-specific provider adapters, fleet-wide claim-any scheduling, and the next product-domain slice remain deferred.

## Deferred

- a Booking.com scraper, vendor-template mailbox parser, or production OTA connector beyond the implemented strict IMAP reservation-envelope adapter;
- arbitrary provider-specific mapping DSLs;
- field-level merge ownership and per-field authority policies;
- distributed remote adapter assignment, fleet scheduling, and autoscaling beyond the implemented single-connection standalone host;
- adapter marketplace/distribution and third-party code loading;
- broad reservation amendments beyond the first provider-proven workflow;
- generalized extraction into GMA before provider-specific adapters demonstrate which runtime and mapping concerns are genuinely reusable.
- automatic retention schedule provisioning across active tenants.

## Acceptance Checks

- product modules contain no provider-specific transport or parser types;
- local and remote adapters can use the same versioned observation protocol;
- adapter retries and worker restarts do not duplicate receipts or product effects;
- cursor advancement occurs only after durable receipt, not after downstream application;
- stale/out-of-order source records cannot overwrite newer state;
- system lifecycle version changes do not masquerade as staff edits;
- staff-edited reservation details produce a proposal instead of automatic overwrite;
- an edit racing proposal acceptance causes a stale/conflict result rather than data loss;
- every automatic or accepted change has linked Ingestion and Reservations history;
- tenant/property isolation, secret handling, raw payload access, and cross-scope denial are covered;
- module boundaries, migration drift, fast tests, and focused Docker scenarios pass.

# Ingestion Module

Status: first reservation ingestion workflow implemented

Ingestion is BunkFy's tenant- and property-scoped control plane for external source adapters, durable observations, normalization, and staff-reviewed change proposals.

The current foundation contains:

- the module project boundaries, PostgreSQL migration, inbox/outbox infrastructure, and optional API/admin front doors;
- scoped adapter connections with conflict policy, write-only secret references, configuration references, and checkpoints;
- task-linked source-run state, durable receipt identity, source deduplication, and staff proposal state machines;
- repository-backed observation receipt handling with raw payload storage through the selected GMA file adapter;
- assignment-bound adapter acknowledgement and checkpoint coordination;
- an opt-in GMA TaskRuntime handler bridge and explicit local adapter runner registry;
- execution-time resolution of opaque configuration/secret references into disposable adapter material;
- a provider-neutral adapter capability registry with protocol/schema versions, supported execution modes, and optional polling guidance;
- a hardened `json.file-drop` polling runner with connection-isolated pending/processed/failed areas, strict envelopes, deterministic replay identity, bounded batches, permanent-input quarantine, and post-acknowledgement archival;
- versioned connection-owned polling schedules projected into tenant-scoped GMA TaskRuntime occurrences;
- bounded raw-payload reads and strict `reservation.v1` normalization;
- durable reservation source links, source ordering, dispatch attempts, and versioned non-PII operational baselines;
- asynchronous create/change/amend/cancel requests with correlated Reservations outcomes and deferred receipt recovery;
- strict operational-baseline classification of guest-only changes versus allocation-affecting amendments;
- atomic Inventory amendment confirmation/rejection through Reservations, without release-then-create gaps;
- automatic change proposals for suggestions-only policy, unverifiable ordering, and staff revision conflicts;
- proposal-triggered accept attempts, optimistic rejection, idempotent retries, and race-to-stale handling;
- property-scoped proposal reads and decisions in the authenticated API, Admin API, and confirmation-gated Admin CLI;
- property-scoped connection lifecycle management with immutable adapter identity, versioned future-run settings, and explicit keep/replace/clear secret-reference updates;
- connection create/update/start validation against composed adapter descriptors, plus worker-side descriptor/runner drift rejection;
- factual connection health derived from latest durable run outcome, last success/observation, receipt backlog, and retention backlog;
- paged connection, run, and receipt operations views in the authenticated API, Admin API, and Admin CLI;
- property-scoped, hash-verified raw-payload downloads behind the separate sensitive-data permission;
- durable raw-payload retention deadlines and optimistic two-phase purge ownership across PostgreSQL and object storage;
- independent 90-day terminal normalized-history deadlines, reason-preserving proposal/dispatch redaction, and factual claimable/protected/due/redacted health counts;
- independently releasable property legal holds with audited actors/reasons, retention-query exclusion, and optimistic fencing against concurrent purge/redaction;
- TaskRuntime-backed run enqueue/retry/cancel orchestration in Admin API and Admin CLI;
- safe checkpoint reset for disabled connections and a rebuildable local active-property projection;
- separate scoped permissions for ordinary reads, connection management, run control, raw payload access, and proposal decisions.
- versioned parser capability discovery plus retained-source reprocessing with immutable source receipts, derived lineage, per-output audit, bounded evidence reservations, and TaskRuntime execution;

GMA TaskRuntime owns enqueue state, worker leases, retries, cancellation, timeout, and daemon lifecycle. Ingestion records the linked task run/attempt and source-specific outcome rather than implementing a parallel scheduler. Admin orchestration resolves an Ingestion-owned connection or run first, then delegates execution control to TaskRuntime.

The deterministic `fake.http` adapter, hardened `json.file-drop` adapter, strict `imap.reservation-json` mailbox adapter, and local configuration-material provider exercise three polling mechanisms from `src/Adapters`. IMAP uses MailKit behind an adapter-local session boundary, password or OAuth 2 authentication, mandatory TLS outside explicit loopback development, HMAC-authenticated attachment bytes, read-only bounded MIME retrieval, and UIDVALIDITY-aware checkpoints. Signed malformed/future mail becomes replayable retained evidence, while unsigned or incorrectly signed mail is retained as non-reprocessable untrusted evidence; neither blocks later UIDs. `BunkFy.Parsers.ReservationMail` can replay only trusted-unparsed evidence through the same strict envelope reader; parser metadata is present in API/Admin hosts and executable code is Worker-only. The dependency-light `BunkFy.Adapters.Http` client exercises the authenticated external push boundary against the same shared observation contract without referencing Ingestion internals. `BunkFy.Adapter.Runtime` and the executable `BunkFy.AdapterHost` run polling contracts independently with pre-acknowledgement local checkpoint durability, reloadable token/material sources, bounded retry, and non-sensitive local status. Ingestion is composed into API, Admin API, Admin CLI, and Worker hosts. MinIO stores JSON and RFC822 raw payloads, and the worker opts into explicit adapter, parser, and projection groups. Vendor-specific OTA/mail/HTML parsers, horizontally scaled remote assignment, and federated workload identities remain deferred.

Descriptor registration is separate from runner registration: control-plane hosts need capability metadata but must not load executable adapter runners. The current registry is host-composed and immutable for the process lifetime. A future remote adapter discovery implementation can replace `IAdapterDescriptorRegistry`; until then, deployments must keep descriptor registrations aligned across API, Admin, CLI, and Worker binaries. A worker rejects a runner whose descriptor differs from its registration.

Polling minimum/recommended intervals remain provider capability metadata, while each polling connection may separately own an explicit interval and retry limit. Ingestion persists that desired schedule and exposes it through a dynamic GMA `ITaskScheduleProvider`; TaskRuntime owns occurrence deduplication, leases, retries, and multi-worker execution. The trusted schedule reader crosses tenant query filters only to project enabled connection ids, tenant ids, cadence, and retry limits into tenant-scoped tasks. It does not expose adapter configuration or secret references.

Disabling a connection pauses schedule emission without deleting its configuration; re-enabling resumes it. Clearing is an explicit optimistic operation, and a schedule must be cleared before changing away from polling mode. Ingestion permits only one active source run per connection, guarded both before start and by a filtered PostgreSQL unique index so concurrent scheduler/manual starts fail closed.

Push is an ingress mode, not a task-run mode. Admin enqueue and task start reject push connections before creating Ingestion run state. The authenticated adapter-ingress endpoint feeds the same durable receipt path directly and accepts only `BunkFy-Adapter` credentials bound to the route tenant and push connection; staff JWTs are not adapter authority.

Ingress credentials are Ingestion-owned rotation records, not provider credentials and not general login accounts. A credential token contains 256 bits of random secret material, is returned once with `Cache-Control: no-store`, and is persisted only as a versioned SHA-256 digest. Credentials have bounded expiry, independent revocation, coarse last-authentication evidence, and five race-safe active slots per connection. PostgreSQL constrains digest shape, lifecycle completeness, expiry ordering, tenant-qualified connection ownership, and active-slot uniqueness. Management API, Admin API, and Admin CLI use the separate `ingestion.credentials.manage` permission; Admin revocation requires confirmation.

Direct submissions are bounded to 100 records, 16 MiB of decoded payload, and a 24 MiB HTTP body. Authentication happens before batch validation, then every record uses the existing operation/source deduplication and raw-payload path. A replay receives durable `Duplicate` results. Disabling the connection makes otherwise valid submissions receive per-record rejection. Direct ingress never accepts a run id, lease id, or checkpoint.

Source-specific adapters do not belong in this module. Local and future remote adapters use `BunkFy.Adapter.Abstractions`; Ingestion turns their observations into durable receipts and normalized product operations.

HTTP downloads are forced to opaque attachments with cache prevention and content sniffing disabled. The Admin CLI requires `--yes`, writes through a same-directory temporary file, and does not replace an existing file unless `--overwrite` is also supplied.

Raw payloads default to a 30-day retention period configured by `Ingestion:Retention:RawPayloadRetention` (valid range: one hour through ten years). Each receipt stores the deadline assigned when it is accepted, so later configuration changes do not silently rewrite existing retention obligations. Only processed or rejected receipts can be claimed; pending/applying proposals and non-expired reprocessing reservations keep source evidence out of the claim query. Reprocessing reservations expire automatically and are not substitutes for legal holds. The `purge-expired-raw-payloads` TaskRuntime job uses a durable claim before deleting MinIO content and finalizes the receipt afterward; the same task retry can resume immediately, another task can recover a stale claim, and an already-missing object is successful idempotent deletion. Admin API and CLI enqueue this tenant-scoped job behind `ingestion.retention.manage` and explicit confirmation.

Normalized proposal diffs and reservation dispatch snapshots have an independent terminal retention policy configured by `Ingestion:Retention:SensitiveHistoryRetention`, defaulting to 90 days with the same bounded range. Pending/applying proposals, pending dispatches, and accepted cancellations have no deadline and keep their evidence. Every genuine terminal transition persists its deadline; `redact-expired-reservation-history` later nulls only the sensitive body and retains reason code, state, actor, decision reason, correlation, revisions, errors, and lifecycle timestamps. Proposal list responses never include diff bodies. Detail reads state whether sensitive history is available or redacted.

Automatic schedule provisioning is intentionally not global: the current platform has no authoritative tenant-enumeration contract. Deployments must enqueue or schedule one scoped occurrence per tenant until that contract exists. Source links retain only arrival, departure, and sorted inventory-unit identity in strict versioned JSON.

Property legal holds are separate permanent audit records protected by `ingestion.legal-holds.manage`. Any active hold excludes the property's otherwise-eligible raw payloads and normalized history in the candidate SQL itself. Overlapping holds remain effective until the final release. Hold changes and retention batches share an optimistic property fence, while placement fails if an external raw-payload deletion is already in progress. Health reports active-hold and held-backlog counts but never reasons. The adapter-owned `json.file-drop` quarantine is outside these holds because malformed node-local input may not have a trustworthy property identity; it uses a separate bounded local retention policy, while export and audited quarantine holds remain deferred.

Connection health keeps its operational state factual: `NoActivity`, `RunActive`, each latest terminal run outcome, `ObservationsReceived`, and `Disabled` come from Ingestion-owned records. For enabled scheduled polling connections it also reports the configured cadence, an immediately due first run after configuration, later expected starts derived from the latest run start, and whether that time is due. `RunExpected` is timing evidence for operators, not an invented unhealthy verdict or proof that a scheduler instance is alive; quiet push connections remain cadence-free.

Health also reports whether the current host knows the connection's adapter descriptor and still supports its execution mode, together with protocol/configuration schema versions. This detects deployment composition drift without claiming that a registered adapter is operationally healthy.

Focused Docker coverage proves PostgreSQL, MinIO, JetStream, exact deduplication, worker-downtime recovery, automatic reservation creation, accepted and rejected allocation amendments, fresh worker restarts, the staff-conflict proposal path, real-token property-scoped connection management and health, separately authorized raw-payload retrieval, retention migration backfill, active-proposal evidence protection, legacy PII baseline reduction, normalized-history redaction and constraints, overlapping legal holds and fence conflicts, physical object purge, one-time adapter credential rotation, tenant/connection denial, direct push acceptance/replay, standalone runner delivery plus checkpointing, revocation, queued JSON file-drop receipt/archive/quarantine behavior, real SMTP-to-IMAP reservation acquisition and poison-message progress through GreenMail, and Admin API/CLI confirmation. Vendor-specific connectors, federated workload identity, distributed remote leases, and broader operational workflows remain later slices.

Retained rejected evidence can be parsed again without reopening the source receipt; the durable attempt, lineage, and retention-fence contract is in [Ingestion Source Reprocessing Task](../../../docs/planning/ingestion-source-reprocessing-task.md).

See [Ingestion Module Task](../../../docs/planning/ingestion-module-task.md) and [Ingestion Adapter Boundary](../../../docs/architecture/ingestion-adapter-boundary.md).

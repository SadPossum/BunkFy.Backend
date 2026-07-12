# Ingestion Adapter Boundary

Status: accepted for first implementation
Date: 2026-07-12

## Context

BunkFy must ingest reservation and operational data from sources with very different execution models: OTA APIs, email polling, website parsing, files, webhooks, and long-running daemons. Local adapters are sufficient initially, but the boundary must allow adapters to become independently deployed services without moving product rules into those services.

External data also cannot blindly overwrite staff decisions. Reservation lifecycle versions include system transitions, so they cannot by themselves identify whether editable booking details changed after an adapter baseline.

## Decision

Use three explicit layers:

1. `BunkFy.Adapter.Abstractions` defines a transport-neutral adapter/run/observation protocol and optional local runtime interface. It contains no PMS or provider-specific rules.
2. Ingestion owns adapter control-plane state, durable receipts, normalization, proposals, and dispatch orchestration.
3. Product modules own normalized command validation and all applied business state/history.

```text
email / OTA / website / file / webhook
                  |
          source-specific adapter
                  |
       versioned observation envelope
                  |
       Ingestion durable receipt + raw file
                  |
       normalize / dedupe / policy decision
             /                    \
      auto dispatch          change proposal
             \                    /
          Reservations-owned command
                  |
        Inventory-owned allocation decision
```

Local adapters invoke the same Ingestion application command used by future HTTP/NATS gateways. They do not receive repositories or DbContexts from product modules.

The local runner protocol is now exercised by three materially different acquisition mechanisms. `fake.http` polls a bounded remote JSON page and advances a source cursor; `json.file-drop` scans a host-owned connection inbox and archives immutable single-record envelopes only after exact durable acknowledgement; `imap.reservation-json` authenticates to a read-only mailbox and advances a UIDVALIDITY/UID checkpoint after bounded MIME evidence is durable. File-drop operation identity includes the connection, filename, source identity/revision, and canonical payload hash, so a crash between receipt and archive safely replays as a duplicate. The file-drop adapter ignores checkpoint values for file discovery and treats pending files as the source of truth, while still publishing the last acknowledged filename as an operational checkpoint. IMAP operation identity additionally binds the mailbox UID generation and UID, while transient sequence indexes are used only to locate the next UID in the currently open folder.

File-drop distinguishes permanent source-format failures from retryable infrastructure failures. Permanently invalid files have no trustworthy observation identity, so they do not create fake rejected receipts. They move to a connection-local failed area with non-PII reason metadata, valid siblings continue, and the Ingestion source run records a partial outcome. Sharing/permission failures, directory links, changed-in-flight content, acknowledgement mismatch, checkpoint rejection, and archive failures move nothing. Central quarantine operations remain a later multi-worker concern because Ingestion cannot safely pretend a host-local path is globally addressable.

Adapter capability discovery is independent from adapter execution. Provider packages expose `IAdapterDescriptorProvider` registrations to every host; only worker hosts register `IAdapterRunner`. Ingestion validates connection create, update, and run start against the descriptor registry, and a local worker rejects protocol, configuration-schema, execution-mode, or polling-capability drift between the registered descriptor and runner. The registry is an application boundary so a later remote discovery/catalog implementation can replace host-local registrations without changing connection commands.

Descriptors may declare minimum and recommended polling intervals. These are provider capability/advice, not a schedule by themselves. A polling connection owns its versioned interval and retry limit as desired state; Ingestion's dynamic GMA schedule provider maps enabled schedules to tenant-scoped TaskRuntime occurrences. The provider uses a narrowly projected trusted query across tenant filters because the scheduler has no tenant context, but emits only scope id, connection id, cadence, and retry metadata. This does not create a general tenant-enumeration contract.

Connection health includes descriptor availability, execution-mode compatibility, and protocol/configuration schema versions as composition evidence. These fields diagnose deployment drift but do not replace durable run and backlog health facts.

Execution mode also determines ingress ownership. Polling and continuous connections may create task-linked runs; push connections submit through the authenticated observation gateway and are rejected by task enqueue and run-start paths.

Connections persist opaque configuration and secret references only. Secret references are write-only at every operator boundary: connection reads expose only `HasSecretReference`, never the reference value. Updates distinguish keep, replace, and clear; omitting secret fields keeps the existing reference, while clearing requires an explicit flag. At execution time an `IAdapterConfigurationMaterialResolver` resolves those references for the authoritative connection scope into a bounded, disposable `AdapterConfigurationMaterial`. The task bridge passes it directly to the selected runner and disposes it after success, failure, or cancellation. The material is not a record, does not render values through `ToString()`, and clears its byte buffers when disposed. Local configuration is one resolver implementation; production secret stores can replace it without changing adapters or connection state.

Raw observation payload retrieval is a separate sensitive-data operation, not part of ordinary receipt reads. Ingestion resolves the receipt inside the requested property and active tenant scope, derives the object key from trusted receipt identity, and verifies the stored SHA-256 against receipt metadata before returning bytes. HTTP responses are opaque attachments with `no-store`, `nosniff`, sandbox, and same-origin resource headers. The Admin CLI requires explicit confirmation and never overwrites an existing destination implicitly.

Retention cannot atomically delete an object and update PostgreSQL. Ingestion therefore stores the deadline and an optimistic purge state on each receipt. A scoped TaskRuntime job first claims a bounded terminal/due batch in PostgreSQL, deletes each object idempotently, then records the tombstone. Stable task-run claim identity closes the retry window; stale claims can be recovered by a later run. Payload download refuses `Purging` and returns a permanent unavailable result after `Purged`. Existing receipts are migrated as available with a conservative deadline of `ReceivedAtUtc + 30 days`.

Operational health is a read model over Ingestion-owned evidence, not a mutable verdict copied from an adapter. It reports connection enablement, latest run outcome/error, last successful run, last received observation, receipt backlog, retention backlog, and persisted polling cadence. For an enabled scheduled connection, `NextRunExpectedAtUtc` is the configuration time until its `runOnStart` occurrence begins, then the latest run start plus the interval; `RunExpected` says only that this timestamp is due. The operational state itself does not infer provider health, scheduler liveness, or push cadence from elapsed time; richer SLO verdicts remain a future versioned capability.

Local BunkFy-owned `Polling` execution uses GMA TaskRuntime for leases, retries, cancellation, timeout, and worker ownership. Ingestion stores a linked source-execution record for adapter counters, checkpoint facts, and source outcome; it does not duplicate that task lease state machine. Independently deployed `RemotePolling` daemons use Ingestion-owned connection leases because their source checkpoint is Ingestion authority. Push adapters enter the durable receipt path without requiring a run.

Remote push authentication is connection-owned. Staff/member JWTs and management permissions cannot substitute for an adapter credential. Ingestion issues a one-time high-entropy `BunkFy-Adapter` token, stores only its versioned digest, and verifies it inside the tenant context against exactly one push connection. Multiple bounded slots permit overlap during rotation; filtered uniqueness makes the active cap race-safe while revoked and expired records remain as operational history. Invalid, malformed, expired, revoked, cross-tenant, cross-connection, and non-push credentials share the same unauthorized response.

External push processes use the dependency-light `BunkFy.Adapters.Http` client over the shared transport envelope. The client validates batches before secret acquisition, requires TLS outside explicit loopback development, obtains a token for each attempt, rebuilds requests for retry, and fails closed on mismatched acknowledgements. Only transport failures and explicitly transient HTTP statuses retry; stable operation ids make an uncertain replay resolve as a durable duplicate. This transport remains BunkFy-owned because its route, credential scheme, limits, and acknowledgement semantics are part of the adapter protocol rather than generic framework policy.

`BunkFy.Adapter.Runtime` and `BunkFy.AdapterHost` run the same `IAdapterRunner` outside the backend process. The preferred `server-lease` mode claims a statically configured `RemotePolling` connection, renews it while work is active, submits fenced observations, and advances only the server checkpoint. The explicit `local-file` compatibility mode delivers to a Push connection and uses an exclusive atomic checkpoint file for one process per connection. Configuration, secret, and ingress-token sources reload for rotation in either mode. Fleet-wide claim-any placement remains a later scheduler concern.

`imap.reservation-json` is the first real protocol adapter. MailKit, IMAP sequence lookup, MIME parsing, authentication mode, and UIDVALIDITY/UID cursor rules stay inside its adapter package. Ingestion receives only the shared observation envelope and therefore does not acquire mailbox concepts. Unsupported mail is accepted as bounded evidence and classified terminally downstream, preserving the durable-receipt-before-cursor rule without allowing poison messages to stall acquisition.

Retained-source parsing is a separate host-composed boundary. `BunkFy.Ingestion.Parsing.Abstractions` exposes bounded source input, versioned parser descriptors, and authority-free outputs; it does not expose tenant/property selection, operation ids, persistence, TaskRuntime, or product commands. Ingestion pins parser type/version in a durable attempt, fences the source raw object with an expiring reservation, derives operation authority itself, and submits outputs through the ordinary receipt path. A per-output ledger bridges accepted, duplicate, and rejected results to their resulting receipts without storing payload bodies. Parser packages are explicitly installed trusted code; dynamic assembly/script loading is rejected. This pattern stays in BunkFy until another project proves the same lifecycle well enough to justify GMA extraction.

This credential lifecycle stays in Ingestion because its authority is the product concept "submit observations for this connection." GMA AccessControl already models service subjects, but GMA Auth currently owns member/session JWTs rather than machine credentials. Extract only cryptographic token mechanics and generic service-principal authentication after another module proves the same lifecycle; connection assignment and submission policy remain Ingestion-owned. OAuth client credentials, workload federation, mTLS, and fleet-wide claim-any placement are separate future concerns.

TaskRuntime occurrence deduplication prevents duplicate scheduled task runs across scheduler instances. Ingestion separately enforces one active source run per connection with an application pre-check and a filtered unique database index, covering races with manual enqueue or another task occurrence. Disabling a connection removes it from schedule discovery but retains cadence for later resume; clearing cadence and changing away from polling are explicit optimistic connection operations.

Operator run controls preserve the same boundary. Ingestion API reads expose linked source-run and receipt facts for both execution kinds. Admin API/CLI first resolve those facts in the requested tenant/property scope, then delegate retry or cancellation only for TaskRuntime-backed runs; remotely leased runs are controlled by lease expiry or connection disable and cannot be presented as TaskRuntime work. Connection records store opaque configuration and write-only secret references only. Adapter type is immutable; future-run settings are optimistic-concurrency controlled; checkpoint reset requires the connection to be disabled.

Ingestion maintains a live, rebuildable active-property projection from Properties events and `IPropertiesTopologyProjectionExportSource`. Connection creation uses that local projection, avoiding synchronous reads or foreign keys into the Properties schema.

## Conflict Decision

Reservations keeps its lifecycle `Version` and adds a separate `DetailsRevision` plus details-change origin. Ingestion records the details revision produced by its last accepted operation.

Reservations integration-event handlers dispatch aggregate domain events inside the module-owned inbox transaction. External operations therefore produce the same allocation requests, cancellation requests, and applied-details history as CQRS commands without depending on the command unit-of-work pipeline.

Reservations persists the current revision/provenance on the aggregate and projects typed details-change events into its own before/after history table. Management commands accept only staff/admin/system provenance; adapter provenance is reserved for the idempotent external-operation boundary. Allocation-only lifecycle transitions never increment `DetailsRevision`.

The product boundary is asynchronous and operation based. Reservations owns request contracts for external create, guest-details change, and cancellation because they describe operations that Reservations accepts. Every request includes stable Ingestion operation/receipt/connection ids and normalized source identity. Reservations keeps its own scoped operation ledger and request fingerprint, so adapter retries cannot depend solely on Ingestion's deduplication state. Exact replays republish the stored outcome; incompatible reuse of an operation id is rejected.

Ingestion derives the Reservations source-system namespace from adapter type plus stable connection id, while retaining the provider's external record id as the source reference. Adapter type alone is not unique enough: two configured mailboxes or OTA accounts may legitimately emit the same external id. Replacing a connection is therefore an explicit relink/migration concern rather than an accidental identity merge.

The first canonical normalized payload is strict `reservation.v1` JSON. Adapters still own source-specific acquisition and parsing; Ingestion rejects unknown fields, invalid units/dates, oversized payloads, and stored hash mismatches before creating a product dispatch. Future normalized record families can add versioned normalizers without changing receipt storage or product contracts.

An `Accepted` cancellation means Reservations recorded the intent but Inventory release or late-allocation compensation is still in progress. Ingestion must not equate acceptance with the terminal source-link state; completion is derived from the correlated Reservations lifecycle fact. This avoids pretending that an asynchronous allocation saga completed inside the initial message transaction.

An incoming adapter change may auto-apply only when:

- connection policy permits automatic application;
- source revision is newer;
- reservation external identity still matches;
- current reservation `DetailsRevision` equals the adapter link's last applied details revision;
- Reservations accepts the same expected revision when handling the command.

Otherwise Ingestion creates a proposal. This final expected-revision check closes the race between deciding and applying.

Proposal acceptance is a new product-operation attempt with identity derived from the proposal version and the reservation details revision the operator reviewed. It never mutates or reuses the original observation dispatch. Exact command retries return the same attempt, a decision made against different reviewed state conflicts, and Reservations remains the final authority on whether the expected revision can be applied. Ingestion marks the proposal applied only after the correlated product outcome, including the terminal cancellation fact for asynchronous cancellation.

Changes to arrival, departure, or requested units use an allocation-amendment saga rather than release-then-create. Inventory evaluates the candidate while excluding the reservation's current allocation and updates that allocation atomically only on success. Its durable amendment decision ledger makes exact retries replay the original decision. Reservations keeps the existing stay details authoritative while the amendment is pending, commits its details revision and history only after Inventory confirmation, and retains the old booking and allocation when Inventory rejects the candidate.

V1 treats reservation details as one conflict unit. Field-level merging is deferred because ownership rules differ by provider and field, and silent partial merges are harder for staff to reason about.

## History Decision

Ingestion stores observation/proposal history. Reservations stores applied change history. Correlation and external operation ids link them. Neither raw adapter payloads nor rejected suggestions become reservation truth.

## Cursor Decision

Adapters advance source cursors after Ingestion durably acknowledges receipt. Downstream normalization or product rejection does not force the adapter to redeliver the entire source page. Failed records remain replayable from Ingestion's durable state.

For independently deployed polling adapters, Ingestion is also the assignment and checkpoint authority. A `RemotePolling` connection issues one bounded lease containing the current server checkpoint. Every renewal, submission, checkpoint advance, and completion is fenced by the credential, worker, run, lease id, and monotonic epoch. The adapter host keeps no authoritative local checkpoint; after expiry a replacement receives the last durably accepted server checkpoint and stale workers are rejected. Direct `Push` ingress remains lease-free and cannot use a remote-polling credential.

## Consequences

- Local adapters require slightly more protocol ceremony, but moving them out of process does not change product commands.
- Reservations needs details-specific revision/provenance in addition to aggregate concurrency.
- Ingestion becomes a durable control plane rather than a parser library.
- Both Ingestion and Reservations keep deliberate idempotency records at their own authority boundaries.
- Observation and proposal-triggered dispatches have separate durable identities, preserving a complete decision trail.
- Source ordering is automatic only when a monotonic sequence or comparable source timestamp is available; unverifiable ordering becomes a proposal.
- Staff authority is explicit and race-safe.
- Raw source evidence and normalized proposal/dispatch bodies have independent persisted retention deadlines; active proposals protect source evidence, terminal normalized bodies redact without deleting decision audit, and independently releasable property legal holds exclude both retention paths through a shared optimistic fence.
- Secret stores and remote adapter brokers remain host/infrastructure choices rather than Ingestion persistence concerns.
- Multiple acquisition mechanisms now support the project-level protocol, but one deterministic HTTP source and one generic file drop still do not justify moving provider runtime policy into GMA.

## Rejected Alternatives

- Last writer wins: silently overwrites staff decisions and cannot explain races.
- Compare only aggregate `Version`: system allocation transitions look like staff edits.
- Let adapters call Reservations directly: couples source implementations to product APIs and bypasses durable ingestion history.
- Put provider logic in Reservations: destroys source agnosticism and module ownership.
- Build remote microservices immediately: adds deployment complexity before the protocol is validated by diverse adapters.

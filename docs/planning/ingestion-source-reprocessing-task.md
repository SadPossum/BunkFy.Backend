# Ingestion Source Reprocessing Task

Status: implemented
Date: 2026-07-12

## Goal

Allow an operator to run an explicitly installed, versioned parser against a retained rejected observation without mutating or reopening the source receipt. Every successful parser output re-enters Ingestion as an ordinary derived observation, so existing deduplication, reservation authority, staff-conflict proposals, retention, and product dispatch remain authoritative.

The first concrete parser handles retained `mail.unparsed.v1` RFC822 evidence and recovers the strict reservation JSON envelope from exactly one JSON MIME attachment. This proves the boundary; provider-specific Booking.com, OTA, HTML, and OCR parsers can follow without changing Ingestion's orchestration model.

## Ownership

- `BunkFy.Ingestion.Parsing.Abstractions` owns a dependency-light, product-local parser contract. It is not a GMA concern yet.
- parser packages under `src/Adapters` own source-format decoding and their implementation dependencies.
- Ingestion owns parser discovery metadata, operator authorization, durable attempts, source/derived lineage, raw-evidence fencing, task orchestration, and derived receipt submission.
- GMA TaskRuntime continues to own scheduling, leases, retries, cancellation, timeout, and worker lifecycle. Reprocessing does not add another scheduler.
- Reservations and other product modules see only the normal normalized observation workflow.

No runtime assembly upload, reflection-based plugin loading, caller-provided type name, script execution, or arbitrary remote parser endpoint is allowed. Hosts explicitly compose trusted parser implementations. A future remote parser can implement the same local interface through an authenticated client proxy.

## Parser Contract

A parser descriptor has a stable normalized parser type, positive immutable parser version, and declared supported source record types. An executable parser must expose exactly the same descriptor as its registered capability.

Parser input contains only bounded source facts and bytes:

- source receipt id and source adapter type;
- source record type, external id, revision, and source/observation timestamps;
- content type, SHA-256 hash, and retained payload.

Parser output contains record type, external id, optional source revision/time, observed time, content type, payload, and SHA-256 hash. It cannot select tenant, property, connection, source receipt, attempt, operation id, receipt id, retention policy, or product action. Ingestion derives those authorities and deterministic identities.

The contract has bounded output count, per-output bytes, and aggregate bytes. A parser returns either parsed outputs or a stable no-match result. Invalid descriptors, malformed outputs, hash mismatches, undeclared source types, duplicate output identities, and descriptor/runtime mismatches fail closed.

## Durable Model

The source `ObservationReceipt` remains terminal and immutable. It gains only a time-bounded active reprocessing reservation used to fence raw deletion. A separate `ObservationReprocessingAttempt` records:

- tenant, property, connection, source receipt, and TaskRuntime run id;
- pinned parser type and version;
- requested actor, requested/start/completion timestamps, task attempt, and reservation expiry;
- queued, running, succeeded, no-match, failed, canceled, or expired state;
- parsed, accepted, duplicate, and rejected counts plus a bounded stable error code.

Derived receipts store source receipt id, reprocessing attempt id, parser type, parser version, and zero-based parser output index. The source receipt is never marked pending again and its rejection reason/history is preserved.

Only one non-expired attempt may reserve one source receipt. Initially only rejected receipts with available retained payload are eligible. An attempt may supersede an expired reservation, but never an active one.

## Enqueue And Retention Safety

The control plane reserves the source and creates the attempt transactionally in Ingestion before enqueueing a TaskRuntime run with the same id. If enqueue returns or throws a failure, it compensates by failing the attempt and releasing the source reservation.

Cross-module atomicity is not assumed. A queued reservation therefore has a bounded expiry derived from its scheduled time. A process crash between reservation and enqueue cannot retain data forever. Starting or retrying the task renews the reservation. Raw retention excludes only non-expired reservations and uses the receipt raw-payload concurrency token, so task start and purge claiming cannot both win.

Cancellation through the Ingestion control plane requests TaskRuntime cancellation and releases queued work when safe. Cancellation or host failure outside that path may leave a queued/running attempt until its reservation expires; it must not leave a permanent retention hold. Legal holds remain the only indefinite evidence-preservation mechanism.

## Execution

1. Validate tenant/property scope, source receipt eligibility, parser capability, parser/source compatibility, schedule bounds, confirmation, actor, and attempt count.
2. Create the attempt and reserve the raw payload for the scheduled task.
3. Enqueue the pinned parser type/version on the Ingestion maintenance worker group.
4. On each execution, atomically acquire or renew the attempt and source reservation.
5. Read the raw object and verify content type/hash against receipt metadata before parser invocation.
6. Validate all parser outputs and derive stable operation ids from source receipt, parser identity/version, output index, output identity, and content hash.
7. Submit each output through Ingestion's authoritative receipt path with explicit lineage. Disabled source connections may be reprocessed, but the property projection must remain active.
8. Treat `Accepted` and `Duplicate` as successful durable outputs. Any rejected/conflicting output makes the attempt failed; earlier accepted outputs remain idempotent and a retry reproduces them as duplicates.
9. Record terminal counts and release the source reservation. No-match is terminal and distinct from infrastructure/parser failure.

## Security And Operations

- reprocessing has a dedicated scoped manage permission; ordinary receipt reads do not grant raw parser execution;
- enqueue requires explicit confirmation in Admin API and `--yes` in Admin CLI;
- parser capabilities and attempt metadata are readable through existing scoped Ingestion reads, while raw payload bytes remain behind the separate raw-payload permission;
- no raw content, MIME headers, guest data, parser payload, or exception stack is stored in attempt errors or task payloads;
- task payloads contain ids and pinned parser metadata only;
- parser input/output buffers are cleared where practical after use;
- operators can list/get attempts and see lineage from both source and derived receipts;
- deterministic identities make retry and crash recovery safe.

## First Slice

1. Add and test parser abstractions and registries.
2. Add the reservation-mail parser and reuse its strict envelope decoder from the live IMAP adapter.
3. Add attempts, receipt reservation/lineage fields, EF mappings, PostgreSQL migration, and retention fencing.
4. Add prepare/start/complete/fail/cancel commands, queries, task payload/handler, permission, Admin API, and Admin CLI.
5. Compose descriptors in control-plane hosts and executable parsers only in Worker.
6. Add architecture, domain, application, parser, persistence, and Docker-backed end-to-end coverage.

## Deferred

- vendor-specific Booking.com/OTA templates, HTML scraping, OCR, provider DKIM/sender trust beyond the signed integration-mail contract, and representative production fixtures;
- bulk search/reprocessing campaigns, automatic parser-upgrade campaigns, and operator diff previews;
- remote parser service transport, parser sandboxing, quotas beyond current protocol bounds, and third-party parser distribution;
- indefinite reprocessing pins; operators must use legal holds for that requirement;
- generalized framework extraction. Move only the transport-neutral parser execution pattern to GMA after at least one other project proves the same lifecycle and security contract.

## Completion

Implemented a host-composed parser boundary, strict reservation-mail parser, durable attempt/output ledger, receipt lineage, time-bounded raw-evidence reservations, retention-query fencing, TaskRuntime execution, scoped permission, management/read APIs, and confirmation-gated Admin API/CLI controls. Derived observations use the normal receipt path and deterministic operation identities; source receipts remain terminal and disabled connections can be replayed only through a matching active attempt.

The live IMAP adapter and replay parser share one strict envelope reader. Control-plane hosts load descriptor metadata only, while Worker loads executable parser code. PostgreSQL enforces tenant-qualified source, attempt, derived-receipt, and output relationships. Focused tests cover parser strictness, no-match, descriptor/runtime composition, attempt lifecycle, raw-purge fencing, lineage, output audit, retry-safe task dispatch, and error redaction. Docker coverage uses PostgreSQL, MinIO, and TaskRuntime to reprocess retained RFC822 evidence through the production parser after disabling its source connection.

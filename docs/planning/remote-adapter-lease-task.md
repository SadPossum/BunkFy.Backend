# Remote Adapter Assignment And Lease Task

Status: implemented and verified
Date: 2026-07-12

## Goal

Let independently deployed polling adapters execute against a BunkFy-owned connection without split-brain source reads or node-local checkpoint authority. Ingestion must issue and fence one active assignment, accept observations only from that assignment, own the durable checkpoint, and reclaim work after a bounded lease expires.

This slice is the coordination contract for statically connection-bound adapter daemons. Fleet-wide discovery, placement, autoscaling, and a worker asking for any compatible connection remain later scheduler concerns.

## Execution Modes

Add `RemotePolling` as an explicit connection execution mode.

- `Polling` remains a BunkFy TaskRuntime-owned local runner.
- `Push` remains direct externally initiated observation delivery, such as a webhook receiver or provider-owned producer.
- `RemotePolling` means a BunkFy adapter runner polls a source outside the server process under a server-issued lease and submits leased observations.
- `Continuous` remains reserved for a future local or supervised continuous runner contract.

Adapters must declare `RemotePolling` support explicitly. A remote polling credential cannot bypass fencing through the direct Push endpoint, and a Push credential cannot claim polling work.

## Lease Protocol

The existing `BunkFy-Adapter` credential authenticates every call and remains bound to one tenant-scoped connection. No tenant, property, connection, adapter type, run, or checkpoint authority comes from an unverified request body.

1. The daemon claims its configured connection and supplies its expected adapter descriptor identity plus a non-empty worker GUID.
2. Ingestion verifies the credential, enabled `RemotePolling` connection, active property, descriptor compatibility, and absence of an unexpired local or remote run.
3. Ingestion atomically creates an `IngestionRun`, issues random run/lease IDs, increments the connection's monotonic lease epoch, and returns a normal polling `AdapterRunAssignment` containing the server checkpoint.
4. The daemon renews before expiry. Renewal is accepted only for the exact credential, worker, run, lease ID, and epoch.
5. Every observation submission carries the same proof. Each accepted record is linearized against the connection row so a concurrent takeover either occurs before it and rejects it, or after it and preserves it as valid pre-takeover work.
6. A proposed checkpoint advances only after every record in that submission is accepted or duplicate and the same lease remains current.
7. Completion records the run outcome and releases only the matching lease. Repeated identical completion is idempotent; mismatched or stale completion cannot clear a newer lease.
8. An expired lease is terminalized and replaced on the next claim. Stable operation IDs make records accepted before expiry safe to replay from the last server checkpoint.

Lease duration, renewal cadence, worker-ID length, request size, record count, checkpoint length, and response size are bounded. Server time is authoritative. Credentials, tokens, configuration/secret material, source payloads, and checkpoint text never enter logs or public host status.

## Persistence And Ownership

Ingestion owns lease state on `AdapterConnection`: current run, lease ID, credential, worker ID, monotonic epoch, and expiry. `IngestionRun` records whether execution came from TaskRuntime or a remote lease and retains the remote assignment identity for audit.

The domain and application layers stay provider/database agnostic. EF persistence uses ordinary optimistic concurrency on the connection and run rows; the PostgreSQL migration is the only provider-specific artifact. Concurrent claims may surface as a bounded conflict/retry response, never as two successful assignments.

`BunkFy.Adapter.Abstractions` owns only the transport-neutral remote control records. `BunkFy.Adapters.Http` owns their authenticated HTTP transport. `BunkFy.Adapter.Runtime` executes an assignment and verifies acknowledgements. `BunkFy.AdapterHost` owns daemon retry, renewal, material reload, and non-sensitive local status. GMA remains unchanged because the authority is Ingestion's connection and source checkpoint.

## Lifecycle Rules

- disabling a connection fences and terminalizes its current remote run in the same transaction;
- changing a `RemotePolling` connection to another mode requires no active remote lease;
- revoking or expiring a credential prevents renewal/submission immediately; the lease remains unavailable to another worker until expiry unless an operator disables it;
- checkpoint reset still requires a disabled connection and therefore no active lease;
- a worker losing renewal cancels its runner promptly and cannot submit or complete under a replacement epoch;
- server unavailability never causes a daemon to advance a local authoritative checkpoint;
- direct Push behavior and local TaskRuntime polling keep their existing semantics.

## Verification

- domain tests cover claim, renew, expiry takeover, fencing epoch monotonicity, stale proof rejection, disable revocation, mode separation, and checkpoint rules;
- application tests cover credential/descriptor/property checks, run lifecycle, idempotent completion, and no cross-tenant authority;
- persistence tests cover concurrency tokens, nullable task/remote run identity constraints, and lease-field shape;
- HTTP tests cover malformed/oversized requests, wrong mode, wrong token/tenant/worker/run/lease/epoch, renewal loss, stale submit, and response bounds;
- a PostgreSQL concurrency test proves two simultaneous claims yield exactly one assignment;
- an end-to-end standalone-host test proves claim, heartbeat, observation receipt, server checkpoint advancement, completion, restart replay, expiry takeover, and stale-worker fencing;
- build, migration drift, non-Docker tests, Docker tests, vulnerability audit, source-package checks, diff checks, and submodule checks pass.

## Deferred

- fleet-wide capability discovery and claim-any scheduling;
- server-pushed assignments over NATS, WebSockets, or long polling;
- workload federation, OAuth client credentials, mTLS, and per-node attestation;
- dynamic configuration/secret delivery from the server to remote workers;
- multi-region source ownership and provider-specific distributed locks;
- extraction of generic lease mechanics into GMA before another product proves the same semantics.

## Implemented Checkpoint

Ingestion now owns `RemotePolling` assignment, durable checkpoint, monotonic epoch fencing, heartbeat renewal, observation authorization, completion, expiry takeover, and disable-time cancellation. The standalone host defaults to `server-lease`, uses no local authoritative checkpoint in that mode, reloads credential/material sources, bounds failed cleanup, and reports ordinary lease contention without inflating runtime failures. Push and remote credentials are mode-separated, and adapter ingress uses the bounded global limiter rather than the login-specific low-volume partition.

Real PostgreSQL coverage proves one winner under simultaneous claims, an HTTP heartbeat, normalized server checkpoint advancement, lost-response-safe claim and completion retries, expiry takeover, stale-worker rejection, and legacy TaskRuntime-run migration backfill. Architecture guards keep lease mechanics in BunkFy shared runtime/transport plus Ingestion and out of GMA. The zero-warning build, migration drift checks, 1,534 non-Docker tests, 31 Docker tests, source-package checks, NuGet vulnerability audit, diff checks, and GMA submodule dev-head checks pass.

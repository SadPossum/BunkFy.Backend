# Standalone Adapter Host Task

Historical note: this task introduced the `local-file` plus Push coordination mode. The preferred independently deployed polling path is now the Ingestion-owned `server-lease` mode specified in [Remote Adapter Assignment And Lease Task](remote-adapter-lease-task.md); local-file mode remains explicit compatibility behavior.

Status: implemented

## Goal

Run an existing polling `IAdapterRunner` as an independently deployed daemon that submits observations through authenticated Ingestion HTTP ingress. Prove that moving acquisition out of the BunkFy worker does not change observation, deduplication, proposal, or product-module contracts.

## Boundary Decision

A standalone adapter uses an Ingestion connection configured for `Push`, while its local source acquisition cycle still executes the runner in `Polling` mode. An adapter descriptor may support both modes: polling means BunkFy TaskRuntime owns the cycle, while push means an external process owns cycle timing and submits through ingress.

Do not add a remote assignment broker or a second server scheduler. TaskRuntime remains authoritative for BunkFy-owned local work. A standalone daemon owns only its process-local schedule and checkpoint; Ingestion owns credentials, connection enablement, durable receipt acknowledgement, deduplication, normalization, proposals, and product dispatch.

## Runtime Library

Add a dependency-light `BunkFy.Adapter.Runtime` library that:

- executes one selected `IAdapterRunner` cycle with a local run/lease identity;
- adapts `IAdapterObservationSink` submissions to `IAdapterPushObservationSink` HTTP ingress;
- accepts a proposed checkpoint only when every observation is durably `Accepted` or `Duplicate`;
- persists that checkpoint before returning an accepted acknowledgement to the runner;
- fails the cycle if checkpoint persistence fails after remote acknowledgement, relying on stable operation ids for safe replay;
- verifies completion run/lease identity and accepted-checkpoint consistency;
- resolves bounded configuration/secret material per cycle and disposes it after every outcome.

The runtime has no reference to Ingestion, TaskRuntime, EF Core, product modules, or provider implementations.

## Checkpoint Ownership

The first durable checkpoint store is a versioned atomic JSON file on a persistent volume. It contains only connection id, checkpoint, generation, and update timestamp.

- a separate lock file is opened with exclusive sharing for the daemon lifetime;
- a second process using the same state path fails before executing source work;
- missing state means no checkpoint;
- malformed, unsupported, wrong-connection, oversized, linked/reparse, or otherwise unsafe state fails closed;
- writes use a same-directory temporary file, flush, and atomic replacement;
- checkpoint contents are bounded by the shared protocol limit;
- temporary files are cleaned after success or failure.

This deliberately supports one active standalone instance per connection. Horizontally scaled remote workers require a future server-owned assignment/lease protocol; the file lock must not be represented as a distributed lease.

## Executable Host

Add `BunkFy.AdapterHost`, a runnable process that selects one registered first-party adapter and validates:

- stable tenant, property, connection, and adapter identity;
- descriptor support for both local polling and remote push delivery;
- interval against provider minimum polling capability;
- bounded cycle timeout and retry backoff;
- exactly one token source and safe material/checkpoint paths.

The host reloads the ingress token and configuration/secret files for each attempt/cycle so rotation does not require writing secrets into appsettings or restarting the process. Secret/configuration bytes are bounded and disposed/cleared through `AdapterConfigurationMaterial`.

The polling service runs one cycle at a time, supports graceful cancellation, and continues after factual failed outcomes or transient exceptions using bounded backoff. It never advances a checkpoint from a rejected or uncertain submission.

## Local Status

The host exposes loopback-bound-by-default endpoints:

- `/health/live`: process is serving;
- `/health/ready`: configuration, runner selection, and checkpoint lease were acquired;
- `/status`: adapter type, connection id, current state, last cycle timestamps/outcome/error code, next cycle time, consecutive failures, and whether a checkpoint exists.

Status never exposes tenant id, property id, checkpoint text, provider configuration, secret material, token source path/value, or payload data. It is factual process evidence, not a provider-health verdict.

## Security And Reliability Invariants

- standalone adapters authenticate only with connection-bound `BunkFy-Adapter` credentials;
- staff/member JWTs and management permissions never enter the runtime;
- HTTPS is mandatory outside explicitly enabled loopback development;
- no credential, secret, checkpoint value, or payload is logged or returned by status;
- source work cannot begin before the exclusive checkpoint lease is acquired;
- accepted remote observations with a failed local checkpoint write replay safely by stable operation id;
- runner completion cannot claim a checkpoint different from the one durably stored;
- a disabled or revoked server connection rejects submissions without checkpoint advancement;
- the host does not duplicate TaskRuntime leases, Ingestion runs, or server scheduling.

## GMA Decision

Keep this runtime in BunkFy. HTTP routes, adapter credentials, observation acknowledgement, and checkpoint semantics are BunkFy protocol concerns. Reconsider generic extraction only after another project needs the same independently deployed producer lifecycle.

## Acceptance Checks

- existing first-party polling descriptors can explicitly support standalone push delivery without changing local execution;
- accepted and duplicate responses advance the local checkpoint, while rejected responses do not;
- checkpoint persistence occurs before runner acknowledgement and failures replay safely;
- file state is atomic, bounded, connection-bound, and exclusively leased;
- token and material sources reload and enforce bounds without exposing values;
- host validation rejects unsafe identity, transport, timing, path, and descriptor combinations;
- status is factual and excludes sensitive fields;
- an end-to-end test runs a runner cycle through the real HTTP ingress and observes durable receipt plus checkpoint;
- build, migration drift, fast tests, complete Docker tests, dependency guards, and submodule checks pass.

## Completion

Implemented a dependency-light runtime, an executable single-connection host, crash-safe file checkpointing, reloadable token and material sources, bounded polling/backoff, and non-sensitive local health/status endpoints. The first-party fake HTTP and JSON file-drop runners now support both BunkFy-owned polling and standalone push delivery without changing Ingestion or product-module contracts.

Focused coverage proves accepted, duplicate, rejected, replay, lock, corruption, rotation, completion-integrity, host-readiness, and status-disclosure behavior. A Docker integration test executes the real standalone runtime through authenticated HTTP ingress and observes both a durable Ingestion receipt and a persisted local checkpoint. The all-up build, migration drift checks, 1,450 non-Docker tests, and 30 Docker tests pass.

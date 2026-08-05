# Durable Runtime Production Admission Task

Status: complete
Date: 2026-08-05

## Goal

Prevent a Production BunkFy deployment from accepting tenant work while its
Worker release identity, task execution topology, message-journal cleanup, or
TaskRuntime history retention is undeclared or contradictory.

## Ownership

- GMA Messaging and TaskRuntime continue to own generic cleanup mechanics,
  bounded batches, leases, heartbeats, retries, retention option validation,
  and operator APIs.
- BunkFy owns the concrete production approval, replay and retention windows,
  Worker topology, selected maintenance owner, and exact release identity.
- Product modules continue to own task payloads, idempotent side effects,
  business retention, legal holds, and workload-specific alerts.
- No BunkFy permission, module name, retention decision, or deployment role is
  added to GMA.

## Audit Findings

- GMA validates enabled cleanup settings and the Worker exposes finite defaults,
  but BunkFy Production startup does not require those services to be activated.
- The public and Admin APIs validate release identity; the Worker does not.
- `Tasks:Worker:Enabled` can be combined with an uncomposed TaskRuntime store,
  and NATS consumers can be requested without NATS publishing, without an
  explicit BunkFy startup error.
- Every long-running host registers message-journal cleanup, so Production must
  prove that exactly one Worker process profile owns it and that non-owners keep
  it disabled.
- TaskRuntime Admin API and CLI operations already use GMA's bounded contracts,
  permissions, auditing, and BunkFy's strong Admin API assurance. No operator
  contract refactor is required in this slice.

## Invariants

- Every Production Worker declares the exact source commit and immutable image
  digest when containerized.
- Production runtime admission requires a non-secret approval reference and one
  declared Worker maintenance-owner instance.
- Only the maintenance-owner process enables message-journal and TaskRuntime
  retention; non-owner API, Admin API, and Worker processes keep both disabled.
- Approved outbox, inbox, replay, task-run, and task-control windows exactly
  match the configured runtime values on the owner.
- The maintenance owner composes TaskRuntime, and task execution cannot be
  enabled without the TaskRuntime persistence module.
- NATS consumers cannot be enabled when NATS publishing is disabled.
- Development, tests, and preview retain opt-in composition and disabled cleanup
  defaults.

## Delivery

1. [Completed] Extend BunkFy's production deployment identity to the Worker
   without applying HTTP-only requirements to it.
2. [Completed] Add durable-runtime production admission options, validation, and
   payload-free startup evidence.
3. [Completed] Compose the admission policy in the public API, Admin API, and
   Worker, preserving one explicit maintenance owner.
4. [Completed] Add focused validation and architecture coverage plus the operator
   activation note.
5. [Completed] Run focused checks, then one coherent non-Docker host gate.

## Outcome

- Production startup now requires one approved Worker maintenance owner, exact
  durable-runtime windows, and internally consistent task and NATS composition.
- The Worker now carries the same immutable release-identity contract as the API
  hosts without inheriting HTTP-only deployment requirements.
- GMA remains responsible for generic leases, cleanup engines, retention jobs,
  and operator contracts; no BunkFy policy or deployment role leaked into it.
- Live policy approval, orchestrator reconciliation, backup/restore evidence,
  and alert wiring remain deployment activation work rather than application
  code gaps.

## Verification Evidence

- `dotnet build BunkFy.slnx --no-restore -m:1 --verbosity:minimal`: succeeded
  with 0 warnings and 0 errors.
- `BunkFy.Host.ServiceDefaults.Tests`: 41 passed.
- focused durable-runtime architecture tests: 7 passed.
- complete non-Docker architecture gate: 85 passed after synchronizing the
  generated solution graph.
- complete non-Docker integration gate: 54 passed.
- no Docker scenario was required because the slice changed no schema, broker
  behavior, or provider implementation.

## Deferred

- Real approval references, final legal/investigation windows, replica counts,
  alert destinations, and orchestrator process profiles remain deployment
  inputs.
- Runtime configuration cannot prove the actual replica count; private
  deployment evidence must reconcile it with the declared single owner.
- Database, NATS, MinIO, and restore drills remain target-environment gates.
- Auth and Organizations history retention have separate owners and require
  separate bounded admission slices.
- Generic Admin API cache policy belongs to GMA Administration and is not mixed
  into this BunkFy runtime-topology change.

## Verification Cadence

Use focused ServiceDefaults and architecture tests while editing. At the
coherent slice boundary, run the complete ServiceDefaults tests, architecture
guards, non-Docker integration tests, and a zero-warning solution build once.
No Docker scenario is required because this slice changes no schema, broker
behavior, or provider implementation.

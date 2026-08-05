# Migrations Host Production Safety Task

Status: complete
Date: 2026-08-05

## Goal

Make `BunkFy.Host.Migrations` a fail-closed production deployment executable
that can inspect, approve, serialize, apply, resume, and prove the exact ordered
PostgreSQL schema target without turning module persistence into deployment
policy.

## Ownership

- Each GMA or BunkFy module owns its `DbContext`, schema, migration history, and
  provider-specific migration assembly.
- `BunkFy.Host.Migrations` owns the product's enabled module order, release and
  database admission, cross-module execution lock, operation budget, evidence,
  and forward-resumption policy.
- Private deployment automation owns real approvals, backup/restore proof,
  rollback/forward-repair decisions, secret delivery, and promotion of the
  immutable artifact.
- GMA does not own BunkFy's module catalogue, database target, release approval,
  or production rollout policy. The current framework and skeleton expose no
  generic migration deployment runner that this host should consume.

## Audit Findings

- The host unconditionally calls `MigrateAsync` for fifteen contexts and has no
  read-only planning mode.
- Concurrent migration-host processes can interleave different module contexts;
  per-context EF locking does not serialize the complete BunkFy catalogue.
- Production mutation is not bound to an approved source commit, immutable
  container image, database target, backup evidence, rollback evidence, or exact
  migration catalogue.
- Cancellation is `CancellationToken.None`; lock acquisition and the complete
  run have no explicit budgets.
- A failure after one module commits leaves a valid partial forward migration,
  but the host emits no deterministic state evidence or explicit resumption
  contract.
- A down-level executable can encounter migration history unknown to its loaded
  assemblies without a product-level compatibility check.

## Invariants

- `Plan` is read-only. It acquires the same product migration lock, inspects all
  histories, validates that every applied history is an exact prefix of the
  loaded target, and reports payload-free digests and counts.
- `Apply` performs the same inspection under the same lock before mutation.
- Production `Apply` requires an approved non-secret evidence reference, exact
  source commit, runtime declaration, database-target fingerprint, target
  catalogue digest, backup evidence, and rollback/forward-repair evidence.
- A container runtime also requires an immutable lowercase OCI image digest.
- One PostgreSQL session advisory lock covers inspection and all module
  migrations. Acquisition and the total operation are bounded and cancellable.
- The target catalogue digest includes the exact product module order and every
  loaded migration identifier. It remains stable when an interrupted run is
  resumed with the same release.
- Applied migrations must be an exact prefix of each module target. Unknown,
  reordered, or down-level history fails before any new migration runs.
- Modules migrate sequentially. A failure never pretends cross-schema atomicity;
  the same approved release can safely continue from the committed prefix.
- Logs contain only reviewed release references, hashes, module names, migration
  identifiers/counts, timings, and outcomes. Connection strings and data are
  never logged.
- Development keeps an ergonomic `Apply` default without Production approval.

## Delivery

1. [Complete] Add options, deterministic catalog/state planning, production
   admission, and structured evidence.
2. [Complete] Add bounded PostgreSQL session-lock ownership and sequential,
   resumable migration coordination.
3. [Complete] Refactor the executable around explicit `Plan` and `Apply` modes and
   host-lifetime cancellation.
4. [Complete] Add focused unit tests, architecture guards, operator documentation,
   and fail-closed defaults.
5. [Complete] Run focused checks, then one exact PostgreSQL lock/apply/resume
   scenario and one coherent non-Docker slice gate.

## Outcome

- `Plan` and `Apply` now share exact-prefix inspection, a deterministic
  fifteen-module catalogue, target/current/pending digests, and a single bounded
  PostgreSQL advisory lock.
- Production `Apply` is bound to immutable release, database-target, catalogue,
  approval, backup, rollback, and compatible-history evidence. Production logs
  emit only reviewed references, hashes, bounded counts, and timings; even the
  configured database alias is hashed.
- Sequential migration is explicitly resumable from a committed compatible
  prefix. Every context and the final catalogue are verified after apply.
- The PostgreSQL proof exposed Npgsql pooled-session lock retention. The lock
  owner now explicitly executes `pg_advisory_unlock` before returning the
  connection to the pool, with physical session termination as the failure
  fallback.

## Verification Evidence

- `BunkFy.Host.Migrations.Tests`: 24 passed.
- `Architecture.Tests`: 89 passed.
- `Integration.Tests` with `Category!=Docker`: 54 passed.
- Exact PostgreSQL scenario
  `Host_resumes_a_partial_catalogue_plans_idempotently_and_serializes_apply`:
  1 passed, covering partial-catalogue resumption, idempotent planning, and
  bounded competing-lock behavior.
- `eng/update-solutions.ps1 -Check`: backend solution synchronized.
- `dotnet build BunkFy.slnx --no-restore -m:1 --verbosity:minimal`: succeeded
  with 0 warnings and 0 errors.
- `git diff --check`: passed; only pre-existing line-ending normalization
  notices were reported.

## Deferred

- Real database endpoints, approval references, backup/restore evidence, image
  digests, and release orchestration remain private deployment inputs.
- Destructive down migrations and automatic rollback are not supported. Failed
  forward migrations require operator-owned forward repair or an approved
  database restore.
- Online/expand-contract compatibility, maintenance windows, replica draining,
  and release promotion remain deployment and module-design responsibilities.
- A generic GMA migration deployment package remains deferred until another
  product proves a provider-neutral reusable boundary.

## Verification Cadence

Use focused pure tests and a project build while editing. At the coherent slice
boundary, run the complete migrations-host test project, architecture and
non-Docker integration gates, solution synchronization, and one zero-warning
solution build. Run one exact PostgreSQL scenario only after the implementation
stabilizes; do not run the broad Docker matrix.

# GMA Durable Runtime Alignment

Status: code foundation passed; deployment activation pending

## Decision

BunkFy will not start another product domain module until GMA durable-runtime hardening is released, integrated, and proven against BunkFy's ingestion workload.

The implementation source of truth is [GMA Durable Runtime Hardening](../../gma/framework/docs/architecture/durable-runtime-hardening-task.md).

## Boundary

GMA owns reusable task-worker correctness, messaging journal lifecycle, JetStream safety, TaskRuntime history lifecycle, generic observability, and conformance tests.

BunkFy owns:

- ingestion and reservation business behavior;
- adapter/source conflict policy and staff suggestions;
- raw evidence, legal holds, redaction, and business retention deadlines;
- event payload minimization and data classification;
- concrete runtime values, worker topology, SLOs, and alerts;
- authoritative tenant/scope scheduling;
- BunkFy-specific load and failure workloads.

No BunkFy domain type, permission, schedule, retention rule, or adapter implementation may be introduced into GMA.

## Work Order

1. GMA task lease, fairness, and automatic-heartbeat correctness.
2. GMA outbox/inbox lifecycle retention.
3. GMA bounded/validated JetStream management and acknowledgement safety.
4. GMA TaskRuntime terminal-history retention.
5. GMA Skeleton production configuration and failure-oriented conformance proof.
6. Published submodule updates in BunkFy.
7. BunkFy runtime-policy configuration and maintenance scheduling.
8. BunkFy ingestion performance/failure harness and measured baseline.
9. Final security, architecture, storage, and operational audit.

## BunkFy Acceptance Workload

The final harness must exercise:

- normal polling across multiple adapter connections;
- direct and remote burst submissions;
- duplicates and source-revision conflicts;
- slow and temporarily unavailable MinIO;
- NATS outage and recovery;
- worker termination and task reclaim;
- staff-edited reservation conflicts and proposal creation;
- raw-payload purge and normalized-history redaction during ingestion;
- multiple tenants without cross-scope reads or cleanup;
- backlog drain after downtime.

Measure at minimum:

- observation acceptance latency;
- accepted-to-processed latency;
- accepted-to-final-reservation latency;
- outbox/inbox backlog count and oldest age;
- task queue wait and execution duration;
- duplicate/redelivery/retry counts;
- PostgreSQL table and index growth;
- MinIO object count and retained bytes;
- broker stream bytes and message age;
- concurrency conflicts, timeouts, and failed cleanup batches.

## Exit Gate

Product-domain development may resume only when:

- BunkFy points to published GMA framework/module commits;
- framework, module, Skeleton, and BunkFy validation suites are green;
- migrations and submodule-head guards are green;
- retention jobs are operationally provisioned for every active tenant;
- broker and journal storage are bounded by explicit production configuration;
- worker leases remain correct under multi-worker failure tests;
- the ingestion harness has a recorded baseline and no unexplained data loss, duplicate side effect, tenant leak, or unbounded growth;
- remaining capacity limits and deferred optimizations are documented with observable thresholds.

While the code foundation gate is active, existing BunkFy modules may be changed only as needed for runtime integration, correctness, testing, or production hardening.

## Completion Evidence

The code foundation gate passed on 2026-07-12:

- GMA Framework `5933ad2`, TaskRuntime `0a07c9d`, Auth `5e32075`, and Notifications `ca76af4` are published and integrated;
- GMA Skeleton `e8cc2cb` carries the finite defaults, provider migrations, production notes, and architecture guards;
- the BunkFy super-solution builds with zero warnings, architecture tests pass, 1,478 non-Docker tests pass, all provider models have committed migrations, and the Docker/runtime evidence sets pass;
- all applicable BunkFy inbox stores have the cleanup index; Properties remains correctly outbox-only;
- finite JetStream limits, acknowledgement progress, automatic task heartbeats, bounded journal cleanup, and bounded task-history cleanup are exposed through guarded host configuration;
- [the ingestion runtime baseline](../operations/ingestion-runtime-baseline.md) records two-tenant burst, MinIO/NATS outage recovery, storage growth, reservation application, and staff-conflict proposal timings.

No unexplained data loss, duplicate side effect, tenant leak, staff overwrite, migration drift, or unbounded configured broker limit was observed.

The following are deployment activation work, not missing reusable code: select production retention/replay/legal-hold windows, enable the disabled-by-default cleanup services, provision recurring tenant-scoped Ingestion retention tasks, configure OTLP/Prometheus and alerts, size connection pools/worker replicas/JetStream replicas, and prove backup/restore on the target environment. Product feature development may resume before a deployment exists, but a live environment may not accept tenant data until these activation checks pass.

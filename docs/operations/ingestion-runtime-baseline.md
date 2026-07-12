# Ingestion Runtime Baseline

Status: production-foundation evidence
Date: 2026-07-12

Run the repeatable Docker-backed evidence set with:

```powershell
.\eng\test-ingestion-runtime.ps1 -NoBuild --no-restore
```

The harness covers the product-specific two-tenant acceptance burst and reservation conflict flow together with remote/local adapter operations, MinIO, NATS ownership and deduplication, journal retention, TaskRuntime lease/reclaim/cancellation/retention, worker restart, IMAP checkpointing, and backlog recovery. Timing values are baselines, not pass/fail SLOs; correctness, isolation, bounded growth, and recovery are asserted.

## Local Baseline

Environment: Windows, Docker Desktop with 7.57 GB assigned memory, PostgreSQL 16 Alpine, current MinIO image, current NATS image, Debug `net10.0` build.

Two-tenant direct burst, 24 accepted observations plus 8 exact duplicates and one conflicting operation:

| Measure | Result |
| --- | ---: |
| Acceptance p50 | 46.4 ms |
| Acceptance p95 | 66.5 ms |
| Acceptance max | 67.5 ms |
| Duplicate p95 | 10.4 ms |
| First acceptance after MinIO recovery | 235.7 ms |
| NATS-outage backlog | 24 messages |
| Backlog drain after NATS recovery | 218.2 ms |
| Receipt table size | 221,184 bytes |
| Receipt index size | 163,840 bytes |
| Retained MinIO objects/bytes | 25 / 7,475 |
| JetStream messages/bytes | 25 / 14,247 |

This run proved that a failed MinIO write created no receipt, retry after recovery succeeded, the paused broker did not block durable acceptance, all queued outbox messages drained after recovery, duplicates created neither receipts nor objects, an incompatible operation was rejected, and each tenant observed only its own receipts.

Reservation authority flow:

| Measure | Result |
| --- | ---: |
| Accepted observation to created reservation | 1,976.0 ms |
| Accepted amendment to applied reservation update | 842.5 ms |
| Accepted conflicting update to staff-review proposal | 534.0 ms |

This path uses fresh worker instances between stages and proves restart recovery, allocation-aware amendments, conflict rejection, preservation of a later staff edit, and proposal creation instead of silent overwrite.

## Release Interpretation

- Do not turn local timings into production promises. Establish staging SLOs with release hardware, expected payload distributions, TLS, real network latency, production telemetry exporters, and representative provider bursts.
- Alert on oldest outbox/inbox age, task queue age, repeated cleanup failures, JetStream limit utilization, MinIO capacity, PostgreSQL table/index growth, lease loss, redelivery, and proposal backlog.
- Re-run after changes to GMA messaging/tasks, Ingestion receipt storage, file storage, database indexes, worker topology, or broker retention.
- Production deployment must explicitly enable journal/task retention only after replay, legal-hold, backup, and incident-response windows are approved.

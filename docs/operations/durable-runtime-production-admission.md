# Durable Runtime Production Admission

BunkFy Production hosts fail closed until one Worker maintenance process and
the durable-runtime retention/replay policy are explicitly approved. GMA owns
the cleanup engines; this contract selects their BunkFy deployment topology and
values.

## Required Declaration

Public API, Admin API, and every Worker process receive the same approved policy:

```json
{
  "BunkFy": {
    "DurableRuntime": {
      "ProductionAdmission": {
        "ApprovalState": "Approved",
        "ApprovalReference": "ops/runtime-policy-2026-08",
        "MaintenanceOwner": "Worker",
        "MaintenanceOwnerInstanceCount": 1,
        "CurrentProcessOwnsMaintenance": false,
        "ProcessedOutboxRetention": "7.00:00:00",
        "ProcessedInboxRetention": "14.00:00:00",
        "BrokerReplayHorizon": "7.00:00:00",
        "SucceededRunRetention": "30.00:00:00",
        "FailedRunRetention": "90.00:00:00",
        "CanceledRunRetention": "30.00:00:00",
        "TimedOutRunRetention": "90.00:00:00",
        "HandledControlRetention": "30.00:00:00",
        "FailedControlRetention": "90.00:00:00",
        "ExpiredControlRetention": "30.00:00:00"
      }
    }
  }
}
```

The values above are examples, not legal approval. Replace the reference and
windows with the deployment's reviewed policy. The reference is a non-secret
evidence identifier and must not contain credentials or personal data.

## Maintenance Owner

Exactly one Worker process profile sets:

```text
BunkFy__DurableRuntime__ProductionAdmission__CurrentProcessOwnsMaintenance=true
MessageJournalCleanup__Enabled=true
TaskRuntimeRetention__Enabled=true
Worker__Modules__TaskRuntime=true
```

Public API, Admin API, and every non-owner Worker keep both cleanup settings
false and set `CurrentProcessOwnsMaintenance=false`. The declared owner count is
validated by startup but must also be reconciled against the orchestrator's
actual replica count before traffic.

The owner must clean both processed outbox and inbox journals. The configured
journal and TaskRuntime windows must exactly match the approved declaration, and
inbox retention cannot be shorter than the broker replay horizon.

## Worker Release Identity

Every Production Worker also supplies `BunkFy:Deployment` with `Profile`,
`Runtime`, exact 40-character source commit, and an immutable OCI digest when
containerized. A Worker that composes Ingestion must additionally satisfy the
same object-storage service-account and hosted transport rules as the API.
HTTP edge, API topology, and Data Protection key-ring declarations do not apply
to the Worker process.

## Composition Failures

Startup rejects:

- pending approval, a missing evidence reference, or an owner count other than
  one;
- cleanup enabled on a non-owner or disabled on the selected owner;
- task execution without TaskRuntime persistence;
- task scheduling without both task execution and TaskRuntime;
- NATS consumers without NATS publishing;
- unapproved, mismatched, non-positive, or excessively long retention windows;
- a Production Worker without exact release identity.

## Activation Evidence

Before real tenant data, retain the approved policy, exact release/image,
orchestrator owner replica count, database and broker backup/restore results,
cleanup metrics, oldest-backlog alerts, and one failure/recovery drill. Do not
manually delete journal, task, control, or module-owned rows to force a clean
dashboard.

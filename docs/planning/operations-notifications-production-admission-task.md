# Operations Notifications Production Admission Task

Status: implemented; production activation blocked pending approval
Date: 2026-07-31

## Goal

Finish the Operations Notifications owner capability with a production
activation gate that proves BunkFy selected an exact notification-retention
policy, approved the exact executable personal-data catalogue, disposed of
legacy pre-reference history, and assigned cleanup to one process.

The gate must fail closed without turning a configuration declaration into
legal approval or moving BunkFy policy into GMA.

## Ownership Boundary

- GMA Notifications owns generic inbox persistence, delivery attempts,
  retention queries, bounded cleanup, reference-version advancement, close
  receipts, replay suppression, indexes, and reusable failure logging.
- Operations Notifications owns the exact BunkFy catalogue, the relationship
  between its product history and approved retention windows, and admission
  evidence for pre-reference history.
- BunkFy host composition owns which process executes generic retention and
  which deployment topology is allowed to select that process.
- Private deployment operations own the approval record, actual replica count,
  alert routing, backup expiry, rollout evidence, and proof that the declared
  process is deployed.

GMA runtime behavior is unchanged by this slice. Its Notifications integration
suite gains one generic PostgreSQL proof for retention deletion, active-delivery
protection, and reference-version advancement. A second BunkFy cleanup
implementation, direct notification-table writes, and BunkFy-specific
configuration in GMA are forbidden.

## Existing Reusable Capability

GMA Notifications already provides:

- disabled-by-default, validated retention options;
- distinct read, unread, broadcast, and delivery-attempt windows;
- bounded batches and bounded batches per cycle;
- retention indexes for PostgreSQL and SQL Server;
- protection for pending, processing, and retry-scheduled deliveries;
- atomic notification removal with history-reference version advancement;
- retained zero-record and closed reference state for stale-decision and replay
  safety; and
- retry-on-failure behavior with a stable error log suitable for alerting.

The missing work is product and deployment admission, not another retention
engine.

## Admission Evidence

`BunkFy:OperationsNotifications:ProductionAdmission` declares:

- `ApprovalState`: `Pending` or `Approved`;
- `ApprovalReference`: a bounded, non-secret identifier for the external
  approval record;
- `CatalogVersion`: the exact embedded Operations Notifications catalogue
  version;
- `CatalogSha256`: the lowercase SHA-256 of the exact embedded catalogue bytes;
- `ReadHistoryDays`, `UnreadHistoryDays`, `BroadcastDays`, and
  `DeliveryAttemptDays`: the approved product windows;
- `LegacyHistoryDisposition`: either `ResetBeforeAdmission` or
  `VerifiedReferenceComplete`;
- `RetentionOwner`: `PublicApi` or `Worker`; and
- `RetentionOwnerInstanceCount`: exactly one.

The digest binds approval to formatting as well as semantics. Any catalogue
change requires a new approval record and digest.

Production validation requires:

1. The external declaration is approved and has a valid reference.
2. The embedded catalogue passes `PersonalDataCatalogValidationMode.Production`.
3. Declared catalogue version and SHA-256 match the embedded artifact.
4. Declared windows exactly match GMA Notifications runtime options.
5. The selected process enables GMA notification retention.
6. Every other composed process disables GMA notification retention.
7. `AdminApi` can never own cleanup.
8. A `PublicApi` owner uses `SingleReplica` topology.
9. A `Worker` owner composes the Notifications module.
10. The declared owner instance count is one.
11. Legacy pre-reference history has an explicit safe disposition.

Repository defaults remain `Pending`, leave generic retention disabled, and
therefore cannot admit a Production process. Legal approval is not inferred
from passing tests or from an engineering default.

## Host Roles

- `PublicApi` always composes Notifications and may own cleanup only in an
  explicitly single-replica deployment.
- `Worker` may own cleanup only when `Worker:Modules:Notifications=true`.
- `AdminApi` composes the shared Notifications database for operator APIs but
  must never execute retention.
- `Migrations` applies schema changes only. Its retention and delivery workers
  remain disabled and it is outside the long-running owner election.

The admission validator is registered by all three long-running hosts. A
successful Production process emits one PII-free startup record containing the
catalogue version and digest, approval reference, host role, selected owner,
legacy disposition, and approved windows. It never logs notification content,
tenant or subject identifiers, provider destinations, or credentials.

## Legacy History

Current product notifications contain exact Staff, Reservation, and Ingestion
references. Older pre-reference copies cannot be safely reconstructed from
arbitrary payload JSON or current mutable account links.

Before the first production admission, deployment operations must choose and
evidence one of:

- `ResetBeforeAdmission`: remove pre-production notification history and drain
  or reset pending source-event replay before accepting tenant data; or
- `VerifiedReferenceComplete`: prove that the target database contains no
  product copy missing its mandatory current references.

The declaration is not the proof. The private deployment record must retain
the query/result, database identity, timestamp, release identity, approver, and
rollback decision.

## Operator Workflow

The public runbook will cover:

1. approve the exact catalogue and runtime windows outside the repository;
2. calculate and record the exact catalogue digest;
3. resolve legacy history;
4. select one owner process and one owner instance;
5. run migrations before activation;
6. start non-owner processes with retention disabled;
7. start the owner with retention enabled and retain its sanitized admission
   record;
8. verify the exact PostgreSQL cleanup scenario and alert routing for
   `Notification retention iteration failed`;
9. inspect delivery backlog and exhausted work through existing GMA
   Notifications admin operations; and
10. pause cleanup by redeploying the owner with retention disabled if policy,
    backup, legal-hold, or database evidence becomes uncertain.

No public customer endpoint, mutable in-process switch, or product-specific
retention command is introduced.

## Verification

- pending, malformed, stale-digest, stale-version, invalid-window, missing
  legacy disposition, invalid owner, and multi-owner declarations fail closed;
- approved synthetic catalogue evidence can pass without changing the
  repository catalogue to `Approved`;
- runtime retention windows must equal the approved declaration exactly;
- Public API, Worker, and Admin API register the same admission policy;
- only the selected role may register the generic retention hosted service;
- the Worker cannot be selected without the Notifications module;
- a Public API owner cannot use multi-replica topology;
- startup evidence contains only approved bounded metadata;
- architecture guards keep defaults pending and retention disabled;
- the Operations Notifications catalogue and generated inventory remain
  current;
- focused tests pass while editing, followed by one coherent non-Docker gate;
  and
- one exact PostgreSQL GMA retention scenario proves translated deletion,
  active-delivery protection, and reference-version advancement before
  publication.

## Completion Boundary

The code slice is complete when admission, host wiring, documentation, guards,
and deployment proof are published. Actual production admission remains
blocked until a real approval reference, approved catalogue, approved windows,
legacy-history evidence, and target-deployment topology exist.

## Completion Evidence

- Operations Notifications unit and registration tests: 80 passed.
- Host composition, privacy-output, and documentation guards: 30 passed.
- `eng/verify.ps1 -SkipRestore`: synchronized solution, source-package checks,
  serial solution build with zero warnings, migration drift, and every
  non-Docker test project passed.
- Exact PostgreSQL proof
  `Retention_deletes_only_completed_history_and_advances_reference_versions_on_postgresql`:
  1 passed and published in GMA Notifications commit `933e2a2`.
- Repository defaults remain pending and generic notification retention remains
  disabled, so this evidence does not fabricate production approval.

## Deferred

- legal approval of the concrete notification-retention periods;
- private infrastructure, alert destinations, and deployment records;
- provider-side deletion after email, SMS, or push delivery;
- lifecycle-state compaction after all decisions and backups expire;
- broad tenant-termination deletion and backup expiry;
- generic account-notification rights; and
- durable cross-process retention status beyond existing GMA failure logs and
  deployment monitoring.

# Operations Notifications Production Admission

This runbook activates BunkFy's product notification retention. It does not
choose legal periods, approve a catalogue, deploy a process, or prove external
infrastructure.

Repository defaults are deliberately blocked:

- the Operations Notifications catalogue and every classified field remain
  `engineering-default`;
- `ApprovalState` is `Pending`;
- no approval reference or catalogue digest is configured;
- no retention owner is selected; and
- GMA notification retention is disabled in API, Worker, and Admin API.

Do not accept tenant data in Production until every step below has private,
reviewed evidence.

## Approval Package

The approval package must identify:

- Operations Notifications catalogue version and exact SHA-256;
- approved read-history, unread-history, broadcast, and delivery-attempt
  windows;
- legal-hold and backup-expiry behavior;
- the pre-reference history disposition;
- one retention owner process and one owner instance;
- approver, approval time, review date, and rollback owner; and
- exact BunkFy source commit and deployment artifact.

The approval reference is a bounded evidence identifier, not a URL containing
credentials, a person's name, a ticket description, or a secret.

Calculate the digest from the exact repository file that is embedded into the
candidate:

```powershell
$catalog = 'src/Extensions/BunkFy.Extensions.Operations.Notifications/docs/personal-data-catalog.v1.json'
(Get-FileHash -LiteralPath $catalog -Algorithm SHA256).Hash.ToLowerInvariant()
```

Any byte change invalidates the approval and requires a new digest.

## Legacy History

Choose exactly one disposition before first admission:

- `ResetBeforeAdmission`: clear pre-production notification history, pending
  delivery work, and replayable product source events before tenant traffic;
  or
- `VerifiedReferenceComplete`: prove every retained product notification has
  its mandatory current Staff reference and, where applicable, Reservation or
  Ingestion source-link reference.

Retain the bounded query or reset procedure, result counts, database identity,
timestamp, source commit, operator, approver, and rollback decision in the
private deployment record. Never put notification bodies, recipient ids,
provider destinations, or tenant identifiers in a public artifact.

## Select The Owner

Preferred hosted topology:

- `RetentionOwner=Worker`;
- exactly one Worker instance owns retention;
- `Worker:Modules:Notifications=true`;
- `Notifications:Retention:Enabled=true` only on that Worker; and
- API and Admin API set `Notifications:Retention:Enabled=false`.

A self-hosted single-process topology may select `PublicApi` only when
`BunkFy:Deployment:ApiTopology=SingleReplica`. A multi-replica Public API
cannot own retention.

Admin API is never an owner. The Migrations host remains short lived with
delivery and retention workers disabled.

## Configure The Candidate

Supply the same approved admission declaration to every long-running process:

```text
BunkFy__OperationsNotifications__ProductionAdmission__ApprovalState=Approved
BunkFy__OperationsNotifications__ProductionAdmission__ApprovalReference=<private-evidence-id>
BunkFy__OperationsNotifications__ProductionAdmission__CatalogVersion=<approved-version>
BunkFy__OperationsNotifications__ProductionAdmission__CatalogSha256=<approved-lowercase-sha256>
BunkFy__OperationsNotifications__ProductionAdmission__ReadHistoryDays=<approved-days>
BunkFy__OperationsNotifications__ProductionAdmission__UnreadHistoryDays=<approved-days>
BunkFy__OperationsNotifications__ProductionAdmission__BroadcastDays=<approved-days>
BunkFy__OperationsNotifications__ProductionAdmission__DeliveryAttemptDays=<approved-days>
BunkFy__OperationsNotifications__ProductionAdmission__LegacyHistoryDisposition=<approved-disposition>
BunkFy__OperationsNotifications__ProductionAdmission__RetentionOwner=<PublicApi-or-Worker>
BunkFy__OperationsNotifications__ProductionAdmission__RetentionOwnerInstanceCount=1
```

The four values must exactly equal:

```text
Notifications__Retention__ReadHistoryDays
Notifications__Retention__UnreadHistoryDays
Notifications__Retention__BroadcastDays
Notifications__Delivery__AttemptRetentionDays
```

Apply PostgreSQL migrations before starting any candidate. Start non-owner
processes with generic retention disabled, then start the owner with retention
enabled.

## Admission Evidence

Every admitted Production process emits one
`Operations Notifications production admission accepted` record containing
only:

- host role and selected owner;
- declared owner instance count;
- catalogue version and SHA-256;
- bounded approval reference;
- legacy-history disposition; and
- approved numeric windows.

Retain the record from API, Worker, and Admin API with the release evidence.
Absence of the selected owner record blocks promotion. A declaration of one
instance must also be proven by the actual deployment topology.

## Runtime Checks

Before promotion:

1. Run the exact PostgreSQL retention proof for the candidate source.
2. Verify the selected owner starts and remains healthy.
3. Verify the non-owner API and Admin API do not register retention.
4. Route the exact error text
   `Notification retention iteration failed` to the privacy/operations alert
   owner.
5. Verify an alert test reaches that owner without notification content.
6. Inspect delivery backlog and exhausted attempts through the existing GMA
   Notifications Admin API before destructive cleanup is enabled.
7. Record database backup identity and the tested restore point.

The reusable local proof is:

```powershell
dotnet test gma/modules/notifications/tests/Gma.Modules.Notifications.IntegrationTests/Gma.Modules.Notifications.IntegrationTests.csproj --filter 'FullyQualifiedName~Retention_deletes_only_completed_history_and_advances_reference_versions_on_postgresql'
```

The reusable engine protects pending, processing, and retry-scheduled
deliveries, advances open history-reference versions before deleting content,
and retains closure evidence for replay suppression. Do not delete its
reference-state or close-receipt tables by age.

## Pause And Rollback

If policy, legacy-history, replica-count, backup, legal-hold, database, or alert
evidence becomes uncertain:

1. stop the selected owner or redeploy it with
   `Notifications:Retention:Enabled=false`;
2. keep every non-owner disabled;
3. preserve logs, approval evidence, and database state;
4. investigate before changing retention windows or deleting rows manually;
   and
5. re-run admission and the exact PostgreSQL proof before resuming.

Pausing cleanup does not restore already expired content. Restore uses the
approved Data Rights and backup procedures, not direct edits to GMA
Notifications tables.

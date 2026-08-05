# Migrations Host Production Safety

`BunkFy.Host.Migrations` is the only BunkFy executable that advances the
product's PostgreSQL schemas. Module assemblies still own their migrations; the
host owns cross-module deployment safety.

## Modes

`Migrations:Mode` accepts:

- `Plan`: acquire the product migration lock, inspect every module, validate
  history compatibility, report hashes/counts, and exit without mutation;
- `Apply`: perform the same locked inspection, apply pending migrations in the
  fixed product order, verify every module reached its target, and exit.

Development defaults to `Apply` for Aspire/local startup. Production `Apply`
fails closed until its admission is complete. Use `Plan` first for every
Production target.

## Plan Evidence

A Production plan requires the exact deployment profile, runtime, source commit,
and a non-secret database identity. A container also requires its immutable OCI
digest. The plan reports only non-secret evidence:

- SHA-256 of the configured non-secret database alias, without logging the
  operator-facing alias itself;
- database-target SHA-256, derived from the connected data source and database
  name without logging either value;
- target-catalogue SHA-256 over the fixed module order and all loaded migration
  identifiers;
- current-state and pending-plan SHA-256 values;
- per-module target, applied, and pending counts.

The target-catalogue digest is stable while the same release advances or resumes.
The state and pending digests change as migrations commit.

Example environment keys:

```text
DOTNET_ENVIRONMENT=Production
Migrations__Mode=Plan
Migrations__ProductionAdmission__DeploymentProfile=Hosted
Migrations__ProductionAdmission__Runtime=Container
Migrations__ProductionAdmission__SourceCommitSha=<exact-lowercase-commit>
Migrations__ProductionAdmission__ContainerImageDigest=sha256:<exact-image-digest>
Migrations__ProductionAdmission__DatabaseIdentity=prod-primary-eu1
ConnectionStrings__PostgreSql=<secret-provider-value>
```

Do not store the connection string or real infrastructure identifiers in this
repository.

## Apply Admission

Run `Apply` with the same immutable artifact and target after retaining the plan,
review, backup/restore, and rollback/forward-repair evidence. Production requires:

```text
Migrations__Mode=Apply
Migrations__ProductionAdmission__ApprovalState=Approved
Migrations__ProductionAdmission__ApprovalReference=<non-secret-change-reference>
Migrations__ProductionAdmission__ApprovedDatabaseTargetSha256=<plan-database-hash>
Migrations__ProductionAdmission__TargetCatalogVersion=1
Migrations__ProductionAdmission__ApprovedTargetCatalogSha256=<plan-target-hash>
Migrations__ProductionAdmission__BackupEvidenceReference=<non-secret-evidence-reference>
Migrations__ProductionAdmission__RollbackEvidenceReference=<non-secret-evidence-reference>
Migrations__ProductionAdmission__ExistingHistoryDisposition=ApplyCompatiblePrefix
```

The release identity and database identity required for `Plan` remain required
for `Apply`. Evidence references are identifiers, never credentials or evidence
payloads.

## Concurrency And Recovery

One session-scoped PostgreSQL advisory lock covers inspection and all module
migrations. A competing host waits only for the configured acquisition budget.
The host explicitly unlocks the exact key before returning a pooled connection;
physical session termination remains PostgreSQL's fallback after process or
connection failure.

Each module migration commits independently. There is deliberately no fictional
cross-schema transaction. If a run stops after earlier modules commit, rerun the
same approved release and configuration. Exact-prefix validation accepts that
forward progress and applies only the remaining migrations.

Never respond to a partial run by starting an older migration image. Unknown,
reordered, or ahead-of-release history fails before mutation. Use the approved
forward repair or restore procedure when the same release cannot continue.

## Budgets

- `LockAcquireTimeoutSeconds`: total wait for the product advisory lock;
- `LockRetryDelayMilliseconds`: bounded retry interval;
- `OperationTimeoutSeconds`: total process operation budget;
- `CommandTimeoutSeconds`: per-database-command budget.

SIGTERM/host shutdown and the total operation budget cancel inspection and EF
migration work. Raising a budget requires an explicit deployment configuration
change; it does not weaken catalogue or target admission.

## Private Evidence

Before promotion, private operations still owns:

- exact artifact signature/attestation and source-to-image provenance;
- plan log and approval record;
- target-specific backup and tested restore evidence;
- rollback or forward-repair decision and owner;
- maintenance-window, replica-drain, compatibility, and post-deploy checks.

This repository does not prove those external controls merely because their
references pass startup validation.

# retention Personal-Data Inventory v1

Generated from `retention.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| retention-control-plane | tenant-retention-schedule-and-execution-coordinates | permission:retention.read<br>permission:retention.retry<br>system:retention-scheduler | system:retention-scheduler<br>system:retention-scope-projections |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| retention-control-plane-evidence | engineering-default | retention-scope-or-execution-recorded | tenant-termination-and-approved-pseudonymization | retain-minimum-required-schedule-and-execution-evidence |
| transient-owner-execution-request | engineering-default | owner-retention-contribution-requested | owner-retention-contribution-completed | not-applicable |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| retention-scope-coordinate | include-in-authorized-operational-retention-export | correct-through-authoritative-organization-or-property-workflow | stop-scheduling-when-authoritative-scope-is-inactive | remove-or-pseudonymize-after-approved-tenant-termination-policy |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| retention.property-reference | account-holder | linked-operational | elevated | owner-module-execution<br>property-retention-scheduling<br>schedule-health | properties-projection | properties | customer-controller-bunk-fy-processor | retention-control-plane | retention.property-reference | retention-control-plane-evidence | retention-scope-coordinate | api-response<br>application-query<br>integration-command<br>persistence | cross-module<br>customer-api<br>intra-module<br>processor | engineering-default |
| retention.tenant-scope-reference | account-holder | pseudonymous-identifier | elevated | owner-module-execution<br>retention-scope-correlation<br>schedule-health | organizations-projection<br>tenant-execution-context | organizations | customer-controller-bunk-fy-processor | retention-control-plane | retention.tenant-scope-reference | retention-control-plane-evidence | retention-scope-coordinate | application-query<br>integration-command<br>persistence | cross-module<br>intra-module<br>processor | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| retention.property-reference | BunkFy.Modules.Retention.Application | BunkFy.Modules.Retention.Application.Ports.RetentionScheduleTarget | PropertyId | application-query | transient-owner-execution-request |
| retention.property-reference | BunkFy.Modules.Retention.Contracts | BunkFy.Modules.Retention.Contracts.RetentionContributionRequest | PropertyId | integration-command | transient-owner-execution-request |
| retention.property-reference | BunkFy.Modules.Retention.Contracts | BunkFy.Modules.Retention.Contracts.RetentionScheduleHealthDto | PropertyId | api-response | transient-owner-execution-request |
| retention.property-reference | BunkFy.Modules.Retention.Persistence | BunkFy.Modules.Retention.Persistence.RetentionPropertyProjection | Id | persistence | retention-control-plane-evidence |
| retention.property-reference | BunkFy.Modules.Retention.Persistence | BunkFy.Modules.Retention.Persistence.RetentionScheduleState | PropertyId | persistence | retention-control-plane-evidence |
| retention.tenant-scope-reference | BunkFy.Modules.Retention.Application | BunkFy.Modules.Retention.Application.Ports.RetentionScheduleTarget | ScopeId | application-query | transient-owner-execution-request |
| retention.tenant-scope-reference | BunkFy.Modules.Retention.Contracts | BunkFy.Modules.Retention.Contracts.RetentionContributionRequest | TenantId | integration-command | transient-owner-execution-request |
| retention.tenant-scope-reference | BunkFy.Modules.Retention.Persistence | BunkFy.Modules.Retention.Persistence.RetentionScheduleState | ScopeId | persistence | retention-control-plane-evidence |
| retention.tenant-scope-reference | BunkFy.Modules.Retention.Persistence | BunkFy.Modules.Retention.Persistence.RetentionTenantProjection | ScopeId | persistence | retention-control-plane-evidence |

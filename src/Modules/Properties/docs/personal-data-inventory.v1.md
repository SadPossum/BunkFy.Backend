# properties Personal-Data Inventory v3

Generated from `properties.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| properties-authorization | tenant-property-access-evaluation | system:properties-access-resolution | system:authenticated-access-context |
| properties-lifecycle-audit | tenant-property-lifecycle-audit | permission:properties.properties.manage<br>system:authorized-audit-consumer<br>system:operations-notifications | permission:properties.properties.manage |
| properties-tenant-destruction-proof | tenant-termination-control-and-accountability-proof | permission:data-rights.tenant-termination<br>system:authorized-audit-consumer<br>system:tenant-termination-coordinator | system:authorized-tenant-termination-owner |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| integration-message-journal | engineering-default | message-created | message-journal-retention-completed | no-payload-hold |
| properties-tenant-destruction-proof | engineering-default | approved-properties-tenant-destruction-started | approved-minimum-termination-proof-retention-completed | retain-minimum-required-termination-proof |
| properties-tenant-termination-export-fragment | engineering-default | tenant-export-fragment-generation | tenant-export-artifact-expiry-or-deletion | retain-only-with-authorized-tenant-export-artifact |
| property-governance-revision-lifecycle | engineering-default | property-governance-decision-recorded | tenant-termination-or-approved-pseudonymization | retain-minimum-required-audit-evidence |
| transient-domain-event | engineering-default | domain-event-raised | domain-event-dispatched | not-applicable |
| transient-request | engineering-default | request-accepted | request-completed | not-applicable |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| authorization-subject | not-retained-by-properties | correct-through-authoritative-auth-workflow | deny-property-access-when-subject-access-is-restricted | not-retained-by-properties |
| properties-tenant-destruction-proof-control | include-in-authorized-tenant-termination-audit | append-a-corrective-control-plane-record | retain-only-the-minimum-owner-proof | remove-after-approved-proof-retention |
| staff-audit-attribution | include-in-authorized-staff-audit-or-tenant-export | append-corrective-property-lifecycle-action | retain-minimum-required-audit-and-notification-processing | pseudonymize-subject-when-approved-retention-permits |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| properties.authorization-subject-reference | account-holder | pseudonymous-identifier | elevated | authorization-scope-resolution | authenticated-access-subject | auth | customer-controller-bunk-fy-processor | properties-authorization | properties.authorization-subject-reference | transient-request | authorization-subject | application-query | intra-module | engineering-default |
| properties.staff-actor-reference | staff | audit-attribution | standard | property-lifecycle-audit-correlation<br>self-notification-suppression | authenticated-access-subject | auth | customer-controller-bunk-fy-processor | properties-lifecycle-audit | properties.staff-actor-reference | integration-message-journal | staff-audit-attribution | application-command<br>data-rights-export<br>domain-event<br>integration-event<br>persistence | cross-module<br>intra-module | engineering-default |
| properties.tenant-destruction.owner-proof | subject-scoped | linked-operational | elevated | destruction-accountability<br>owner-proof-correlation<br>tenant-termination-safety | approved-tenant-termination-case<br>properties-owner-destruction | properties | customer-controller-bunk-fy-processor | properties-tenant-destruction-proof | properties.tenant-destruction.owner-proof | properties-tenant-destruction-proof | properties-tenant-destruction-proof-control | persistence | intra-module<br>processor | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| properties.authorization-subject-reference | BunkFy.Modules.Properties.Application | BunkFy.Modules.Properties.Application.Queries.ListVisiblePropertiesQuery | Subject | application-query | transient-request |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Application | BunkFy.Modules.Properties.Application.Commands.ActivatePropertyProcessingCommand | ActorId | application-command | transient-request |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Application | BunkFy.Modules.Properties.Application.Commands.RetirePropertyCommand | ActorId | application-command | transient-request |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Application | BunkFy.Modules.Properties.Application.Commands.SuspendPropertyProcessingCommand | ActorId | application-command | transient-request |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Application | BunkFy.Modules.Properties.Application.Ports.PropertyGovernanceRevisionWriteModel | ActorId | application-command | transient-request |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Contracts | BunkFy.Modules.Properties.Contracts.PropertyRetiredIntegrationEvent | ActorId | integration-event | integration-message-journal |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Domain | BunkFy.Modules.Properties.Domain.Events.PropertyProcessingPolicyActivatedDomainEvent | ActorId | domain-event | transient-domain-event |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Domain | BunkFy.Modules.Properties.Domain.Events.PropertyProcessingSuspendedDomainEvent | ActorId | domain-event | transient-domain-event |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Domain | BunkFy.Modules.Properties.Domain.Events.PropertyRetiredDomainEvent | ActorId | domain-event | transient-domain-event |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.PropertyGovernanceRevision | ActorId | persistence | property-governance-revision-lifecycle |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.Repositories.PropertiesGovernanceRevisionTenantExport | ActorId | data-rights-export | properties-tenant-termination-export-fragment |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | BatchSize | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | CompletedBatchCount | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | ConcurrencyVersion | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | OperationId | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | ProofVersion | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | RemovalProofSha256 | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | RemovedRecordCount | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | RequestSha256 | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | ResultingRevision | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | ScopeId | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | SelectedRevision | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | Stage | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | StartedAtUtc | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyOperation | UpdatedAtUtc | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | BatchSize | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | CompletedAtUtc | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | CompletedBatchCount | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | OperationId | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | RemovalProofSha256 | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | RemovalProofVersion | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | RemovedRecordCount | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | RequestSha256 | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | ResultingRevision | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | ScopeId | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | SelectedRevision | persistence | properties-tenant-destruction-proof |
| properties.tenant-destruction.owner-proof | BunkFy.Modules.Properties.Persistence | BunkFy.Modules.Properties.Persistence.TenantTermination.PropertiesTenantDestroyReceipt | StartedAtUtc | persistence | properties-tenant-destruction-proof |

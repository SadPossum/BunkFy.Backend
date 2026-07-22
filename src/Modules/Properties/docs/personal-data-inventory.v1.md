# properties Personal-Data Inventory v1

Generated from `properties.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| properties-authorization | tenant-property-access-evaluation | system:properties-access-resolution | system:authenticated-access-context |
| properties-lifecycle-audit | tenant-property-lifecycle-audit | permission:properties.properties.manage<br>system:authorized-audit-consumer<br>system:operations-notifications | permission:properties.properties.manage |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| integration-message-journal | engineering-default | message-created | message-journal-retention-completed | no-payload-hold |
| transient-domain-event | engineering-default | domain-event-raised | domain-event-dispatched | not-applicable |
| transient-request | engineering-default | request-accepted | request-completed | not-applicable |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| authorization-subject | not-retained-by-properties | correct-through-authoritative-auth-workflow | deny-property-access-when-subject-access-is-restricted | not-retained-by-properties |
| staff-audit-attribution | include-in-authorized-staff-audit-export | append-corrective-property-lifecycle-action | retain-minimum-required-audit-and-notification-processing | pseudonymize-subject-when-approved-retention-permits |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| properties.authorization-subject-reference | account-holder | pseudonymous-identifier | elevated | authorization-scope-resolution | authenticated-access-subject | auth | customer-controller-bunk-fy-processor | properties-authorization | properties.authorization-subject-reference | transient-request | authorization-subject | application-query | intra-module | engineering-default |
| properties.staff-actor-reference | staff | audit-attribution | standard | property-lifecycle-audit-correlation<br>self-notification-suppression | authenticated-access-subject | auth | customer-controller-bunk-fy-processor | properties-lifecycle-audit | properties.staff-actor-reference | integration-message-journal | staff-audit-attribution | application-command<br>domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| properties.authorization-subject-reference | BunkFy.Modules.Properties.Application | BunkFy.Modules.Properties.Application.Queries.ListVisiblePropertiesQuery | Subject | application-query | transient-request |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Application | BunkFy.Modules.Properties.Application.Commands.RetirePropertyCommand | ActorId | application-command | transient-request |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Contracts | BunkFy.Modules.Properties.Contracts.PropertyRetiredIntegrationEvent | ActorId | integration-event | integration-message-journal |
| properties.staff-actor-reference | BunkFy.Modules.Properties.Domain | BunkFy.Modules.Properties.Domain.Events.PropertyRetiredDomainEvent | ActorId | domain-event | transient-domain-event |

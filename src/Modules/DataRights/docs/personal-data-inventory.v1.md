# data-rights Personal-Data Inventory v1

Generated from `data-rights.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| data-rights-case-audit | tenant-property-data-rights-case | permission:data-rights.cases.manage<br>permission:data-rights.cases.read<br>system:authorized-audit-consumer | permission:data-rights.cases.create<br>permission:data-rights.cases.discover<br>permission:data-rights.cases.manage<br>permission:data-rights.cases.review |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| data-rights-case-lifecycle | engineering-default | data-rights-case-created | approved-case-retention-completed-or-tenant-termination | retain-minimum-required-audit-evidence |
| transient-request | engineering-default | request-accepted | request-completed | not-applicable |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| staff-audit-attribution | include-in-authorized-staff-audit-export | append-corrective-case-action | retain-minimum-required-audit-attribution | pseudonymize-subject-when-approved-retention-permits |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| data-rights.staff-actor-reference | staff | audit-attribution | standard | authorized-change-traceability<br>case-accountability | authenticated-access-subject | auth | customer-controller-bunk-fy-processor | data-rights-case-audit | staff.audit-attribution | data-rights-case-lifecycle | staff-audit-attribution | application-command<br>persistence | intra-module | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.BeginDataRightsDiscoveryCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.CancelDataRightsCaseCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.CreateDataRightsCaseCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.RecordControllerRoutingCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.RecordRequesterVerificationCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.RequireDataRightsReviewCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Aggregates.DataRightsCase | CreatedBy | persistence | data-rights-case-lifecycle |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Aggregates.DataRightsCase | LastChangedBy | persistence | data-rights-case-lifecycle |

# data-rights Personal-Data Inventory v5

Generated from `data-rights.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| data-rights-case-audit | tenant-property-data-rights-case | permission:data-rights.cases.manage<br>permission:data-rights.cases.read<br>system:authorized-audit-consumer | permission:data-rights.cases.create<br>permission:data-rights.cases.discover<br>permission:data-rights.cases.manage<br>permission:data-rights.cases.review |
| data-rights-owner-export | tenant-property-selected-subject | system:data-rights.export-assembler | system:authorized-owner-export |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| data-rights-case-lifecycle | engineering-default | data-rights-case-created | approved-case-retention-completed-or-tenant-termination | retain-minimum-required-audit-evidence |
| transient-owner-export-fragment | engineering-default | owner-export-started | 01:00:00 | not-applicable |
| transient-request | engineering-default | request-accepted | request-completed | not-applicable |
| transient-response | engineering-default | response-created | response-completed | not-applicable |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| guest-subject-discovery | include-selected-coordinates-in-authorized-case-export | re-run-discovery-against-authoritative-owner | exclude-coordinate-from-selection-and-downstream-work | remove-selected-coordinate-when-approved-case-retention-permits |
| selected-owner-export | include-in-authorized-subject-export | route-to-authoritative-owner | discard-uncommitted-export-fragment | expire-export-fragment |
| staff-audit-attribution | include-in-authorized-staff-audit-export | append-corrective-case-action | retain-minimum-required-audit-attribution | pseudonymize-subject-when-approved-retention-permits |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| data-rights.guest-discovery-date-of-birth | guest | demographic | elevated | authorized-subject-discovery<br>candidate-disambiguation | request-input | guests | customer-controller-bunk-fy-processor | data-rights-case-audit | guest.profile.demographic | transient-request | guest-subject-discovery | api-input<br>application-query | cross-module<br>customer-api<br>intra-module | engineering-default |
| data-rights.guest-discovery-email | guest | contact | elevated | authorized-subject-discovery<br>candidate-disambiguation | guests-authoritative-owner<br>request-input | guests | customer-controller-bunk-fy-processor | data-rights-case-audit | guest.profile.contact | transient-request | guest-subject-discovery | api-input<br>api-response<br>application-query | cross-module<br>customer-api<br>intra-module | engineering-default |
| data-rights.guest-discovery-name | guest | direct-identifier | standard | authorized-subject-discovery<br>candidate-disambiguation | guests-authoritative-owner<br>request-input | guests | customer-controller-bunk-fy-processor | data-rights-case-audit | guest.profile.identity | transient-request | guest-subject-discovery | api-input<br>api-response<br>application-query | cross-module<br>customer-api<br>intra-module | engineering-default |
| data-rights.guest-discovery-phone | guest | contact | elevated | authorized-subject-discovery<br>candidate-disambiguation | guests-authoritative-owner<br>request-input | guests | customer-controller-bunk-fy-processor | data-rights-case-audit | guest.profile.contact | transient-request | guest-subject-discovery | api-input<br>api-response<br>application-query | cross-module<br>customer-api<br>intra-module | engineering-default |
| data-rights.guest-subject-record-id | guest | linked-operational | standard | authorized-subject-discovery<br>case-subject-selection | guests-authoritative-owner<br>request-input | guests | customer-controller-bunk-fy-processor | data-rights-case-audit | guest.profile.identifier | data-rights-case-lifecycle | guest-subject-discovery | api-input<br>api-response<br>application-query<br>persistence | cross-module<br>customer-api<br>intra-module | engineering-default |
| data-rights.guest-subject-selected-at | guest | lifecycle | standard | authorized-change-traceability<br>case-subject-selection | operator-selection | data-rights | customer-controller-bunk-fy-processor | data-rights-case-audit | guest.profile.lifecycle | data-rights-case-lifecycle | guest-subject-discovery | api-response<br>persistence | customer-api<br>intra-module | engineering-default |
| data-rights.owner-export-envelope | subject-scoped | structured-payload | unstructured | authorized-subject-export<br>protected-export-assembly | authorized-owner-export | selected-owner-module | customer-controller-bunk-fy-processor | data-rights-owner-export | data-rights.owner-export | transient-owner-export-fragment | selected-owner-export | data-rights-export | cross-module | engineering-default |
| data-rights.staff-actor-reference | staff | audit-attribution | standard | authorized-change-traceability<br>case-accountability | authenticated-access-subject | auth | customer-controller-bunk-fy-processor | data-rights-case-audit | staff.audit-attribution | data-rights-case-lifecycle | staff-audit-attribution | application-command<br>application-query<br>persistence | cross-module<br>intra-module | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| data-rights.guest-discovery-date-of-birth | BunkFy.Modules.DataRights.Api | BunkFy.Modules.DataRights.Api.DataRightsDiscoveryEndpoints+DiscoverDataRightsSubjectsRequest | DateOfBirth | api-input | transient-request |
| data-rights.guest-discovery-date-of-birth | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectLookup | DateOfBirth | application-query | transient-request |
| data-rights.guest-discovery-email | BunkFy.Modules.DataRights.Api | BunkFy.Modules.DataRights.Api.DataRightsDiscoveryEndpoints+DiscoverDataRightsSubjectsRequest | Email | api-input | transient-request |
| data-rights.guest-discovery-email | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectCandidate | EmailHint | api-response | transient-response |
| data-rights.guest-discovery-email | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectLookup | Email | application-query | transient-request |
| data-rights.guest-discovery-name | BunkFy.Modules.DataRights.Api | BunkFy.Modules.DataRights.Api.DataRightsDiscoveryEndpoints+DiscoverDataRightsSubjectsRequest | Name | api-input | transient-request |
| data-rights.guest-discovery-name | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectCandidate | DisplayName | api-response | transient-response |
| data-rights.guest-discovery-name | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectLookup | Name | application-query | transient-request |
| data-rights.guest-discovery-phone | BunkFy.Modules.DataRights.Api | BunkFy.Modules.DataRights.Api.DataRightsDiscoveryEndpoints+DiscoverDataRightsSubjectsRequest | Phone | api-input | transient-request |
| data-rights.guest-discovery-phone | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectCandidate | PhoneHint | api-response | transient-response |
| data-rights.guest-discovery-phone | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectLookup | Phone | application-query | transient-request |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Api | BunkFy.Modules.DataRights.Api.DataRightsDiscoveryEndpoints+DiscoverDataRightsSubjectsRequest | RecordId | api-input | transient-request |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.Authorization.DataRightsOperationApprovalRequest | RecordId | application-query | transient-request |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsExecutionWorkItemDto | RecordId | api-response | transient-response |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSelectedSubjectDto | RecordId | api-response | transient-response |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectCoordinate | RecordId | api-input | transient-request |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectCoordinate | RecordId | api-response | transient-response |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectCoordinateKey | RecordId | api-input | transient-request |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSubjectLookup | RecordId | application-query | transient-request |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Aggregates.DataRightsExecutionWorkItem | RecordId | persistence | data-rights-case-lifecycle |
| data-rights.guest-subject-record-id | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate | RecordId | persistence | data-rights-case-lifecycle |
| data-rights.guest-subject-selected-at | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsSelectedSubjectDto | SelectedAtUtc | api-response | transient-response |
| data-rights.guest-subject-selected-at | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate | SelectedAtUtc | persistence | data-rights-case-lifecycle |
| data-rights.owner-export-envelope | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsExportField | FieldId | data-rights-export | transient-owner-export-fragment |
| data-rights.owner-export-envelope | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsExportField | Value | data-rights-export | transient-owner-export-fragment |
| data-rights.owner-export-envelope | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsExportRecord | Fields | data-rights-export | transient-owner-export-fragment |
| data-rights.owner-export-envelope | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsExportRecord | RecordId | data-rights-export | transient-owner-export-fragment |
| data-rights.owner-export-envelope | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsExportRecord | RecordType | data-rights-export | transient-owner-export-fragment |
| data-rights.owner-export-envelope | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.DataRightsExportRecord | RecordVersion | data-rights-export | transient-owner-export-fragment |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.BeginDataRightsDecisionCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.BeginDataRightsDiscoveryCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.CancelDataRightsCaseCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.CreateDataRightsCaseCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.RecordControllerRoutingCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.RecordDataRightsDecisionCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.RecordRequesterVerificationCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.RequireDataRightsReviewCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.SelectDataRightsSubjectCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.StartDataRightsAnonymisationExecutionCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Application | BunkFy.Modules.DataRights.Application.Commands.UnselectDataRightsSubjectCommand | ActorId | application-command | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Contracts | BunkFy.Modules.DataRights.Contracts.Authorization.DataRightsOperationApprovalRequest | ExecutingActorId | application-query | transient-request |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Aggregates.DataRightsCase | CreatedBy | persistence | data-rights-case-lifecycle |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Aggregates.DataRightsCase | DecidedBy | persistence | data-rights-case-lifecycle |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Aggregates.DataRightsCase | ExecutionStartedBy | persistence | data-rights-case-lifecycle |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Aggregates.DataRightsCase | LastChangedBy | persistence | data-rights-case-lifecycle |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Aggregates.DataRightsExecutionWorkItem | CreatedBy | persistence | data-rights-case-lifecycle |
| data-rights.staff-actor-reference | BunkFy.Modules.DataRights.Domain | BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate | SelectedBy | persistence | data-rights-case-lifecycle |

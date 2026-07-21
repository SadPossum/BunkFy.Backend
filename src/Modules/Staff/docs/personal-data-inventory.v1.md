# staff Personal-Data Inventory v1

Generated from `staff.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| staff-audit | tenant-internal-audit | permission:staff.sensitive-profile.read<br>system:authorized-audit-consumer | system:staff-domain |
| staff-directory | tenant-property-authorized | permission:staff.read<br>system:staff-authorized-audience-reader | permission:staff.assign-properties<br>permission:staff.create<br>permission:staff.manage<br>permission:staff.manage-lifecycle<br>system:workspace-staff-onboarding |
| staff-directory-request | tenant-property-authorized-request | permission:staff.read | permission:staff.read |
| staff-event-metadata | tenant-internal-messaging | system:authorized-module-consumer | system:staff-outbox |
| staff-internal | tenant-internal | system:staff-projection-runtime | system:staff-projection-runtime |
| staff-sensitive-profile | tenant-sensitive-profile | permission:staff.sensitive-profile.read<br>subject:self<br>system:workspace-staff-onboarding | permission:staff.create<br>permission:staff.manage<br>subject:self<br>system:workspace-staff-onboarding |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| integration-message-journal | engineering-default | message-created | message-journal-retention-completed | no-payload-hold |
| staff-assignment-history | engineering-default | staff-assignment-created | approved-erasure-or-employment-retention-completed | pause-approved-erasure |
| staff-profile-lifecycle | engineering-default | staff-profile-created | approved-erasure-or-employment-retention-completed | pause-approved-erasure |
| transient-request | engineering-default | request-accepted | request-completed | not-applicable |
| transient-response | engineering-default | response-created | response-completed | not-applicable |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| staff-audit-attribution | include-in-authorized-staff-export | correct-authoritative-account-source | retain-minimum-required-audit-attribution | pseudonymize-subject-to-minimum-audit-receipt |
| staff-employment-history | include-in-authorized-staff-export | append-corrective-employment-action | suppress-non-required-operational-use | anonymize-or-delete-after-approved-employment-retention |
| staff-profile-editable | include-in-authorized-staff-export | replace-through-staff-profile-workflow | suppress-non-required-operational-use | irreversibly-anonymize-or-delete |
| transient-request-data | not-retained-after-request | replace-before-submission | discard-request | discard-on-request-completion |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| staff.assignment-change-reason | staff | free-text | unstructured | assignment-change-context | staff-entry | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.assignment.reason | staff-assignment-history | staff-employment-history | admin-input<br>api-input<br>application-command<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.assignment-id | staff | linked-operational | standard | assignment-linkage | system-generated | staff | customer-controller-bunk-fy-processor | staff-directory | staff.assignment.identifier | staff-assignment-history | staff-employment-history | admin-output<br>api-response | customer-api<br>support | engineering-default |
| staff.assignment-state | staff | lifecycle | standard | assignment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-directory | staff.assignment.lifecycle | staff-assignment-history | staff-employment-history | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.assignment-timestamps | staff | lifecycle | standard | assignment-audit | system-generated | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.assignment.audit | staff-assignment-history | staff-employment-history | admin-output<br>api-response<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.assignment-version | staff | linked-operational | standard | assignment-audit | system-generated | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.assignment.audit | staff-assignment-history | staff-employment-history | admin-output<br>api-response<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.assignments | staff | linked-operational | standard | staff-assignment-presentation | system-derived | staff | customer-controller-bunk-fy-processor | staff-directory | staff.assignment.collection | transient-response | staff-employment-history | admin-output<br>api-response | customer-api<br>support | engineering-default |
| staff.audit-actor-id | staff | audit-attribution | standard | accountability | authenticated-subject | staff | customer-controller-bunk-fy-processor | staff-audit | staff.audit-attribution | staff-profile-lifecycle | staff-audit-attribution | application-command<br>integration-command<br>persistence | cross-module<br>intra-module | engineering-default |
| staff.auth-subject-id | staff | pseudonymous-identifier | elevated | account-linkage | auth-owned-identity | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.profile.account-link | staff-profile-lifecycle | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>application-query<br>integration-command<br>persistence | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.change-reason | staff | free-text | unstructured | employment-change-context | staff-entry | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.change.reason | staff-profile-lifecycle | staff-employment-history | admin-input<br>api-input<br>application-command<br>integration-command | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.department | staff | linked-operational | standard | operational-directory | staff-entry | staff | customer-controller-bunk-fy-processor | staff-directory | staff.profile.job | staff-profile-lifecycle | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>integration-command<br>persistence | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.departure-effective-on | staff | lifecycle | standard | employment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.profile.lifecycle | staff-profile-lifecycle | staff-employment-history | admin-input<br>api-input<br>application-command<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.directory-items | staff | linked-operational | standard | staff-directory-presentation | system-derived | staff | customer-controller-bunk-fy-processor | staff-directory | staff.directory.collection | transient-response | staff-profile-editable | admin-output<br>api-response | customer-api<br>support | engineering-default |
| staff.display-name | staff | direct-identifier | standard | staff-identification | staff-entry | staff | customer-controller-bunk-fy-processor | staff-directory | staff.profile.identity | staff-profile-lifecycle | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>integration-command<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.effective-from | staff | lifecycle | standard | assignment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-directory | staff.assignment.lifecycle | staff-assignment-history | staff-employment-history | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.effective-to | staff | lifecycle | standard | assignment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.assignment.lifecycle | staff-assignment-history | staff-employment-history | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.employee-number | staff | pseudonymous-identifier | elevated | employment-administration | staff-entry | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.profile.employee-identifier | staff-profile-lifecycle | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>integration-command<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.event.actor-id | staff | audit-attribution | standard | accountability | authenticated-subject | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.audit-attribution | integration-message-journal | staff-audit-attribution | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.assignment-id | staff | linked-operational | standard | assignment-linkage | system-generated | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.auth-subject-id | staff | pseudonymous-identifier | elevated | account-linkage | auth-owned-identity | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.effective-from | staff | lifecycle | standard | assignment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.effective-on | staff | lifecycle | standard | employment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.effective-to | staff | lifecycle | standard | assignment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.event-id | staff | linked-operational | standard | idempotent-event-processing | system-generated | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.is-current | staff | lifecycle | standard | assignment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.is-primary | staff | linked-operational | standard | primary-assignment-selection | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.occurred-at-utc | staff | lifecycle | standard | event-ordering | system-generated | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.property-id | staff | linked-operational | standard | property-assignment | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.scope-id | staff | linked-operational | standard | tenant-isolation | system-derived | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.staff-member-id | staff | pseudonymous-identifier | standard | staff-record-linkage | system-generated | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.staff-version | staff | linked-operational | standard | projection-consistency | system-generated | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.event.status | staff | lifecycle | standard | employment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-event-metadata | staff.event.metadata | integration-message-journal | staff-employment-history | domain-event<br>integration-event | cross-module<br>intra-module | engineering-default |
| staff.job-title | staff | linked-operational | standard | operational-directory | staff-entry | staff | customer-controller-bunk-fy-processor | staff-directory | staff.profile.job | staff-profile-lifecycle | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>integration-command<br>persistence | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.legal-name | staff | direct-identifier | elevated | employment-administration | staff-entry | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.profile.legal-identity | staff-profile-lifecycle | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>integration-command<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.profile-timestamps | staff | lifecycle | standard | employment-lifecycle | system-generated | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.profile.lifecycle | staff-profile-lifecycle | staff-employment-history | admin-output<br>api-response<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.projection-ordinal | staff | linked-operational | standard | projection-ordering | system-generated | staff | customer-controller-bunk-fy-processor | staff-internal | staff.projection.order | staff-profile-lifecycle | staff-employment-history | persistence | intra-module | engineering-default |
| staff.property-id | staff | linked-operational | standard | property-assignment | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-directory | staff.assignment.property | staff-assignment-history | staff-employment-history | admin-output<br>api-response<br>application-command<br>application-query<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.property-job-title | staff | linked-operational | standard | property-operations | staff-entry | staff | customer-controller-bunk-fy-processor | staff-directory | staff.assignment.job | staff-assignment-history | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.record-version | staff | linked-operational | standard | optimistic-concurrency | system-generated | staff | customer-controller-bunk-fy-processor | staff-directory | staff.profile.version | staff-profile-lifecycle | staff-employment-history | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>persistence | customer-api<br>intra-module<br>support | engineering-default |
| staff.scope-id | staff | linked-operational | standard | tenant-isolation | system-derived | staff | customer-controller-bunk-fy-processor | staff-directory | staff.scope.identifier | staff-profile-lifecycle | staff-employment-history | persistence | intra-module | engineering-default |
| staff.search-input | staff | search-input | unstructured | staff-directory-search | staff-entry | staff | customer-controller-bunk-fy-processor | staff-directory-request | staff.search.input | transient-request | transient-request-data | application-query | intra-module | engineering-default |
| staff.staff-member-id | staff | pseudonymous-identifier | standard | staff-record-linkage | system-generated | staff | customer-controller-bunk-fy-processor | staff-directory | staff.profile.identifier | staff-profile-lifecycle | staff-profile-editable | admin-output<br>api-response<br>application-command<br>application-query<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.status | staff | lifecycle | standard | employment-lifecycle | staff-workflow | staff | customer-controller-bunk-fy-processor | staff-directory | staff.profile.lifecycle | staff-profile-lifecycle | staff-employment-history | admin-output<br>api-response<br>application-command<br>application-query<br>integration-command<br>persistence | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.work-email | staff | contact | elevated | staff-contact | staff-entry | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.profile.contact | staff-profile-lifecycle | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>integration-command<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |
| staff.work-phone | staff | contact | elevated | staff-contact | staff-entry | staff | customer-controller-bunk-fy-processor | staff-sensitive-profile | staff.profile.contact | staff-profile-lifecycle | staff-profile-editable | admin-input<br>admin-output<br>api-input<br>api-response<br>application-command<br>integration-command<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module<br>support | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| staff.assignment-change-reason | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffUnassignmentRequest | Reason | admin-input | transient-request |
| staff.assignment-change-reason | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffUnassignmentRequest | Reason | api-input | transient-request |
| staff.assignment-change-reason | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UnassignStaffPropertyCommand | Reason | application-command | transient-request |
| staff.assignment-change-reason | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | UnassignmentReason | persistence | staff-assignment-history |
| staff.assignment-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | AssignmentId | api-response | transient-response |
| staff.assignment-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | AssignmentId | admin-output | transient-response |
| staff.assignment-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | AssignmentId | api-response | transient-response |
| staff.assignment-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | AssignmentId | admin-output | transient-response |
| staff.assignment-state | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffAssignmentRequest | IsPrimary | admin-input | transient-request |
| staff.assignment-state | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffAssignmentRequest | IsPrimary | api-input | transient-request |
| staff.assignment-state | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.AssignStaffPropertyCommand | IsPrimary | application-command | transient-request |
| staff.assignment-state | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | IsPrimary | api-response | transient-response |
| staff.assignment-state | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | IsPrimary | admin-output | transient-response |
| staff.assignment-state | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | IsCurrent | api-response | transient-response |
| staff.assignment-state | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | IsCurrent | admin-output | transient-response |
| staff.assignment-state | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | IsPrimary | api-response | transient-response |
| staff.assignment-state | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | IsPrimary | admin-output | transient-response |
| staff.assignment-state | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | IsCurrent | persistence | staff-assignment-history |
| staff.assignment-state | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | IsPrimary | persistence | staff-assignment-history |
| staff.assignment-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | AssignedAtUtc | api-response | transient-response |
| staff.assignment-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | AssignedAtUtc | admin-output | transient-response |
| staff.assignment-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | UnassignedAtUtc | api-response | transient-response |
| staff.assignment-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | UnassignedAtUtc | admin-output | transient-response |
| staff.assignment-timestamps | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | AssignedAtUtc | persistence | staff-assignment-history |
| staff.assignment-timestamps | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | UnassignedAtUtc | persistence | staff-assignment-history |
| staff.assignment-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | AssignedAtVersion | api-response | transient-response |
| staff.assignment-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | AssignedAtVersion | admin-output | transient-response |
| staff.assignment-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | UnassignedAtVersion | api-response | transient-response |
| staff.assignment-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | UnassignedAtVersion | admin-output | transient-response |
| staff.assignment-version | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | AssignedAtVersion | persistence | staff-assignment-history |
| staff.assignment-version | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | UnassignedAtVersion | persistence | staff-assignment-history |
| staff.assignments | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | Assignments | api-response | transient-response |
| staff.assignments | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | Assignments | admin-output | transient-response |
| staff.assignments | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | Assignments | api-response | transient-response |
| staff.assignments | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | Assignments | admin-output | transient-response |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.AssignStaffPropertyCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.DepartStaffMemberCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ReconcileStaffIdentityCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ResumeStaffMemberCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.SetStaffAuthSubjectCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.SuspendStaffMemberCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UnassignStaffPropertyCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | ActorId | application-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffIdentityReconciliationRequest | ActorId | integration-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | ActorId | integration-command | transient-request |
| staff.audit-actor-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | CreatedBy | persistence | staff-profile-lifecycle |
| staff.audit-actor-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | LastChangedBy | persistence | staff-profile-lifecycle |
| staff.audit-actor-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | AssignedBy | persistence | staff-profile-lifecycle |
| staff.audit-actor-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | UnassignedBy | persistence | staff-profile-lifecycle |
| staff.auth-subject-id | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffAuthSubjectRequest | AuthSubjectId | admin-input | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileWriteRequest | AuthSubjectId | admin-input | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffAuthSubjectRequest | AuthSubjectId | api-input | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileWriteRequest | AuthSubjectId | api-input | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | AuthSubjectId | application-command | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | AuthSubjectId | application-command | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ReconcileStaffIdentityCommand | AuthSubjectId | application-command | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.SetStaffAuthSubjectCommand | AuthSubjectId | application-command | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | AuthSubjectId | application-command | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.GetCurrentStaffMemberQuery | AuthSubjectId | application-query | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffIdentityReconciliationRequest | AuthSubjectId | integration-command | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | AuthSubjectId | api-response | transient-response |
| staff.auth-subject-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | AuthSubjectId | admin-output | transient-response |
| staff.auth-subject-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | AuthSubjectId | integration-command | transient-request |
| staff.auth-subject-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | AuthSubjectId | persistence | staff-profile-lifecycle |
| staff.change-reason | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffDepartureRequest | Reason | admin-input | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffLifecycleRequest | Reason | admin-input | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffDepartureRequest | Reason | api-input | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffLifecycleRequest | Reason | api-input | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.DepartStaffMemberCommand | Reason | application-command | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | Reason | application-command | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ReconcileStaffIdentityCommand | Reason | application-command | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ResumeStaffMemberCommand | Reason | application-command | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.SuspendStaffMemberCommand | Reason | application-command | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffIdentityReconciliationRequest | Reason | integration-command | transient-request |
| staff.change-reason | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | Reason | integration-command | transient-request |
| staff.department | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileUpdateRequest | Department | admin-input | transient-request |
| staff.department | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileWriteRequest | Department | admin-input | transient-request |
| staff.department | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileUpdateRequest | Department | api-input | transient-request |
| staff.department | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileWriteRequest | Department | api-input | transient-request |
| staff.department | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | Department | application-command | transient-request |
| staff.department | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | Department | application-command | transient-request |
| staff.department | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | Department | application-command | transient-request |
| staff.department | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | Department | application-command | transient-request |
| staff.department | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | Department | api-response | transient-response |
| staff.department | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | Department | admin-output | transient-response |
| staff.department | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | Department | api-response | transient-response |
| staff.department | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | Department | admin-output | transient-response |
| staff.department | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | Department | integration-command | transient-request |
| staff.department | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | Department | persistence | staff-profile-lifecycle |
| staff.departure-effective-on | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffDepartureRequest | EffectiveOn | admin-input | transient-request |
| staff.departure-effective-on | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffDepartureRequest | EffectiveOn | api-input | transient-request |
| staff.departure-effective-on | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.DepartStaffMemberCommand | EffectiveOn | application-command | transient-request |
| staff.departure-effective-on | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | DepartureEffectiveOn | persistence | staff-profile-lifecycle |
| staff.directory-items | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryListResponse | Items | api-response | transient-response |
| staff.directory-items | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryListResponse | Items | admin-output | transient-response |
| staff.display-name | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileUpdateRequest | DisplayName | admin-input | transient-request |
| staff.display-name | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileWriteRequest | DisplayName | admin-input | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileUpdateRequest | DisplayName | api-input | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileWriteRequest | DisplayName | api-input | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | DisplayName | application-command | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | DisplayName | application-command | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ReconcileStaffIdentityCommand | DisplayName | application-command | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | DisplayName | application-command | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | DisplayName | application-command | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | DisplayName | api-response | transient-response |
| staff.display-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | DisplayName | admin-output | transient-response |
| staff.display-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffIdentityReconciliationRequest | DisplayName | integration-command | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | DisplayName | api-response | transient-response |
| staff.display-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | DisplayName | admin-output | transient-response |
| staff.display-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | DisplayName | integration-command | transient-request |
| staff.display-name | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | DisplayName | persistence | staff-profile-lifecycle |
| staff.display-name | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | DisplayNameSearch | persistence | staff-profile-lifecycle |
| staff.display-name | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | DisplayNameSearch | search-index | staff-profile-lifecycle |
| staff.effective-from | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffAssignmentRequest | EffectiveFrom | admin-input | transient-request |
| staff.effective-from | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffAssignmentRequest | EffectiveFrom | api-input | transient-request |
| staff.effective-from | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.AssignStaffPropertyCommand | EffectiveFrom | application-command | transient-request |
| staff.effective-from | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | EffectiveFrom | api-response | transient-response |
| staff.effective-from | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | EffectiveFrom | admin-output | transient-response |
| staff.effective-from | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | EffectiveFrom | api-response | transient-response |
| staff.effective-from | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | EffectiveFrom | admin-output | transient-response |
| staff.effective-from | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | EffectiveFrom | persistence | staff-assignment-history |
| staff.effective-to | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffUnassignmentRequest | EffectiveTo | admin-input | transient-request |
| staff.effective-to | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffUnassignmentRequest | EffectiveTo | api-input | transient-request |
| staff.effective-to | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UnassignStaffPropertyCommand | EffectiveTo | application-command | transient-request |
| staff.effective-to | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | EffectiveTo | api-response | transient-response |
| staff.effective-to | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | EffectiveTo | admin-output | transient-response |
| staff.effective-to | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | EffectiveTo | persistence | staff-assignment-history |
| staff.employee-number | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileUpdateRequest | EmployeeNumber | admin-input | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileWriteRequest | EmployeeNumber | admin-input | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileUpdateRequest | EmployeeNumber | api-input | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileWriteRequest | EmployeeNumber | api-input | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | EmployeeNumber | application-command | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | EmployeeNumber | application-command | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | EmployeeNumber | application-command | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | EmployeeNumber | application-command | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | EmployeeNumber | api-response | transient-response |
| staff.employee-number | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | EmployeeNumber | admin-output | transient-response |
| staff.employee-number | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | EmployeeNumber | integration-command | transient-request |
| staff.employee-number | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | EmployeeNumber | persistence | staff-profile-lifecycle |
| staff.employee-number | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | EmployeeNumberSearch | persistence | staff-profile-lifecycle |
| staff.employee-number | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | EmployeeNumberSearch | search-index | staff-profile-lifecycle |
| staff.event.actor-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberLifecycleChangedIntegrationEvent | ActorId | integration-event | integration-message-journal |
| staff.event.actor-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | ActorId | integration-event | integration-message-journal |
| staff.event.actor-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberLifecycleChangedDomainEvent | ActorId | domain-event | transient-request |
| staff.event.actor-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | ActorId | domain-event | transient-request |
| staff.event.assignment-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | AssignmentId | integration-event | integration-message-journal |
| staff.event.assignment-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | AssignmentId | domain-event | transient-request |
| staff.event.auth-subject-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffAuthSubjectChangedIntegrationEvent | AuthSubjectId | integration-event | integration-message-journal |
| staff.event.auth-subject-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberCreatedIntegrationEvent | AuthSubjectId | integration-event | integration-message-journal |
| staff.event.auth-subject-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffAuthSubjectChangedDomainEvent | AuthSubjectId | domain-event | transient-request |
| staff.event.auth-subject-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberCreatedDomainEvent | AuthSubjectId | domain-event | transient-request |
| staff.event.effective-from | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | EffectiveFrom | integration-event | integration-message-journal |
| staff.event.effective-from | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | EffectiveFrom | domain-event | transient-request |
| staff.event.effective-on | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberLifecycleChangedIntegrationEvent | EffectiveOn | integration-event | integration-message-journal |
| staff.event.effective-on | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberLifecycleChangedDomainEvent | EffectiveOn | domain-event | transient-request |
| staff.event.effective-to | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | EffectiveTo | integration-event | integration-message-journal |
| staff.event.effective-to | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | EffectiveTo | domain-event | transient-request |
| staff.event.event-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffAuthSubjectChangedIntegrationEvent | EventId | integration-event | integration-message-journal |
| staff.event.event-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberCreatedIntegrationEvent | EventId | integration-event | integration-message-journal |
| staff.event.event-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberLifecycleChangedIntegrationEvent | EventId | integration-event | integration-message-journal |
| staff.event.event-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberUpdatedIntegrationEvent | EventId | integration-event | integration-message-journal |
| staff.event.event-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | EventId | integration-event | integration-message-journal |
| staff.event.event-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffAuthSubjectChangedDomainEvent | EventId | domain-event | transient-request |
| staff.event.event-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberCreatedDomainEvent | EventId | domain-event | transient-request |
| staff.event.event-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberLifecycleChangedDomainEvent | EventId | domain-event | transient-request |
| staff.event.event-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberUpdatedDomainEvent | EventId | domain-event | transient-request |
| staff.event.event-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | EventId | domain-event | transient-request |
| staff.event.is-current | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | IsCurrent | integration-event | integration-message-journal |
| staff.event.is-current | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | IsCurrent | domain-event | transient-request |
| staff.event.is-primary | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | IsPrimary | integration-event | integration-message-journal |
| staff.event.is-primary | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | IsPrimary | domain-event | transient-request |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffAuthSubjectChangedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberCreatedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberLifecycleChangedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberUpdatedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffAuthSubjectChangedDomainEvent | OccurredAtUtc | domain-event | transient-request |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberCreatedDomainEvent | OccurredAtUtc | domain-event | transient-request |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberLifecycleChangedDomainEvent | OccurredAtUtc | domain-event | transient-request |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberUpdatedDomainEvent | OccurredAtUtc | domain-event | transient-request |
| staff.event.occurred-at-utc | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | OccurredAtUtc | domain-event | transient-request |
| staff.event.property-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | PropertyId | integration-event | integration-message-journal |
| staff.event.property-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | PropertyId | domain-event | transient-request |
| staff.event.scope-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffAuthSubjectChangedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| staff.event.scope-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberCreatedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| staff.event.scope-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberLifecycleChangedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| staff.event.scope-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberUpdatedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| staff.event.scope-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| staff.event.scope-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffAuthSubjectChangedDomainEvent | ScopeId | domain-event | transient-request |
| staff.event.scope-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberCreatedDomainEvent | ScopeId | domain-event | transient-request |
| staff.event.scope-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberLifecycleChangedDomainEvent | ScopeId | domain-event | transient-request |
| staff.event.scope-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberUpdatedDomainEvent | ScopeId | domain-event | transient-request |
| staff.event.scope-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | ScopeId | domain-event | transient-request |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffAuthSubjectChangedIntegrationEvent | StaffMemberId | integration-event | integration-message-journal |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberCreatedIntegrationEvent | StaffMemberId | integration-event | integration-message-journal |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberLifecycleChangedIntegrationEvent | StaffMemberId | integration-event | integration-message-journal |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberUpdatedIntegrationEvent | StaffMemberId | integration-event | integration-message-journal |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | StaffMemberId | integration-event | integration-message-journal |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffAuthSubjectChangedDomainEvent | StaffMemberId | domain-event | transient-request |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberCreatedDomainEvent | StaffMemberId | domain-event | transient-request |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberLifecycleChangedDomainEvent | StaffMemberId | domain-event | transient-request |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberUpdatedDomainEvent | StaffMemberId | domain-event | transient-request |
| staff.event.staff-member-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | StaffMemberId | domain-event | transient-request |
| staff.event.staff-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffAuthSubjectChangedIntegrationEvent | StaffVersion | integration-event | integration-message-journal |
| staff.event.staff-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberCreatedIntegrationEvent | StaffVersion | integration-event | integration-message-journal |
| staff.event.staff-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberLifecycleChangedIntegrationEvent | StaffVersion | integration-event | integration-message-journal |
| staff.event.staff-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberUpdatedIntegrationEvent | StaffVersion | integration-event | integration-message-journal |
| staff.event.staff-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentChangedIntegrationEvent | StaffVersion | integration-event | integration-message-journal |
| staff.event.staff-version | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffAuthSubjectChangedDomainEvent | StaffVersion | domain-event | transient-request |
| staff.event.staff-version | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberCreatedDomainEvent | StaffVersion | domain-event | transient-request |
| staff.event.staff-version | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberLifecycleChangedDomainEvent | StaffVersion | domain-event | transient-request |
| staff.event.staff-version | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberUpdatedDomainEvent | StaffVersion | domain-event | transient-request |
| staff.event.staff-version | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffPropertyAssignmentChangedDomainEvent | StaffVersion | domain-event | transient-request |
| staff.event.status | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberCreatedIntegrationEvent | Status | integration-event | integration-message-journal |
| staff.event.status | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberLifecycleChangedIntegrationEvent | Status | integration-event | integration-message-journal |
| staff.event.status | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberUpdatedIntegrationEvent | Status | integration-event | integration-message-journal |
| staff.event.status | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberCreatedDomainEvent | Status | domain-event | transient-request |
| staff.event.status | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberLifecycleChangedDomainEvent | Status | domain-event | transient-request |
| staff.event.status | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Events.StaffMemberUpdatedDomainEvent | Status | domain-event | transient-request |
| staff.job-title | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileUpdateRequest | JobTitle | admin-input | transient-request |
| staff.job-title | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileWriteRequest | JobTitle | admin-input | transient-request |
| staff.job-title | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileUpdateRequest | JobTitle | api-input | transient-request |
| staff.job-title | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileWriteRequest | JobTitle | api-input | transient-request |
| staff.job-title | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | JobTitle | application-command | transient-request |
| staff.job-title | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | JobTitle | application-command | transient-request |
| staff.job-title | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | JobTitle | application-command | transient-request |
| staff.job-title | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | JobTitle | application-command | transient-request |
| staff.job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | JobTitle | api-response | transient-response |
| staff.job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | JobTitle | admin-output | transient-response |
| staff.job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | JobTitle | api-response | transient-response |
| staff.job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | JobTitle | admin-output | transient-response |
| staff.job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | JobTitle | integration-command | transient-request |
| staff.job-title | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | JobTitle | persistence | staff-profile-lifecycle |
| staff.legal-name | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileUpdateRequest | LegalName | admin-input | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileWriteRequest | LegalName | admin-input | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileUpdateRequest | LegalName | api-input | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileWriteRequest | LegalName | api-input | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | LegalName | application-command | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | LegalName | application-command | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | LegalName | application-command | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | LegalName | application-command | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | LegalName | api-response | transient-response |
| staff.legal-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | LegalName | admin-output | transient-response |
| staff.legal-name | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | LegalName | integration-command | transient-request |
| staff.legal-name | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | LegalName | persistence | staff-profile-lifecycle |
| staff.legal-name | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | LegalNameSearch | persistence | staff-profile-lifecycle |
| staff.legal-name | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | LegalNameSearch | search-index | staff-profile-lifecycle |
| staff.profile-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | CreatedAtUtc | api-response | transient-response |
| staff.profile-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | CreatedAtUtc | admin-output | transient-response |
| staff.profile-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | DepartedAtUtc | api-response | transient-response |
| staff.profile-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | DepartedAtUtc | admin-output | transient-response |
| staff.profile-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | LastChangedAtUtc | api-response | transient-response |
| staff.profile-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | LastChangedAtUtc | admin-output | transient-response |
| staff.profile-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | SuspendedAtUtc | api-response | transient-response |
| staff.profile-timestamps | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | SuspendedAtUtc | admin-output | transient-response |
| staff.profile-timestamps | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | CreatedAtUtc | persistence | staff-profile-lifecycle |
| staff.profile-timestamps | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | DepartedAtUtc | persistence | staff-profile-lifecycle |
| staff.profile-timestamps | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | LastChangedAtUtc | persistence | staff-profile-lifecycle |
| staff.profile-timestamps | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | SuspendedAtUtc | persistence | staff-profile-lifecycle |
| staff.projection-ordinal | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | ProjectionOrdinal | persistence | staff-profile-lifecycle |
| staff.property-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.AssignStaffPropertyCommand | PropertyId | application-command | transient-request |
| staff.property-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UnassignStaffPropertyCommand | PropertyId | application-command | transient-request |
| staff.property-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.GetStaffMemberAtPropertyQuery | PropertyId | application-query | transient-request |
| staff.property-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.ListStaffMembersAtPropertyQuery | PropertyId | application-query | transient-request |
| staff.property-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | PropertyId | api-response | transient-response |
| staff.property-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | PropertyId | admin-output | transient-response |
| staff.property-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | PropertyId | api-response | transient-response |
| staff.property-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | PropertyId | admin-output | transient-response |
| staff.property-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | PropertyId | persistence | staff-assignment-history |
| staff.property-job-title | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffAssignmentRequest | PropertyJobTitle | admin-input | transient-request |
| staff.property-job-title | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffAssignmentRequest | PropertyJobTitle | api-input | transient-request |
| staff.property-job-title | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.AssignStaffPropertyCommand | PropertyJobTitle | application-command | transient-request |
| staff.property-job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | PropertyJobTitle | api-response | transient-response |
| staff.property-job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryAssignmentDto | PropertyJobTitle | admin-output | transient-response |
| staff.property-job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | PropertyJobTitle | api-response | transient-response |
| staff.property-job-title | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffPropertyAssignmentDto | PropertyJobTitle | admin-output | transient-response |
| staff.property-job-title | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | PropertyJobTitle | persistence | staff-assignment-history |
| staff.record-version | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffAssignmentRequest | ExpectedVersion | admin-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffAuthSubjectRequest | ExpectedVersion | admin-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffDepartureRequest | ExpectedVersion | admin-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffLifecycleRequest | ExpectedVersion | admin-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileUpdateRequest | ExpectedVersion | admin-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffUnassignmentRequest | ExpectedVersion | admin-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffAssignmentRequest | ExpectedVersion | api-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffAuthSubjectRequest | ExpectedVersion | api-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffDepartureRequest | ExpectedVersion | api-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffLifecycleRequest | ExpectedVersion | api-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileUpdateRequest | ExpectedVersion | api-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffUnassignmentRequest | ExpectedVersion | api-input | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.AssignStaffPropertyCommand | ExpectedVersion | application-command | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.DepartStaffMemberCommand | ExpectedVersion | application-command | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ResumeStaffMemberCommand | ExpectedVersion | application-command | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.SetStaffAuthSubjectCommand | ExpectedVersion | application-command | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.SuspendStaffMemberCommand | ExpectedVersion | application-command | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UnassignStaffPropertyCommand | ExpectedVersion | application-command | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | ExpectedVersion | application-command | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | ExpectedVersion | application-command | transient-request |
| staff.record-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | Version | api-response | transient-response |
| staff.record-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | Version | admin-output | transient-response |
| staff.record-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | Version | api-response | transient-response |
| staff.record-version | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | Version | admin-output | transient-response |
| staff.record-version | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | Version | persistence | staff-profile-lifecycle |
| staff.scope-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | ScopeId | persistence | staff-profile-lifecycle |
| staff.scope-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | ScopeId | persistence | staff-profile-lifecycle |
| staff.search-input | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.ListStaffMembersAtPropertyQuery | Search | application-query | transient-request |
| staff.search-input | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.ListStaffMembersQuery | Search | application-query | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.AssignStaffPropertyCommand | StaffMemberId | application-command | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.DepartStaffMemberCommand | StaffMemberId | application-command | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ResumeStaffMemberCommand | StaffMemberId | application-command | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.SetStaffAuthSubjectCommand | StaffMemberId | application-command | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.SuspendStaffMemberCommand | StaffMemberId | application-command | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UnassignStaffPropertyCommand | StaffMemberId | application-command | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | StaffMemberId | application-command | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.GetStaffDirectoryMemberQuery | StaffMemberId | application-query | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.GetStaffMemberAtPropertyQuery | StaffMemberId | application-query | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.GetStaffMemberQuery | StaffMemberId | application-query | transient-request |
| staff.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | StaffMemberId | api-response | transient-response |
| staff.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | StaffMemberId | admin-output | transient-response |
| staff.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | StaffMemberId | api-response | transient-response |
| staff.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | StaffMemberId | admin-output | transient-response |
| staff.staff-member-id | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningResult | StaffMemberId | projection-export | transient-response |
| staff.staff-member-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | Id | persistence | staff-profile-lifecycle |
| staff.staff-member-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | Id | persistence | staff-profile-lifecycle |
| staff.staff-member-id | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Entities.StaffPropertyAssignment | StaffMemberId | persistence | staff-profile-lifecycle |
| staff.status | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ReconcileStaffIdentityCommand | IsActive | application-command | transient-request |
| staff.status | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.ListStaffMembersAtPropertyQuery | Status | application-query | transient-request |
| staff.status | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Queries.ListStaffMembersQuery | Status | application-query | transient-request |
| staff.status | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | Status | api-response | transient-response |
| staff.status | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffDirectoryMemberDto | Status | admin-output | transient-response |
| staff.status | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffIdentityReconciliationRequest | IsActive | integration-command | transient-request |
| staff.status | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | Status | api-response | transient-response |
| staff.status | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | Status | admin-output | transient-response |
| staff.status | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | Status | persistence | staff-profile-lifecycle |
| staff.work-email | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileUpdateRequest | WorkEmail | admin-input | transient-request |
| staff.work-email | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileWriteRequest | WorkEmail | admin-input | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileUpdateRequest | WorkEmail | api-input | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileWriteRequest | WorkEmail | api-input | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | WorkEmail | application-command | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | WorkEmail | application-command | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ReconcileStaffIdentityCommand | WorkEmail | application-command | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | WorkEmail | application-command | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | WorkEmail | application-command | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffIdentityReconciliationRequest | WorkEmail | integration-command | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | WorkEmail | api-response | transient-response |
| staff.work-email | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | WorkEmail | admin-output | transient-response |
| staff.work-email | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | WorkEmail | integration-command | transient-request |
| staff.work-email | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | WorkEmail | persistence | staff-profile-lifecycle |
| staff.work-email | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | WorkEmailSearch | persistence | staff-profile-lifecycle |
| staff.work-email | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | WorkEmailSearch | search-index | staff-profile-lifecycle |
| staff.work-phone | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileUpdateRequest | WorkPhone | admin-input | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.AdminApi | BunkFy.Modules.Staff.AdminApi.StaffAdminApiModule+StaffProfileWriteRequest | WorkPhone | admin-input | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileUpdateRequest | WorkPhone | api-input | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.Api | BunkFy.Modules.Staff.Api.Requests.StaffProfileWriteRequest | WorkPhone | api-input | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.CreateStaffMemberCommand | WorkPhone | application-command | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.ProvisionStaffOnboardingCommand | WorkPhone | application-command | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateCurrentStaffMemberCommand | WorkPhone | application-command | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.Application | BunkFy.Modules.Staff.Application.Commands.UpdateStaffMemberCommand | WorkPhone | application-command | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | WorkPhone | api-response | transient-response |
| staff.work-phone | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffMemberDto | WorkPhone | admin-output | transient-response |
| staff.work-phone | BunkFy.Modules.Staff.Contracts | BunkFy.Modules.Staff.Contracts.StaffOnboardingProvisioningRequest | WorkPhone | integration-command | transient-request |
| staff.work-phone | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | WorkPhone | persistence | staff-profile-lifecycle |
| staff.work-phone | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | WorkPhoneSearch | persistence | staff-profile-lifecycle |
| staff.work-phone | BunkFy.Modules.Staff.Domain | BunkFy.Modules.Staff.Domain.Aggregates.StaffMember | WorkPhoneSearch | search-index | staff-profile-lifecycle |

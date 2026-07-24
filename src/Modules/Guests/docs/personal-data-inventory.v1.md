# guests Personal-Data Inventory v1

Generated from `guests.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| guest-event-metadata | tenant-internal-messaging | system:authorized-module-consumer | system:guests.outbox<br>system:reservations.outbox |
| guest-records | tenant-property-authorized | permission:guests.read<br>system:reservations.guest-profile-projection | permission:guests.archive<br>permission:guests.create<br>permission:guests.manage<br>system:guests.profile-lifecycle |
| guest-request-context | tenant-property-authorized-request | permission:guests.read | permission:guests.archive<br>permission:guests.create<br>permission:guests.manage<br>permission:guests.read |
| guest-stay-history | tenant-property-authorized | permission:guests.read<br>system:guests.stay-projection-export | system:reservations.stay-projection |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| guest-data-rights-export-fragment | engineering-default | owner-export-started | 01:00:00 | not-applicable |
| guest-profile-lifecycle | engineering-default | guest-profile-created | approved-erasure-or-tenant-termination | pause-approved-erasure |
| guest-stay-history-lifecycle | engineering-default | reservation-guest-linked | approved-erasure-or-tenant-termination | pause-approved-erasure |
| integration-message-journal | engineering-default | message-created | message-journal-retention-completed | no-payload-hold |
| projection-transfer | engineering-default | projection-batch-created | projection-batch-completed | not-applicable |
| transient-request | engineering-default | request-accepted | request-completed | not-applicable |
| transient-response | engineering-default | response-created | response-completed | not-applicable |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| guest-operational-history | include-in-authorized-guest-export | correct-authoritative-reservation-source | suppress-non-required-operational-use | unlink-or-anonymize-subject-identifiers |
| guest-profile-editable | include-in-authorized-guest-export | replace-through-guest-profile-workflow | suppress-non-required-operational-use | irreversibly-anonymize-or-delete |
| staff-audit-attribution | route-through-staff-subject-workflow | correct-authoritative-account-source | retain-minimum-required-audit-attribution | pseudonymize-subject-to-minimum-audit-receipt |
| transient-request-data | not-retained-after-request | replace-before-submission | discard-request | discard-on-request-completion |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| guest.event.event-id | guest | linked-operational | standard | idempotent-event-processing<br>projection-consistency | system-generated | guests | customer-controller-bunk-fy-processor | guest-event-metadata | guest.event.metadata | integration-message-journal | guest-operational-history | integration-event | cross-module | engineering-default |
| guest.event.occurred-at-utc | guest | linked-operational | standard | event-ordering<br>projection-consistency | system-generated | guests | customer-controller-bunk-fy-processor | guest-event-metadata | guest.event.metadata | integration-message-journal | guest-operational-history | integration-event | cross-module | engineering-default |
| guest.profile.archived-at-utc | guest | lifecycle | standard | guest-record-lifecycle | system-generated | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.lifecycle | guest-profile-lifecycle | guest-profile-editable | api-response<br>data-rights-export<br>persistence | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.audit-actor-id | staff | audit-attribution | standard | accountability<br>authorized-change-traceability | authenticated-subject | guests | customer-controller-bunk-fy-processor | guest-records | staff.audit-attribution | guest-profile-lifecycle | staff-audit-attribution | api-response<br>application-command<br>persistence | customer-api<br>intra-module | engineering-default |
| guest.profile.created-at-utc | guest | lifecycle | standard | guest-record-lifecycle | system-generated | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.lifecycle | guest-profile-lifecycle | guest-profile-editable | api-response<br>data-rights-export<br>persistence | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.date-of-birth | guest | demographic | elevated | country-record-compliance<br>guest-identification | reservation-promotion<br>staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.demographic | guest-profile-lifecycle | guest-profile-editable | api-input<br>api-response<br>application-command<br>data-rights-export<br>persistence | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.display-name | guest | direct-identifier | standard | guest-identification<br>reservation-operations | reservation-promotion<br>staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.identity | guest-profile-lifecycle | guest-profile-editable | api-input<br>api-response<br>application-command<br>data-rights-export<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.email | guest | contact | elevated | guest-contact<br>guest-record-matching | reservation-promotion<br>staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.contact | guest-profile-lifecycle | guest-profile-editable | api-input<br>api-response<br>application-command<br>data-rights-export<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.id | guest | linked-operational | standard | guest-record-linkage<br>projection-consistency<br>reservation-operations | system-generated | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.identifier | guest-profile-lifecycle | guest-profile-editable | api-response<br>application-command<br>application-query<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.last-changed-at-utc | guest | lifecycle | standard | guest-record-lifecycle<br>optimistic-concurrency | system-generated | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.lifecycle | guest-profile-lifecycle | guest-profile-editable | api-response<br>data-rights-export<br>persistence | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.legal-name | guest | direct-identifier | elevated | country-record-compliance<br>guest-identification | reservation-promotion<br>staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.identity | guest-profile-lifecycle | guest-profile-editable | api-input<br>api-response<br>application-command<br>data-rights-export<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.nationality-country-code | guest | demographic | elevated | country-record-compliance | reservation-promotion<br>staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.demographic | guest-profile-lifecycle | guest-profile-editable | api-input<br>api-response<br>application-command<br>data-rights-export<br>persistence | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.notes | guest | free-text | unstructured | guest-service-operations | reservation-promotion<br>staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.notes | guest-profile-lifecycle | guest-profile-editable | api-input<br>api-response<br>application-command<br>data-rights-export<br>persistence | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.origin-property-id | guest | linked-operational | standard | guest-record-ownership<br>property-authorization | staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.identifier | guest-profile-lifecycle | guest-profile-editable | api-response<br>application-command<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.phone | guest | contact | elevated | guest-contact<br>guest-record-matching | reservation-promotion<br>staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.contact | guest-profile-lifecycle | guest-profile-editable | api-input<br>api-response<br>application-command<br>data-rights-export<br>persistence<br>search-index | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.preferred-language-tag | guest | preference | standard | guest-service-operations | reservation-promotion<br>staff-entry | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.preference | guest-profile-lifecycle | guest-profile-editable | api-input<br>api-response<br>application-command<br>data-rights-export<br>persistence | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.projection-ordinal | guest | linked-operational | standard | projection-consistency | system-generated | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.lifecycle | guest-profile-lifecycle | guest-operational-history | persistence | intra-module | engineering-default |
| guest.profile.record-version | guest | lifecycle | standard | optimistic-concurrency<br>projection-consistency | system-generated | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.lifecycle | guest-profile-lifecycle | guest-operational-history | api-input<br>api-response<br>application-command<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.status | guest | lifecycle | standard | guest-record-lifecycle<br>projection-consistency | system-generated | guests | customer-controller-bunk-fy-processor | guest-records | guest.profile.lifecycle | guest-profile-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.profile.tenant-scope-id | guest | linked-operational | standard | scope-isolation<br>tenant-routing | authenticated-tenant-context | guests | customer-controller-bunk-fy-processor | guest-event-metadata | guest.profile.identifier | guest-profile-lifecycle | guest-operational-history | integration-event<br>persistence<br>projection-export | cross-module<br>intra-module | engineering-default |
| guest.request.property-scope-id | guest | linked-operational | standard | property-authorization<br>scope-isolation | request-input | guests | customer-controller-bunk-fy-processor | guest-request-context | guest.profile.identifier | transient-request | transient-request-data | application-command<br>application-query | customer-api<br>intra-module | engineering-default |
| guest.search.term | guest | search-input | unstructured | guest-record-lookup | request-input | guests | customer-controller-bunk-fy-processor | guest-request-context | guest.profile.search | transient-request | transient-request-data | application-query | customer-api<br>intra-module | engineering-default |
| guest.stay.arrival | guest | linked-operational | standard | guest-stay-history<br>reservation-operations | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.checked-in-business-date | guest | linked-operational | standard | guest-stay-history<br>reservation-operations | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.checked-out-business-date | guest | linked-operational | standard | guest-stay-history<br>reservation-operations | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.current-participant | guest | lifecycle | standard | current-occupancy-projection<br>guest-stay-history | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.departure | guest | linked-operational | standard | guest-stay-history<br>reservation-operations | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.no-show-business-date | guest | linked-operational | standard | guest-stay-history<br>reservation-operations | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.property-id | guest | linked-operational | standard | guest-stay-history<br>property-authorization | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.reservation-id | guest | linked-operational | standard | guest-stay-history<br>reservation-linkage | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.reservation-version | guest | lifecycle | standard | projection-consistency | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.role | guest | linked-operational | standard | guest-stay-history<br>reservation-operations | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |
| guest.stay.status | guest | lifecycle | standard | guest-stay-history<br>reservation-operations | reservation-projection | guests | customer-controller-bunk-fy-processor | guest-stay-history | guest.stay-history | guest-stay-history-lifecycle | guest-operational-history | api-response<br>data-rights-export<br>integration-event<br>persistence<br>projection-export | cross-module<br>customer-api<br>intra-module | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| guest.event.event-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileArchivedIntegrationEvent | EventId | integration-event | integration-message-journal |
| guest.event.event-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileCreatedIntegrationEvent | EventId | integration-event | integration-message-journal |
| guest.event.event-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileUpdatedIntegrationEvent | EventId | integration-event | integration-message-journal |
| guest.event.event-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | EventId | integration-event | integration-message-journal |
| guest.event.event-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | EventId | integration-event | integration-message-journal |
| guest.event.occurred-at-utc | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileArchivedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| guest.event.occurred-at-utc | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileCreatedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| guest.event.occurred-at-utc | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileUpdatedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| guest.event.occurred-at-utc | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| guest.event.occurred-at-utc | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | OccurredAtUtc | integration-event | integration-message-journal |
| guest.profile.archived-at-utc | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | ArchivedAtUtc | api-response | transient-response |
| guest.profile.archived-at-utc | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | ArchivedAtUtc | persistence | guest-profile-lifecycle |
| guest.profile.archived-at-utc | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | ArchivedAtUtc | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.audit-actor-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.ArchiveGuestProfileCommand | ActorId | application-command | transient-request |
| guest.profile.audit-actor-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | ActorId | application-command | transient-request |
| guest.profile.audit-actor-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | ActorId | application-command | transient-request |
| guest.profile.audit-actor-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | CreatedBy | api-response | transient-response |
| guest.profile.audit-actor-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | LastChangedBy | api-response | transient-response |
| guest.profile.audit-actor-id | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | CreatedBy | persistence | guest-profile-lifecycle |
| guest.profile.audit-actor-id | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | LastChangedBy | persistence | guest-profile-lifecycle |
| guest.profile.created-at-utc | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | CreatedAtUtc | api-response | transient-response |
| guest.profile.created-at-utc | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | CreatedAtUtc | persistence | guest-profile-lifecycle |
| guest.profile.created-at-utc | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | CreatedAtUtc | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.date-of-birth | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | DateOfBirth | api-input | transient-request |
| guest.profile.date-of-birth | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileWriteRequest | DateOfBirth | api-input | transient-request |
| guest.profile.date-of-birth | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | DateOfBirth | application-command | transient-request |
| guest.profile.date-of-birth | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | DateOfBirth | application-command | transient-request |
| guest.profile.date-of-birth | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | DateOfBirth | api-response | transient-response |
| guest.profile.date-of-birth | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | DateOfBirth | persistence | guest-profile-lifecycle |
| guest.profile.date-of-birth | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | DateOfBirth | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.display-name | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | DisplayName | api-input | transient-request |
| guest.profile.display-name | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileWriteRequest | DisplayName | api-input | transient-request |
| guest.profile.display-name | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | DisplayName | application-command | transient-request |
| guest.profile.display-name | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | DisplayName | application-command | transient-request |
| guest.profile.display-name | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | DisplayName | api-response | transient-response |
| guest.profile.display-name | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | DisplayName | persistence | guest-profile-lifecycle |
| guest.profile.display-name | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | DisplayNameSearch | persistence | guest-profile-lifecycle |
| guest.profile.display-name | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | DisplayNameSearch | search-index | guest-profile-lifecycle |
| guest.profile.display-name | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | DisplayName | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.email | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | Email | api-input | transient-request |
| guest.profile.email | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileWriteRequest | Email | api-input | transient-request |
| guest.profile.email | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | Email | application-command | transient-request |
| guest.profile.email | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | Email | application-command | transient-request |
| guest.profile.email | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | Email | api-response | transient-response |
| guest.profile.email | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | Email | persistence | guest-profile-lifecycle |
| guest.profile.email | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | EmailSearch | persistence | guest-profile-lifecycle |
| guest.profile.email | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | EmailSearch | search-index | guest-profile-lifecycle |
| guest.profile.email | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | Email | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.ArchiveGuestProfileCommand | GuestId | application-command | transient-request |
| guest.profile.id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | GuestId | application-command | transient-request |
| guest.profile.id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Queries.GetGuestProfileQuery | GuestId | application-query | transient-request |
| guest.profile.id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Queries.GetGuestStayHistoryQuery | GuestId | application-query | transient-request |
| guest.profile.id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileArchivedIntegrationEvent | GuestId | integration-event | integration-message-journal |
| guest.profile.id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileCreatedIntegrationEvent | GuestId | integration-event | integration-message-journal |
| guest.profile.id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | GuestId | api-response | transient-response |
| guest.profile.id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileEligibilityProjectionExport | GuestId | projection-export | projection-transfer |
| guest.profile.id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileUpdatedIntegrationEvent | GuestId | integration-event | integration-message-journal |
| guest.profile.id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | GuestId | integration-event | integration-message-journal |
| guest.profile.id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | GuestId | integration-event | integration-message-journal |
| guest.profile.id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | GuestId | projection-export | projection-transfer |
| guest.profile.id | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | Id | persistence | guest-profile-lifecycle |
| guest.profile.id | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | GuestId | persistence | guest-stay-history-lifecycle |
| guest.profile.id | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | GuestId | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.last-changed-at-utc | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | LastChangedAtUtc | api-response | transient-response |
| guest.profile.last-changed-at-utc | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | LastChangedAtUtc | persistence | guest-profile-lifecycle |
| guest.profile.last-changed-at-utc | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | LastChangedAtUtc | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.legal-name | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | LegalName | api-input | transient-request |
| guest.profile.legal-name | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileWriteRequest | LegalName | api-input | transient-request |
| guest.profile.legal-name | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | LegalName | application-command | transient-request |
| guest.profile.legal-name | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | LegalName | application-command | transient-request |
| guest.profile.legal-name | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | LegalName | api-response | transient-response |
| guest.profile.legal-name | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | LegalName | persistence | guest-profile-lifecycle |
| guest.profile.legal-name | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | LegalNameSearch | persistence | guest-profile-lifecycle |
| guest.profile.legal-name | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | LegalNameSearch | search-index | guest-profile-lifecycle |
| guest.profile.legal-name | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | LegalName | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.nationality-country-code | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | NationalityCountryCode | api-input | transient-request |
| guest.profile.nationality-country-code | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileWriteRequest | NationalityCountryCode | api-input | transient-request |
| guest.profile.nationality-country-code | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | NationalityCountryCode | application-command | transient-request |
| guest.profile.nationality-country-code | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | NationalityCountryCode | application-command | transient-request |
| guest.profile.nationality-country-code | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | NationalityCountryCode | api-response | transient-response |
| guest.profile.nationality-country-code | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | NationalityCountryCode | persistence | guest-profile-lifecycle |
| guest.profile.nationality-country-code | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | NationalityCountryCode | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.notes | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | Notes | api-input | transient-request |
| guest.profile.notes | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileWriteRequest | Notes | api-input | transient-request |
| guest.profile.notes | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | Notes | application-command | transient-request |
| guest.profile.notes | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | Notes | application-command | transient-request |
| guest.profile.notes | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | Notes | api-response | transient-response |
| guest.profile.notes | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | Notes | persistence | guest-profile-lifecycle |
| guest.profile.notes | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | Notes | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.origin-property-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | PropertyId | application-command | transient-request |
| guest.profile.origin-property-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileCreatedIntegrationEvent | OriginPropertyId | integration-event | integration-message-journal |
| guest.profile.origin-property-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | OriginPropertyId | api-response | transient-response |
| guest.profile.origin-property-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileEligibilityProjectionExport | OriginPropertyId | projection-export | projection-transfer |
| guest.profile.origin-property-id | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | OriginPropertyId | persistence | guest-profile-lifecycle |
| guest.profile.origin-property-id | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | OriginPropertyId | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.phone | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | Phone | api-input | transient-request |
| guest.profile.phone | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileWriteRequest | Phone | api-input | transient-request |
| guest.profile.phone | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | Phone | application-command | transient-request |
| guest.profile.phone | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | Phone | application-command | transient-request |
| guest.profile.phone | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | Phone | api-response | transient-response |
| guest.profile.phone | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | Phone | persistence | guest-profile-lifecycle |
| guest.profile.phone | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | PhoneSearch | persistence | guest-profile-lifecycle |
| guest.profile.phone | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | PhoneSearch | search-index | guest-profile-lifecycle |
| guest.profile.phone | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | Phone | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.preferred-language-tag | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | PreferredLanguageTag | api-input | transient-request |
| guest.profile.preferred-language-tag | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileWriteRequest | PreferredLanguageTag | api-input | transient-request |
| guest.profile.preferred-language-tag | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.CreateGuestProfileCommand | PreferredLanguageTag | application-command | transient-request |
| guest.profile.preferred-language-tag | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | PreferredLanguageTag | application-command | transient-request |
| guest.profile.preferred-language-tag | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | PreferredLanguageTag | api-response | transient-response |
| guest.profile.preferred-language-tag | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | PreferredLanguageTag | persistence | guest-profile-lifecycle |
| guest.profile.preferred-language-tag | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | PreferredLanguageTag | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.projection-ordinal | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | ProjectionOrdinal | persistence | guest-profile-lifecycle |
| guest.profile.record-version | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+ArchiveGuestProfileRequest | ExpectedVersion | api-input | transient-request |
| guest.profile.record-version | BunkFy.Modules.Guests.Api | BunkFy.Modules.Guests.Api.GuestsModule+GuestProfileUpdateRequest | ExpectedVersion | api-input | transient-request |
| guest.profile.record-version | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.ArchiveGuestProfileCommand | ExpectedVersion | application-command | transient-request |
| guest.profile.record-version | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | ExpectedVersion | application-command | transient-request |
| guest.profile.record-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileArchivedIntegrationEvent | GuestVersion | integration-event | integration-message-journal |
| guest.profile.record-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileCreatedIntegrationEvent | GuestVersion | integration-event | integration-message-journal |
| guest.profile.record-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | Version | api-response | transient-response |
| guest.profile.record-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileEligibilityProjectionExport | GuestVersion | projection-export | projection-transfer |
| guest.profile.record-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileUpdatedIntegrationEvent | GuestVersion | integration-event | integration-message-journal |
| guest.profile.record-version | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | Version | persistence | guest-profile-lifecycle |
| guest.profile.record-version | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | Version | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.status | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileCreatedIntegrationEvent | Status | integration-event | integration-message-journal |
| guest.profile.status | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileDto | Status | api-response | transient-response |
| guest.profile.status | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileEligibilityProjectionExport | Status | projection-export | projection-transfer |
| guest.profile.status | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileUpdatedIntegrationEvent | Status | integration-event | integration-message-journal |
| guest.profile.status | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | Status | persistence | guest-profile-lifecycle |
| guest.profile.status | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestProfileDataRightsExport | Status | data-rights-export | guest-data-rights-export-fragment |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileArchivedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileArchivedIntegrationEvent | TenantId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileCreatedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileCreatedIntegrationEvent | TenantId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileEligibilityProjectionExport | TenantId | projection-export | projection-transfer |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileUpdatedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestProfileUpdatedIntegrationEvent | TenantId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | TenantId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | ScopeId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | TenantId | integration-event | integration-message-journal |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | TenantId | projection-export | projection-transfer |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Domain | BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile | ScopeId | persistence | guest-profile-lifecycle |
| guest.profile.tenant-scope-id | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | ScopeId | persistence | guest-stay-history-lifecycle |
| guest.request.property-scope-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.ArchiveGuestProfileCommand | PropertyId | application-command | transient-request |
| guest.request.property-scope-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Commands.UpdateGuestProfileCommand | PropertyId | application-command | transient-request |
| guest.request.property-scope-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Queries.GetGuestProfileQuery | PropertyId | application-query | transient-request |
| guest.request.property-scope-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Queries.GetGuestStayHistoryQuery | PropertyId | application-query | transient-request |
| guest.request.property-scope-id | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Queries.ListGuestProfilesQuery | PropertyId | application-query | transient-request |
| guest.search.term | BunkFy.Modules.Guests.Application | BunkFy.Modules.Guests.Application.Queries.ListGuestProfilesQuery | Search | application-query | transient-request |
| guest.stay.arrival | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | Arrival | api-response | transient-response |
| guest.stay.arrival | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | Arrival | integration-event | integration-message-journal |
| guest.stay.arrival | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | Arrival | integration-event | integration-message-journal |
| guest.stay.arrival | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | Arrival | projection-export | projection-transfer |
| guest.stay.arrival | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | Arrival | persistence | guest-stay-history-lifecycle |
| guest.stay.arrival | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | Arrival | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.checked-in-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | CheckedInBusinessDate | api-response | transient-response |
| guest.stay.checked-in-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | CheckedInBusinessDate | integration-event | integration-message-journal |
| guest.stay.checked-in-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | CheckedInBusinessDate | integration-event | integration-message-journal |
| guest.stay.checked-in-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | CheckedInBusinessDate | projection-export | projection-transfer |
| guest.stay.checked-in-business-date | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | CheckedInBusinessDate | persistence | guest-stay-history-lifecycle |
| guest.stay.checked-in-business-date | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | CheckedInBusinessDate | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.checked-out-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | CheckedOutBusinessDate | api-response | transient-response |
| guest.stay.checked-out-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | CheckedOutBusinessDate | integration-event | integration-message-journal |
| guest.stay.checked-out-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | CheckedOutBusinessDate | integration-event | integration-message-journal |
| guest.stay.checked-out-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | CheckedOutBusinessDate | projection-export | projection-transfer |
| guest.stay.checked-out-business-date | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | CheckedOutBusinessDate | persistence | guest-stay-history-lifecycle |
| guest.stay.checked-out-business-date | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | CheckedOutBusinessDate | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.current-participant | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | IsCurrentParticipant | api-response | transient-response |
| guest.stay.current-participant | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | IsCurrentParticipant | integration-event | integration-message-journal |
| guest.stay.current-participant | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | IsCurrentParticipant | projection-export | projection-transfer |
| guest.stay.current-participant | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | IsCurrentParticipant | persistence | guest-stay-history-lifecycle |
| guest.stay.current-participant | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | IsCurrentParticipant | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.departure | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | Departure | api-response | transient-response |
| guest.stay.departure | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | Departure | integration-event | integration-message-journal |
| guest.stay.departure | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | Departure | integration-event | integration-message-journal |
| guest.stay.departure | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | Departure | projection-export | projection-transfer |
| guest.stay.departure | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | Departure | persistence | guest-stay-history-lifecycle |
| guest.stay.departure | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | Departure | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.no-show-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | NoShowBusinessDate | api-response | transient-response |
| guest.stay.no-show-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | NoShowBusinessDate | integration-event | integration-message-journal |
| guest.stay.no-show-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | NoShowBusinessDate | integration-event | integration-message-journal |
| guest.stay.no-show-business-date | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | NoShowBusinessDate | projection-export | projection-transfer |
| guest.stay.no-show-business-date | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | NoShowBusinessDate | persistence | guest-stay-history-lifecycle |
| guest.stay.no-show-business-date | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | NoShowBusinessDate | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.property-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | PropertyId | api-response | transient-response |
| guest.stay.property-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | PropertyId | integration-event | integration-message-journal |
| guest.stay.property-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | PropertyId | integration-event | integration-message-journal |
| guest.stay.property-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | PropertyId | projection-export | projection-transfer |
| guest.stay.property-id | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | PropertyId | persistence | guest-stay-history-lifecycle |
| guest.stay.property-id | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | PropertyId | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.reservation-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | ReservationId | api-response | transient-response |
| guest.stay.reservation-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | ReservationId | integration-event | integration-message-journal |
| guest.stay.reservation-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | ReservationId | integration-event | integration-message-journal |
| guest.stay.reservation-id | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | ReservationId | projection-export | projection-transfer |
| guest.stay.reservation-id | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | ReservationId | persistence | guest-stay-history-lifecycle |
| guest.stay.reservation-id | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | ReservationId | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.reservation-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | ReservationVersion | api-response | transient-response |
| guest.stay.reservation-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | ReservationVersion | integration-event | integration-message-journal |
| guest.stay.reservation-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | ReservationVersion | integration-event | integration-message-journal |
| guest.stay.reservation-version | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | ReservationVersion | projection-export | projection-transfer |
| guest.stay.reservation-version | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | ReservationVersion | persistence | guest-stay-history-lifecycle |
| guest.stay.reservation-version | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | ReservationVersion | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.role | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | Role | api-response | transient-response |
| guest.stay.role | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | Role | integration-event | integration-message-journal |
| guest.stay.role | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | Role | integration-event | integration-message-journal |
| guest.stay.role | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | Role | projection-export | projection-transfer |
| guest.stay.role | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | Role | persistence | guest-stay-history-lifecycle |
| guest.stay.role | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | Role | data-rights-export | guest-data-rights-export-fragment |
| guest.stay.status | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.GuestStayHistoryItem | Status | api-response | transient-response |
| guest.stay.status | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestLinkedIntegrationEvent | Status | integration-event | integration-message-journal |
| guest.stay.status | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayChangedIntegrationEvent | Status | integration-event | integration-message-journal |
| guest.stay.status | BunkFy.Modules.Guests.Contracts | BunkFy.Modules.Guests.Contracts.ReservationGuestStayProjectionExport | Status | projection-export | projection-transfer |
| guest.stay.status | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.GuestStayHistoryEntry | Status | persistence | guest-stay-history-lifecycle |
| guest.stay.status | BunkFy.Modules.Guests.Persistence | BunkFy.Modules.Guests.Persistence.Repositories.GuestStayDataRightsExport | Status | data-rights-export | guest-data-rights-export-fragment |

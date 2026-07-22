# operations-notifications Personal-Data Inventory v1

Generated from `operations-notifications.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| operations-inbox | tenant-addressed-operational-inbox | subject:self<br>system:approved-retention<br>system:notifications-delivery | system:bunk-fy-operations-notifications |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| notification-inbox | engineering-default | notification-requested | approved-notification-retention-completed | pause-approved-erasure |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| guest-operation-reference | resolve-through-authorized-reservation-or-ingestion-export | correct-authoritative-source-record | suppress-non-required-operational-delivery | remove-notification-copy-with-approved-guest-rights-workflow |
| recipient-inbox | include-in-authorized-account-notification-export | correct-authoritative-source-and-append-new-notification-if-required | respect-recipient-preferences-and-approved-account-restrictions | delete-or-pseudonymize-after-approved-notification-retention |
| staff-operation-reference | include-in-authorized-staff-notification-export | correct-authoritative-staff-source | suppress-non-required-operational-delivery | remove-or-pseudonymize-after-approved-employment-retention |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| operations-notifications.block-arrival | account-holder | linked-operational | standard | availability-context<br>inventory-block-navigation | inventory | inventory | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.block-arrival | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.block-departure | account-holder | linked-operational | standard | availability-context<br>inventory-block-navigation | inventory | inventory | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.block-departure | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.block-group-id | account-holder | linked-operational | standard | inventory-block-navigation<br>resource-highlighting | inventory | inventory | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.block-group-id | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.connection-id | guest | linked-operational | elevated | provider-conflict-resolution<br>provider-connection-navigation | ingestion<br>reservations | ingestion | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.connection-id | notification-inbox | guest-operation-reference | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.notification-id | account-holder | pseudonymous-identifier | standard | inbox-idempotency<br>notification-correlation | recipient-id<br>source-event<br>system-derived | operations-notifications | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.notification-id | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.occurred-at | account-holder | lifecycle | standard | inbox-ordering<br>operational-timeline | source-event | source-module | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.occurred-at | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.payload-envelope | account-holder | structured-payload | elevated | resource-highlighting<br>safe-navigation<br>targeted-live-refresh | typed-navigation-payload | operations-notifications | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.payload-envelope | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.property-id | account-holder | linked-operational | standard | property-scoped-navigation<br>tenant-resource-isolation | properties<br>source-event | properties | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.property-id | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.receipt-id | guest | pseudonymous-identifier | elevated | provider-conflict-resolution<br>source-evidence-navigation | ingestion<br>reservations | ingestion | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.receipt-id | notification-inbox | guest-operation-reference | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.recipient-subject-id | account-holder | pseudonymous-identifier | elevated | addressed-delivery<br>inbox-authorization<br>recipient-preferences | auth<br>organizations<br>staff | auth | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.recipient-subject-id | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.rendered-body | account-holder | linked-operational | elevated | attention-routing<br>operational-awareness | source-event<br>system-template | operations-notifications | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.rendered-body | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.reservation-id | guest | pseudonymous-identifier | elevated | provider-conflict-resolution<br>reservation-live-refresh<br>reservation-navigation | reservations | reservations | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.reservation-id | notification-inbox | guest-operation-reference | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.room-id | account-holder | linked-operational | standard | resource-highlighting<br>room-navigation | inventory<br>properties | properties | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.room-id | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.staff-member-id | staff | pseudonymous-identifier | elevated | profile-live-refresh<br>staff-self-navigation | staff | staff | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.staff-member-id | notification-inbox | staff-operation-reference | notification | cross-module<br>customer-api<br>processor | engineering-default |
| operations-notifications.workspace-scope-id | account-holder | linked-operational | standard | tenant-isolation<br>workspace-inbox-routing | organizations<br>source-event | organizations | customer-controller-bunk-fy-processor | operations-inbox | operations-notifications.workspace-scope-id | notification-inbox | recipient-inbox | notification | cross-module<br>customer-api<br>processor | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| operations-notifications.block-arrival | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.InventoryBlockCreatedNotificationPayload | Arrival | notification | notification-inbox |
| operations-notifications.block-departure | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.InventoryBlockCreatedNotificationPayload | Departure | notification | notification-inbox |
| operations-notifications.block-group-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.InventoryBlockCreatedNotificationPayload | BlockGroupId | notification | notification-inbox |
| operations-notifications.block-group-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.InventoryBlockReleasedNotificationPayload | BlockGroupId | notification | notification-inbox |
| operations-notifications.connection-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.ProviderAttentionNotificationPayload | ConnectionId | notification | notification-inbox |
| operations-notifications.notification-id | Gma.Modules.Notifications.Contracts | Gma.Modules.Notifications.Contracts.UserNotificationRequestedIntegrationEventV2 | EventId | notification | notification-inbox |
| operations-notifications.occurred-at | Gma.Modules.Notifications.Contracts | Gma.Modules.Notifications.Contracts.UserNotificationRequestedIntegrationEventV2 | OccurredAtUtc | notification | notification-inbox |
| operations-notifications.payload-envelope | Gma.Modules.Notifications.Contracts | Gma.Modules.Notifications.Contracts.UserNotificationRequestedIntegrationEventV2 | PayloadJson | notification | notification-inbox |
| operations-notifications.property-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.InventoryBlockCreatedNotificationPayload | PropertyId | notification | notification-inbox |
| operations-notifications.property-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.InventoryBlockReleasedNotificationPayload | PropertyId | notification | notification-inbox |
| operations-notifications.property-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.PropertyNotificationPayload | PropertyId | notification | notification-inbox |
| operations-notifications.property-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.ProviderAttentionNotificationPayload | PropertyId | notification | notification-inbox |
| operations-notifications.property-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.ReservationNotificationPayload | PropertyId | notification | notification-inbox |
| operations-notifications.property-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.RoomNotificationPayload | PropertyId | notification | notification-inbox |
| operations-notifications.receipt-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.ProviderAttentionNotificationPayload | ReceiptId | notification | notification-inbox |
| operations-notifications.recipient-subject-id | Gma.Modules.Notifications.Contracts | Gma.Modules.Notifications.Contracts.UserNotificationRequestedIntegrationEventV2 | UserId | notification | notification-inbox |
| operations-notifications.rendered-body | Gma.Modules.Notifications.Contracts | Gma.Modules.Notifications.Contracts.UserNotificationRequestedIntegrationEventV2 | Body | notification | notification-inbox |
| operations-notifications.reservation-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.ProviderAttentionNotificationPayload | ReservationId | notification | notification-inbox |
| operations-notifications.reservation-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.ReservationNotificationPayload | ReservationId | notification | notification-inbox |
| operations-notifications.room-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.RoomNotificationPayload | RoomId | notification | notification-inbox |
| operations-notifications.staff-member-id | BunkFy.Extensions.Operations.Notifications | BunkFy.Extensions.Operations.Notifications.StaffProfileNotificationPayload | StaffMemberId | notification | notification-inbox |
| operations-notifications.workspace-scope-id | Gma.Modules.Notifications.Contracts | Gma.Modules.Notifications.Contracts.UserNotificationRequestedIntegrationEventV2 | ScopeId | notification | notification-inbox |

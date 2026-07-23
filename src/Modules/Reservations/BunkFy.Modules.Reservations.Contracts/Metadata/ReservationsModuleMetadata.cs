namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Modules;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Properties.Contracts;

public static class ReservationsModuleMetadata
{
    public const string Name = "reservations";
    public const string Schema = "reservations";
    public const string InventoryProjectionName = "inventory-availability";
    public const int InventoryProjectionVersion = 1;
    public const string GuestProfilesProjectionName = "guest-profiles";
    public const int GuestProfilesProjectionVersion = 1;
    public const string PropertyProjectionName = "properties";
    public const int PropertyProjectionVersion = 2;
    public const string ProjectionWorkerGroup = "projection-workers";
    public const string ReminderWorkerGroup = "reminder-workers";
    public const int ArrivalReminderLeadTimeMinutes = 120;
    public const string AllocationConfirmedHandlerName = "allocation-confirmed";
    public const string AllocationRejectedHandlerName = "allocation-rejected";
    public const string AllocationReleasedHandlerName = "allocation-released";
    public const string AllocationReleaseRejectedHandlerName = "allocation-release-rejected";
    public const string AllocationAmendmentConfirmedHandlerName = "allocation-amendment-confirmed";
    public const string AllocationAmendmentRejectedHandlerName = "allocation-amendment-rejected";
    public const string UnitDefinitionChangedHandlerName = "inventory-unit-definition-changed";
    public const string ManualBlockCreatedHandlerName = "manual-inventory-block-created";
    public const string ManualBlockReleasedHandlerName = "manual-inventory-block-released";
    public const string ExternalOperationSourceModuleName = "ingestion";
    public const string ExternalCreateHandlerName = "external-reservation-create";
    public const string ExternalGuestDetailsHandlerName = "external-reservation-guest-details";
    public const string ExternalAmendmentHandlerName = "external-reservation-amendment";
    public const string ExternalCancellationHandlerName = "external-reservation-cancellation";
    public const string GuestCreatedHandlerName = "guest-created";
    public const string GuestUpdatedHandlerName = "guest-updated";
    public const string GuestArchivedHandlerName = "guest-archived";
    public const string PropertyCreatedHandlerName = "property-created";
    public const string PropertyUpdatedHandlerName = "property-updated";
    public const string PropertyRetiredHandlerName = "property-retired";
    public const string PropertyProcessingPolicyActivatedHandlerName = "property-processing-policy-activated";
    public const string PropertyProcessingSuspendedHandlerName = "property-processing-suspended";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.Read, "Read reservations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.Create, "Create reservations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.Manage, "Manage reservation lifecycle.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.ManageGuests, "Manage canonical guest links on reservations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.Cancel, "Cancel reservations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.CheckIn, "Check in confirmed reservations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.NoShow, "Mark confirmed reservations as no-show.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.CheckOut, "Check out active stays.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants)
        ])
        .WithSubscription<InventoryAllocationConfirmedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationConfirmedHandlerName)
        .WithSubscription<InventoryAllocationRejectedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationRejectedHandlerName)
        .WithSubscription<InventoryAllocationReleasedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationReleasedHandlerName)
        .WithSubscription<InventoryAllocationReleaseRejectedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationReleaseRejectedHandlerName)
        .WithSubscription<InventoryAllocationAmendmentConfirmedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationAmendmentConfirmedHandlerName)
        .WithSubscription<InventoryAllocationAmendmentRejectedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationAmendmentRejectedHandlerName)
        .WithSubscription<InventoryUnitDefinitionChangedIntegrationEvent>(InventoryModuleMetadata.Name, UnitDefinitionChangedHandlerName)
        .WithSubscription<ManualInventoryBlockCreatedIntegrationEvent>(InventoryModuleMetadata.Name, ManualBlockCreatedHandlerName)
        .WithSubscription<ManualInventoryBlockReleasedIntegrationEvent>(InventoryModuleMetadata.Name, ManualBlockReleasedHandlerName)
        .WithSubscription<ExternalReservationCreateRequestedIntegrationEvent>(ExternalOperationSourceModuleName, ExternalCreateHandlerName)
        .WithSubscription<ExternalReservationGuestDetailsChangeRequestedIntegrationEvent>(ExternalOperationSourceModuleName, ExternalGuestDetailsHandlerName)
        .WithSubscription<ExternalReservationAmendmentRequestedIntegrationEvent>(ExternalOperationSourceModuleName, ExternalAmendmentHandlerName)
        .WithSubscription<ExternalReservationCancellationRequestedIntegrationEvent>(ExternalOperationSourceModuleName, ExternalCancellationHandlerName)
        .WithSubscription<GuestProfileCreatedIntegrationEvent>(GuestsModuleMetadata.Name, GuestCreatedHandlerName)
        .WithSubscription<GuestProfileUpdatedIntegrationEvent>(GuestsModuleMetadata.Name, GuestUpdatedHandlerName)
        .WithSubscription<GuestProfileArchivedIntegrationEvent>(GuestsModuleMetadata.Name, GuestArchivedHandlerName)
        .WithSubscription<PropertyCreatedIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyCreatedHandlerName)
        .WithSubscription<PropertyUpdatedIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyUpdatedHandlerName)
        .WithSubscription<PropertyRetiredIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyRetiredHandlerName)
        .WithSubscription<PropertyProcessingPolicyActivatedIntegrationEvent>(
            PropertiesModuleMetadata.Name,
            PropertyProcessingPolicyActivatedHandlerName)
        .WithSubscription<PropertyProcessingSuspendedIntegrationEvent>(
            PropertiesModuleMetadata.Name,
            PropertyProcessingSuspendedHandlerName)
        .WithPublishedEvent<ReservationCreatedIntegrationEvent>()
        .WithPublishedEvent<ReservationConfirmedIntegrationEvent>()
        .WithPublishedEvent<ReservationAllocationRejectedIntegrationEvent>()
        .WithPublishedEvent<ReservationCancelledIntegrationEvent>()
        .WithPublishedEvent<ReservationCheckedInIntegrationEvent>()
        .WithPublishedEvent<ReservationNoShowIntegrationEvent>()
        .WithPublishedEvent<ReservationCheckedOutIntegrationEvent>()
        .WithPublishedEvent<ExternalReservationOperationCompletedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationRequestedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationReleaseRequestedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationAmendmentRequestedIntegrationEvent>()
        .WithPublishedEvent<ReservationGuestLinkedIntegrationEvent>()
        .WithPublishedEvent<ReservationGuestStayChangedIntegrationEvent>()
        .WithPublishedEvent<ReservationArrivalReminderDueIntegrationEvent>()
        .WithPublishedEvent<ReservationArrivalReminderDueIntegrationEventV2>()
        .WithTask<RebuildReservationInventoryProjectionPayload>()
        .WithTask<RebuildReservationGuestProfilesPayload>()
        .WithTask<RebuildReservationPropertiesPayload>()
        .WithTask<DispatchReservationArrivalRemindersPayload>()
        .WithProfile(ReservationsProfiles.Default)
        .Build();
}

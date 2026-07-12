namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Modules;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Guests.Contracts;

public static class ReservationsModuleMetadata
{
    public const string Name = "reservations";
    public const string Schema = "reservations";
    public const string InventoryProjectionName = "inventory-availability";
    public const int InventoryProjectionVersion = 1;
    public const string GuestProfilesProjectionName = "guest-profiles";
    public const int GuestProfilesProjectionVersion = 1;
    public const string ProjectionWorkerGroup = "projection-workers";
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
        .WithTask<RebuildReservationInventoryProjectionPayload>()
        .WithTask<RebuildReservationGuestProfilesPayload>()
        .WithProfile(ReservationsProfiles.Default)
        .Build();
}

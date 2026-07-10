namespace Inventory.Contracts;

using Gma.Framework.Messaging;

public static class InventoryIntegrationSubjects
{
    public static string CreateUnitDefinitionChanged(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            subjectPrefix,
            InventoryModuleMetadata.Name,
            InventoryUnitDefinitionChangedIntegrationEvent.EventType,
            InventoryUnitDefinitionChangedIntegrationEvent.EventVersion);

    public static string CreateRoomSalesModeChanged(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            subjectPrefix,
            InventoryModuleMetadata.Name,
            RoomSalesModeChangedIntegrationEvent.EventType,
            RoomSalesModeChangedIntegrationEvent.EventVersion);

    public static string CreateManualBlockCreated(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            subjectPrefix,
            InventoryModuleMetadata.Name,
            ManualInventoryBlockCreatedIntegrationEvent.EventType,
            ManualInventoryBlockCreatedIntegrationEvent.EventVersion);

    public static string CreateManualBlockReleased(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            subjectPrefix,
            InventoryModuleMetadata.Name,
            ManualInventoryBlockReleasedIntegrationEvent.EventType,
            ManualInventoryBlockReleasedIntegrationEvent.EventVersion);

    public static string CreateAllocationConfirmed(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, InventoryModuleMetadata.Name, InventoryAllocationConfirmedIntegrationEvent.EventType, InventoryAllocationConfirmedIntegrationEvent.EventVersion);

    public static string CreateAllocationRejected(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, InventoryModuleMetadata.Name, InventoryAllocationRejectedIntegrationEvent.EventType, InventoryAllocationRejectedIntegrationEvent.EventVersion);

    public static string CreateAllocationReleased(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, InventoryModuleMetadata.Name, InventoryAllocationReleasedIntegrationEvent.EventType, InventoryAllocationReleasedIntegrationEvent.EventVersion);

    public static string CreateAllocationReleaseRejected(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, InventoryModuleMetadata.Name, InventoryAllocationReleaseRejectedIntegrationEvent.EventType, InventoryAllocationReleaseRejectedIntegrationEvent.EventVersion);
}

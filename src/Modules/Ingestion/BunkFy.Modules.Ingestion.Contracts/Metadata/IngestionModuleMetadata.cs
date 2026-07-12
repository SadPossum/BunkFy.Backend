namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.Permissions;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Framework.Tasks;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Properties.Contracts;

public static class IngestionModuleMetadata
{
    public const string Name = "ingestion";
    public const string Schema = "ingestion";
    public const string AdapterWorkerGroup = "ingestion-adapters";
    public const string ProjectionWorkerGroup = "projection-workers";
    public const string MaintenanceWorkerGroup = "ingestion-maintenance";
    public const string PropertyProjectionName = "properties";
    public const int PropertyProjectionVersion = 1;
    public const string ReceiptAcceptedHandlerName = "process-observation-receipt";
    public const string ReservationOperationOutcomeHandlerName = "reservation-operation-outcome";
    public const string ReservationCancelledHandlerName = "reservation-cancelled";
    public const string PropertyCreatedHandlerName = "property-created-projection";
    public const string PropertyUpdatedHandlerName = "property-updated-projection";
    public const string PropertyRetiredHandlerName = "property-retired-projection";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.Read, "Read ingestion connections, runs, receipts, and proposals.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.ConnectionsManage, "Manage ingestion connections.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.CredentialsManage, "Issue and revoke adapter ingress credentials.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.RunsManage, "Start, retry, or cancel ingestion runs.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.RawPayloadsRead, "Read sensitive raw ingestion payloads.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.RetentionManage, "Run ingestion retention operations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.ReprocessingManage, "Run and cancel retained observation reprocessing.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.LegalHoldsManage, "Inspect, place, and release ingestion legal holds.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(IngestionAdminPermissionCodes.ProposalsDecide, "Accept or reject ingestion change proposals.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
        ])
        .WithSubscription<ObservationReceiptAcceptedIntegrationEvent>(Name, ReceiptAcceptedHandlerName)
        .WithSubscription<ExternalReservationOperationCompletedIntegrationEvent>(ReservationsModuleMetadata.Name, ReservationOperationOutcomeHandlerName)
        .WithSubscription<ReservationCancelledIntegrationEvent>(ReservationsModuleMetadata.Name, ReservationCancelledHandlerName)
        .WithSubscription<PropertyCreatedIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyCreatedHandlerName)
        .WithSubscription<PropertyUpdatedIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyUpdatedHandlerName)
        .WithSubscription<PropertyRetiredIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyRetiredHandlerName)
        .WithPublishedEvent<ObservationReceiptAcceptedIntegrationEvent>()
        .WithPublishedEvent<ExternalReservationCreateRequestedIntegrationEvent>()
        .WithPublishedEvent<ExternalReservationGuestDetailsChangeRequestedIntegrationEvent>()
        .WithPublishedEvent<ExternalReservationAmendmentRequestedIntegrationEvent>()
        .WithPublishedEvent<ExternalReservationCancellationRequestedIntegrationEvent>()
        .WithTask<RunAdapterTaskPayload>()
        .WithTask<RebuildIngestionPropertiesPayload>()
        .WithTask<PurgeExpiredRawPayloadsPayload>()
        .WithTask<RedactExpiredReservationHistoryPayload>()
        .WithTask<ReprocessObservationPayload>()
        .WithProfile(IngestionProfiles.Default)
        .Build();
}

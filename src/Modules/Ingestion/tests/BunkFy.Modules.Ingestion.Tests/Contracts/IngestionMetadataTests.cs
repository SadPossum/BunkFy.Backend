namespace BunkFy.Modules.Ingestion.Tests.Contracts;

using Gma.Framework.Permissions;
using Gma.Framework.Messaging;
using Gma.Framework.FileManagement;
using Gma.Framework.Tasks;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Properties.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionMetadataTests
{
    [Fact]
    public void Descriptor_separates_sensitive_ingestion_permissions()
    {
        IReadOnlyCollection<ModulePermissionDescriptor> permissions = IngestionModuleMetadata.Descriptor.GetPermissions();

        Assert.Equal(9, permissions.Count);
        Assert.Contains(permissions, permission => permission.Code == IngestionAdminPermissionCodes.CredentialsManage);
        Assert.Contains(permissions, permission => permission.Code == IngestionAdminPermissionCodes.RawPayloadsRead);
        Assert.Contains(permissions, permission => permission.Code == IngestionAdminPermissionCodes.RetentionManage);
        Assert.Contains(permissions, permission => permission.Code == IngestionAdminPermissionCodes.ReprocessingManage);
        Assert.Contains(permissions, permission => permission.Code == IngestionAdminPermissionCodes.LegalHoldsManage);
        Assert.Contains(permissions, permission => permission.Code == IngestionAdminPermissionCodes.ProposalsDecide);
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeRequirement.Scoped, permission.ScopeRequirement));
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeGrantPolicy.Descendants, permission.ScopeGrantPolicy));
    }

    [Fact]
    public void Default_profile_requires_file_storage_and_provides_receipts()
    {
        Assert.Contains(
            IngestionProfiles.Default.Requires,
            feature => feature.Id.Value == FileManagementCompositionFeatures.Storage);
        Assert.Contains(
            IngestionProfiles.Default.Provides,
            feature => feature.Id == IngestionCompositionFeatures.ObservationReceipts);
    }

    [Fact]
    public void Connection_read_contract_never_exposes_secret_reference_identity()
    {
        string[] properties = typeof(AdapterConnectionDto).GetProperties().Select(property => property.Name).ToArray();

        Assert.Contains(nameof(AdapterConnectionDto.HasSecretReference), properties);
        Assert.DoesNotContain("SecretReference", properties);
    }

    [Fact]
    public void Credential_read_contract_never_exposes_token_or_digest_material()
    {
        string[] properties = typeof(AdapterIngressCredentialDto).GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("Token", properties);
        Assert.DoesNotContain("SecretHash", properties);
        Assert.DoesNotContain("SecretHashAlgorithm", properties);
    }

    [Fact]
    public void Proposal_lists_exclude_sensitive_diff_bodies()
    {
        Assert.DoesNotContain(
            "Diff",
            typeof(ChangeProposalSummaryDto).GetProperties().Select(property => property.Name));
        Assert.Contains(
            nameof(ChangeProposalDto.Diff),
            typeof(ChangeProposalDto).GetProperties().Select(property => property.Name));
        Assert.Contains(
            nameof(ChangeProposalDto.SensitiveHistoryStatus),
            typeof(ChangeProposalDto).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Descriptor_declares_the_adapter_worker_task()
    {
        IReadOnlyCollection<Gma.Framework.Tasks.ModuleTaskDescriptor> tasks = IngestionModuleMetadata.Descriptor.GetTasks();
        Assert.Equal(5, tasks.Count);
        Gma.Framework.Tasks.ModuleTaskDescriptor task = Assert.Single(
            tasks,
            item => item.Name == RunAdapterTaskPayload.TaskName);

        Assert.Equal(RunAdapterTaskPayload.TaskName, task.Name);
        Assert.Equal(IngestionModuleMetadata.AdapterWorkerGroup, task.WorkerGroup);
        Assert.Equal(Gma.Framework.Tasks.ModuleTaskKind.Recurring, task.Kind);
        Gma.Framework.Tasks.ModuleTaskDescriptor rebuild = Assert.Single(
            tasks,
            item => item.Name == RebuildIngestionPropertiesPayload.TaskName);
        Assert.Equal(IngestionModuleMetadata.ProjectionWorkerGroup, rebuild.WorkerGroup);
        Assert.Equal(Gma.Framework.Tasks.ModuleTaskKind.OneShot, rebuild.Kind);
        Gma.Framework.Tasks.ModuleTaskDescriptor purge = Assert.Single(
            tasks,
            item => item.Name == PurgeExpiredRawPayloadsPayload.TaskName);
        Assert.Equal(IngestionModuleMetadata.MaintenanceWorkerGroup, purge.WorkerGroup);
        Assert.Equal(Gma.Framework.Tasks.ModuleTaskKind.Recurring, purge.Kind);
        Gma.Framework.Tasks.ModuleTaskDescriptor redact = Assert.Single(
            tasks,
            item => item.Name == RedactExpiredReservationHistoryPayload.TaskName);
        Assert.Equal(IngestionModuleMetadata.MaintenanceWorkerGroup, redact.WorkerGroup);
        Assert.Equal(Gma.Framework.Tasks.ModuleTaskKind.Recurring, redact.Kind);
        Gma.Framework.Tasks.ModuleTaskDescriptor reprocess = Assert.Single(
            tasks,
            item => item.Name == ReprocessObservationPayload.TaskName);
        Assert.Equal(IngestionModuleMetadata.MaintenanceWorkerGroup, reprocess.WorkerGroup);
        Assert.Equal(Gma.Framework.Tasks.ModuleTaskKind.OneShot, reprocess.Kind);
    }

    [Fact]
    public void Descriptor_declares_the_receipt_and_reservation_handshake()
    {
        Assert.Equal(5, IngestionModuleMetadata.Descriptor.GetPublishedEvents().Count);
        Assert.Equal(6, IngestionModuleMetadata.Descriptor.GetSubscriptions().Count);
        Assert.Contains(
            IngestionModuleMetadata.Descriptor.GetPublishedEvents(),
            published => published.EventType == ExternalReservationCreateRequestedIntegrationEvent.EventType);
        Assert.Contains(
            IngestionModuleMetadata.Descriptor.GetPublishedEvents(),
            published => published.EventType == ExternalReservationAmendmentRequestedIntegrationEvent.EventType);
        Assert.Contains(
            IngestionModuleMetadata.Descriptor.GetSubscriptions(),
            subscription => subscription.ProducerModule == ReservationsModuleMetadata.Name &&
                            subscription.EventType == ExternalReservationOperationCompletedIntegrationEvent.EventType);
        Assert.Contains(
            IngestionModuleMetadata.Descriptor.GetSubscriptions(),
            subscription => subscription.ProducerModule == PropertiesModuleMetadata.Name &&
                            subscription.EventType == PropertyCreatedIntegrationEvent.EventType);
    }
}

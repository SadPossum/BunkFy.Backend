namespace BunkFy.Modules.Ingestion.Contracts;

using System.Security.Cryptography;
using System.Text;

public static class IngestionTenantTerminationMetadata
{
    public const string OwnerKey = IngestionModuleMetadata.Name;
    public const string DependencyOwnerKey = "staff";
    public const int CatalogVersion = 3;
    public const int PersonalDataCatalogVersion = 15;
    public const string ExportCatalogId = "ingestion.tenant-portability";
    public const int ExportCatalogSchemaVersion = 2;
    public const string ExportSchemaId =
        "ingestion.tenant-termination-export";
    public const int ExportSchemaVersion = 2;

    public const string AdapterConnectionRecordType = "adapter-connection";
    public const string ConnectionManagementOperationRecordType =
        "connection-management-operation";
    public const string AdapterCredentialRecordType =
        "adapter-ingress-credential-metadata";
    public const string AdapterIngressControlRecordType =
        "adapter-ingress-tenant-control";
    public const string IngestionRunRecordType = "ingestion-run";
    public const string ObservationReceiptRecordType =
        "observation-receipt";
    public const string ObservationReprocessingAttemptRecordType =
        "observation-reprocessing-attempt";
    public const string ObservationReprocessingOutputRecordType =
        "observation-reprocessing-output";
    public const string ChangeProposalRecordType = "change-proposal";
    public const string ReservationSourceLinkRecordType =
        "reservation-source-link";
    public const string ReservationDispatchRecordType =
        "reservation-dispatch";
    public const string LegalHoldRecordType = "legal-hold";
    public const string RetentionExecutionRecordType =
        "retention-execution";
    public const string AnonymisationReceiptRecordType =
        "anonymisation-receipt";
    public const string AnonymisationTombstoneRecordType =
        "anonymisation-tombstone";
    public const string LargeTextChunkRecordType = "large-text-chunk";

    public static IReadOnlyList<string> RecordTypes { get; } =
        Array.AsReadOnly(
        [
            AdapterConnectionRecordType,
            ConnectionManagementOperationRecordType,
            AdapterCredentialRecordType,
            AdapterIngressControlRecordType,
            IngestionRunRecordType,
            ObservationReceiptRecordType,
            ObservationReprocessingAttemptRecordType,
            ObservationReprocessingOutputRecordType,
            ChangeProposalRecordType,
            ReservationSourceLinkRecordType,
            ReservationDispatchRecordType,
            LegalHoldRecordType,
            RetentionExecutionRecordType,
            AnonymisationReceiptRecordType,
            AnonymisationTombstoneRecordType,
            LargeTextChunkRecordType
        ]);

    public static IReadOnlyList<string> ExportFieldIds { get; } =
        Array.AsReadOnly(
        [
            "ingestion.operations.scope-id",
            "ingestion.operations.property-id",
            "ingestion.operations.connection-id",
            "ingestion.operations.id",
            "ingestion.operations.operation-id",
            "ingestion.tenant.adapter-connection",
            "ingestion.tenant.connection-management-operation",
            "ingestion.tenant.adapter-credential-metadata",
            "ingestion.tenant.adapter-ingress-control",
            "ingestion.tenant.run",
            "ingestion.tenant.observation-evidence",
            "ingestion.tenant.reprocessing-attempt",
            "ingestion.tenant.reprocessing-output",
            "ingestion.tenant.change-proposal",
            "ingestion.tenant.reservation-source-link",
            "ingestion.tenant.reservation-dispatch",
            "ingestion.tenant.legal-hold",
            "ingestion.tenant.retention-execution",
            "ingestion.tenant.anonymisation-proof",
            "ingestion.tenant.large-text-metadata",
            "ingestion.tenant.large-text-content"
        ]);

    public static string CatalogManifest { get; } = string.Join(
        '|',
        OwnerKey,
        "contract=1",
        "phases=export,destroy",
        $"dependencies={DependencyOwnerKey}",
        "mandatory=true",
        $"catalog={CatalogVersion}",
        $"personal-data-catalog={PersonalDataCatalogVersion}",
        $"export-schema={ExportSchemaId}:{ExportSchemaVersion}",
        "records=" + string.Join(',', RecordTypes),
        "fields=" + string.Join(',', ExportFieldIds));

    public static string CatalogSha256 { get; } =
        Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(CatalogManifest)));
}

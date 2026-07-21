namespace BunkFy.Modules.Ingestion.Contracts;

public static class IngestionAdminPermissionCodes
{
    public const string Read = IngestionModuleMetadata.Name + ".read";
    public const string ConnectionsManage = IngestionModuleMetadata.Name + ".connections.manage";
    public const string CredentialsManage = IngestionModuleMetadata.Name + ".credentials.manage";
    public const string RunsManage = IngestionModuleMetadata.Name + ".runs.manage";
    public const string RawPayloadsRead = IngestionModuleMetadata.Name + ".raw-payloads.read";
    public const string SensitiveHistoryRead = IngestionModuleMetadata.Name + ".sensitive-history.read";
    public const string RetentionManage = IngestionModuleMetadata.Name + ".retention.manage";
    public const string ReprocessingManage = IngestionModuleMetadata.Name + ".reprocessing.manage";
    public const string LegalHoldsManage = IngestionModuleMetadata.Name + ".legal-holds.manage";
    public const string ProposalsDecide = IngestionModuleMetadata.Name + ".proposals.decide";
}

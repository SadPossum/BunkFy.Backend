namespace BunkFy.Modules.Ingestion.Admin.Contracts;

using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Administration;

public static class IngestionAdminPermissions
{
    public static readonly AdminPermission Read = AdminPermission.Create(IngestionAdminPermissionCodes.Read);
    public static readonly AdminPermission ConnectionsManage = AdminPermission.Create(IngestionAdminPermissionCodes.ConnectionsManage);
    public static readonly AdminPermission CredentialsManage = AdminPermission.Create(IngestionAdminPermissionCodes.CredentialsManage);
    public static readonly AdminPermission RunsManage = AdminPermission.Create(IngestionAdminPermissionCodes.RunsManage);
    public static readonly AdminPermission RawPayloadsRead = AdminPermission.Create(IngestionAdminPermissionCodes.RawPayloadsRead);
    public static readonly AdminPermission RetentionManage = AdminPermission.Create(IngestionAdminPermissionCodes.RetentionManage);
    public static readonly AdminPermission ReprocessingManage = AdminPermission.Create(IngestionAdminPermissionCodes.ReprocessingManage);
    public static readonly AdminPermission LegalHoldsManage = AdminPermission.Create(IngestionAdminPermissionCodes.LegalHoldsManage);
    public static readonly AdminPermission ProposalsDecide = AdminPermission.Create(IngestionAdminPermissionCodes.ProposalsDecide);
}

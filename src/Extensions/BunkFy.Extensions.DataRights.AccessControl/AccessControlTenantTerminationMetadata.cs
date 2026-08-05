namespace BunkFy.Extensions.DataRights.AccessControl;

using System.Security.Cryptography;
using System.Text;
using Gma.Modules.AccessControl.Contracts;

public static class AccessControlTenantTerminationMetadata
{
    public const string OwnerKey = AccessControlModuleMetadata.Name;
    public const string DependencyOwnerKey = "organizations";
    public const int CatalogVersion = 1;
    public const int PersonalDataCatalogVersion = 1;
    public const string ExportCatalogId =
        "access-control.tenant-portability";
    public const int ExportCatalogSchemaVersion = 1;
    public const string ExportSchemaId =
        "access-control.tenant-termination-export";
    public const int ExportSchemaVersion = 1;

    public const string RoleAssignmentRecordType =
        "access-role-assignment";
    public const string ProfileRecordType = "access-profile";
    public const string ProfileAssignmentRecordType =
        "access-profile-assignment";
    public const string ProfileChangeRecordType = "access-profile-change";

    public static IReadOnlyList<string> DependencyOwnerKeys { get; } =
        Array.AsReadOnly([DependencyOwnerKey]);

    public static IReadOnlyList<string> RecordTypes { get; } =
        Array.AsReadOnly(
        [
            RoleAssignmentRecordType,
            ProfileRecordType,
            ProfileAssignmentRecordType,
            ProfileChangeRecordType
        ]);

    public static IReadOnlyList<string> ExportFieldIds { get; } =
        Array.AsReadOnly(
        [
            "access-control.access-scope",
            "access-control.actor-id",
            "access-control.actor-kind",
            "access-control.assignment-id",
            "access-control.assignment-scope",
            "access-control.change-id",
            "access-control.change-kind",
            "access-control.created-at",
            "access-control.created-by-id",
            "access-control.created-by-kind",
            "access-control.description",
            "access-control.display-name",
            "access-control.expires-at",
            "access-control.last-changed-at",
            "access-control.last-changed-by-id",
            "access-control.last-changed-by-kind",
            "access-control.occurred-at",
            "access-control.permissions",
            "access-control.profile-id",
            "access-control.profile-key",
            "access-control.profile-owner-scope",
            "access-control.record-version",
            "access-control.revoked-at",
            "access-control.role-name",
            "access-control.role-permissions",
            "access-control.status",
            "access-control.subject-id",
            "access-control.subject-kind"
        ]);

    public static string CatalogManifest { get; } = string.Join(
        '|',
        OwnerKey,
        "contract=1",
        "phases=export,destroy",
        "dependencies=" + string.Join(',', DependencyOwnerKeys),
        "mandatory=true",
        $"catalog={CatalogVersion}",
        $"personal-data-catalog={PersonalDataCatalogVersion}",
        $"export-schema={ExportSchemaId}:{ExportSchemaVersion}",
        "scope-lifecycle=access-control:v1",
        "records=" + string.Join(',', RecordTypes),
        "fields=" + string.Join(',', ExportFieldIds));

    public static string CatalogSha256 { get; } =
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(CatalogManifest)));
}

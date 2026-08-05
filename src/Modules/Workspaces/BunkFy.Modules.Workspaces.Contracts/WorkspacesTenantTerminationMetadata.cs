namespace BunkFy.Modules.Workspaces.Contracts;

using System.Security.Cryptography;
using System.Text;

public static class WorkspacesTenantTerminationMetadata
{
    public const string OwnerKey = "workspaces";
    public const string PropertiesDestroyDependencyOwnerKey = "properties";
    public const string TaskRuntimeDestroyDependencyOwnerKey = "task-runtime";
    public const int CatalogVersion = 9;
    public const int PersonalDataCatalogVersion = 9;
    public const string ExportSchemaId =
        "workspaces.tenant-termination-export";
    public const int ExportSchemaVersion = 1;

    public static IReadOnlyList<string> DestroyDependencyOwnerKeys { get; } =
        Array.AsReadOnly(
        [
            PropertiesDestroyDependencyOwnerKey,
            TaskRuntimeDestroyDependencyOwnerKey
        ]);

    public static string CatalogManifest { get; } = string.Join(
        '|',
        OwnerKey,
        "contract=1",
        "phases=freeze,export,destroy,restore",
        "freeze-dependencies=",
        "export-dependencies=",
        "destroy-dependencies=" +
            string.Join(',', DestroyDependencyOwnerKeys),
        "restore-dependencies=",
        "mandatory=true",
        $"catalog={CatalogVersion}",
        $"personal-data-catalog={PersonalDataCatalogVersion}",
        $"export-schema={ExportSchemaId}:{ExportSchemaVersion}");

    public static string CatalogSha256 { get; } =
        Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(CatalogManifest)));
}

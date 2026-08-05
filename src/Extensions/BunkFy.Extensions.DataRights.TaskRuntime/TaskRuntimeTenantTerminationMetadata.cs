namespace BunkFy.Extensions.DataRights.TaskRuntime;

using System.Security.Cryptography;
using System.Text;
using Gma.Modules.TaskRuntime.Contracts;

public static class TaskRuntimeTenantTerminationMetadata
{
    public const string OwnerKey = TaskRuntimeModuleMetadata.Name;
    public const string DependencyOwnerKey = "access-control";
    public const int CatalogVersion = 1;
    public const int PersonalDataCatalogVersion = 1;
    public const string RunRecordType = "task-run-operational-copy";
    public const string ControlMessageRecordType =
        "task-control-message-operational-copy";

    public static IReadOnlyList<string> DependencyOwnerKeys { get; } =
        Array.AsReadOnly([DependencyOwnerKey]);

    public static IReadOnlyList<string> RecordTypes { get; } =
        Array.AsReadOnly([RunRecordType, ControlMessageRecordType]);

    public static IReadOnlyList<string> PersonalDataFieldIds { get; } =
        Array.AsReadOnly(
        [
            "task-runtime.actor-id",
            "task-runtime.correlation-id",
            "task-runtime.deduplication-key",
            "task-runtime.operational-message",
            "task-runtime.payload",
            "task-runtime.scope-id",
            "task-runtime.worker-id"
        ]);

    public static string CatalogManifest { get; } = string.Join(
        '|',
        OwnerKey,
        "contract=1",
        "phases=destroy",
        "dependencies=" + string.Join(',', DependencyOwnerKeys),
        "mandatory=true",
        $"catalog={CatalogVersion}",
        $"personal-data-catalog={PersonalDataCatalogVersion}",
        "scope-lifecycle=task-runtime:v1",
        "portability=excluded-operational-copy",
        "records=" + string.Join(',', RecordTypes),
        "fields=" + string.Join(',', PersonalDataFieldIds));

    public static string CatalogSha256 { get; } =
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(CatalogManifest)));
}

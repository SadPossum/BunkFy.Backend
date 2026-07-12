namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Purge expired raw observation payloads for one tenant scope.")]
[TaskKind(ModuleTaskKind.Recurring)]
[TaskWorkerGroup(IngestionModuleMetadata.MaintenanceWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record PurgeExpiredRawPayloadsPayload(
    int BatchSize = PurgeExpiredRawPayloadsPayload.DefaultBatchSize,
    int MaxBatches = PurgeExpiredRawPayloadsPayload.DefaultMaxBatches,
    int StaleClaimMinutes = PurgeExpiredRawPayloadsPayload.DefaultStaleClaimMinutes) : ITaskPayload
{
    public const string TaskName = "purge-expired-raw-payloads";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 25;
    public const int MaximumBatchSize = 500;
    public const int DefaultMaxBatches = 4;
    public const int MaximumBatches = 100;
    public const int DefaultStaleClaimMinutes = 15;
    public const int MinimumStaleClaimMinutes = 5;
    public const int MaximumStaleClaimMinutes = 1440;
}

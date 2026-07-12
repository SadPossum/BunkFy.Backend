namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Redact expired normalized reservation history for one tenant scope.")]
[TaskKind(ModuleTaskKind.Recurring)]
[TaskWorkerGroup(IngestionModuleMetadata.MaintenanceWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RedactExpiredReservationHistoryPayload(
    int BatchSize = RedactExpiredReservationHistoryPayload.DefaultBatchSize,
    int MaxBatches = RedactExpiredReservationHistoryPayload.DefaultMaxBatches) : ITaskPayload
{
    public const string TaskName = "redact-expired-reservation-history";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
    public const int MaximumBatchSize = 500;
    public const int DefaultMaxBatches = 10;
    public const int MaximumBatches = 100;
}

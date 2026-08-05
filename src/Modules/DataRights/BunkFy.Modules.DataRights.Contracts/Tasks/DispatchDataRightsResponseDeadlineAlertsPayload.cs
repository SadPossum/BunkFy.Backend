namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Dispatch due-soon and overdue Guest Rights response deadline alerts for one tenant scope.")]
[TaskKind(ModuleTaskKind.Recurring)]
[TaskWorkerGroup(DataRightsModuleMetadata.DeadlineAlertWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record DispatchDataRightsResponseDeadlineAlertsPayload(
    int BatchSize = DispatchDataRightsResponseDeadlineAlertsPayload.DefaultBatchSize,
    int MaxBatches = DispatchDataRightsResponseDeadlineAlertsPayload.DefaultMaxBatches)
    : ITaskPayload
{
    public const string TaskName = "dispatch-data-rights-response-deadline-alerts";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
    public const int MaximumBatchSize = 500;
    public const int DefaultMaxBatches = 4;
    public const int MaximumBatches = 20;
}

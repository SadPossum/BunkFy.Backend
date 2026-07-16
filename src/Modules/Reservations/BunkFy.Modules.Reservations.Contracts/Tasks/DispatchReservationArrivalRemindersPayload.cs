namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Dispatch due expected-arrival reminders for one tenant scope.")]
[TaskKind(ModuleTaskKind.Recurring)]
[TaskWorkerGroup(ReservationsModuleMetadata.ReminderWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record DispatchReservationArrivalRemindersPayload(
    int BatchSize = DispatchReservationArrivalRemindersPayload.DefaultBatchSize,
    int MaxBatches = DispatchReservationArrivalRemindersPayload.DefaultMaxBatches) : ITaskPayload
{
    public const string TaskName = "dispatch-reservation-arrival-reminders";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
    public const int MaximumBatchSize = 500;
    public const int DefaultMaxBatches = 4;
    public const int MaximumBatches = 20;
}

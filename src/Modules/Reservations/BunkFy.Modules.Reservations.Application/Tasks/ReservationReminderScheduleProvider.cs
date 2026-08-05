namespace BunkFy.Modules.Reservations.Application.Tasks;

using System.Runtime.CompilerServices;
using System.Text.Json;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Tasks;

internal sealed class ReservationReminderScheduleProvider(IReservationArrivalReminderRepository reminders)
    : ITaskScheduleProvider
{
    public async IAsyncEnumerable<ScheduledTaskDefinition> GetSchedulesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (string scopeId in reminders
            .StreamScheduleScopeIdsAsync(cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new(
                "properties-refresh",
                ReservationsModuleMetadata.Name,
                RebuildReservationPropertiesPayload.TaskName,
                JsonSerializer.Serialize(new RebuildReservationPropertiesPayload()),
                TimeSpan.FromHours(24),
                ReservationsModuleMetadata.ProjectionWorkerGroup,
                scopeId,
                maxAttempts: 3,
                RebuildReservationPropertiesPayload.PayloadVersion,
                runOnStart: true);
            yield return new(
                "arrival-reminders",
                ReservationsModuleMetadata.Name,
                DispatchReservationArrivalRemindersPayload.TaskName,
                JsonSerializer.Serialize(new DispatchReservationArrivalRemindersPayload()),
                TimeSpan.FromMinutes(1),
                ReservationsModuleMetadata.ReminderWorkerGroup,
                scopeId,
                maxAttempts: 3,
                DispatchReservationArrivalRemindersPayload.PayloadVersion,
                runOnStart: true);
        }
    }
}

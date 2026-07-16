namespace BunkFy.Modules.Reservations.Application.Tasks;

using System.Text.Json;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Tasks;

internal sealed class ReservationReminderScheduleProvider(IReservationArrivalReminderRepository reminders)
    : ITaskScheduleProvider
{
    public async Task<IReadOnlyList<ScheduledTaskDefinition>> GetSchedulesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> scopes = await reminders.ListScheduleScopeIdsAsync(cancellationToken)
            .ConfigureAwait(false);
        List<ScheduledTaskDefinition> schedules = new(scopes.Count * 2);

        foreach (string scopeId in scopes)
        {
            schedules.Add(new(
                "properties-refresh",
                ReservationsModuleMetadata.Name,
                RebuildReservationPropertiesPayload.TaskName,
                JsonSerializer.Serialize(new RebuildReservationPropertiesPayload()),
                TimeSpan.FromHours(24),
                ReservationsModuleMetadata.ProjectionWorkerGroup,
                scopeId,
                maxAttempts: 3,
                RebuildReservationPropertiesPayload.PayloadVersion,
                runOnStart: true));
            schedules.Add(new(
                "arrival-reminders",
                ReservationsModuleMetadata.Name,
                DispatchReservationArrivalRemindersPayload.TaskName,
                JsonSerializer.Serialize(new DispatchReservationArrivalRemindersPayload()),
                TimeSpan.FromMinutes(1),
                ReservationsModuleMetadata.ReminderWorkerGroup,
                scopeId,
                maxAttempts: 3,
                DispatchReservationArrivalRemindersPayload.PayloadVersion,
                runOnStart: true));
        }

        return schedules;
    }
}

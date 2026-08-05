namespace BunkFy.Modules.DataRights.Application.Tasks;

using System.Runtime.CompilerServices;
using System.Text.Json;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Tasks;

internal sealed class DataRightsResponseDeadlineAlertScheduleProvider(
    IDataRightsResponseDeadlineAlertRepository alerts)
    : ITaskScheduleProvider
{
    public async IAsyncEnumerable<ScheduledTaskDefinition> GetSchedulesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (string scopeId in alerts
            .StreamScheduleScopeIdsAsync(cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new ScheduledTaskDefinition(
                "response-deadline-alerts",
                DataRightsModuleMetadata.Name,
                DispatchDataRightsResponseDeadlineAlertsPayload.TaskName,
                JsonSerializer.Serialize(
                    new DispatchDataRightsResponseDeadlineAlertsPayload()),
                TimeSpan.FromMinutes(1),
                DataRightsModuleMetadata.DeadlineAlertWorkerGroup,
                scopeId,
                maxAttempts: 3,
                DispatchDataRightsResponseDeadlineAlertsPayload.PayloadVersion,
                runOnStart: true);
        }
    }
}

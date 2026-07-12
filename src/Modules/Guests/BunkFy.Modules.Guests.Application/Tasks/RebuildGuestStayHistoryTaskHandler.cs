namespace BunkFy.Modules.Guests.Application.Tasks;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using BunkFy.Modules.Guests.Contracts;

internal sealed class RebuildGuestStayHistoryTaskHandler(
    IReservationGuestStayProjectionExportSource source,
    IProjectionRebuildWriter<ReservationGuestStayProjectionExport> writer,
    TaskProjectionRebuildRunner<ReservationGuestStayProjectionExport> runner)
    : ITaskHandler<RebuildGuestStayHistoryPayload>
{
    public Task HandleAsync(
        RebuildGuestStayHistoryPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ProjectionRebuildRequest request = new(
            GuestsModuleMetadata.StayHistoryProjectionName,
            payload.ProjectionVersion,
            payload.BatchSize,
            payload.DryRun,
            payload.Cursor);
        return runner.RunAsync(
            GuestsModuleMetadata.Name,
            request,
            source,
            writer,
            context,
            scopeAware: true,
            cancellationToken);
    }
}

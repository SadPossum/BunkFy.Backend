namespace BunkFy.Modules.Reservations.Application.Tasks;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;

internal sealed class ExecuteReservationGuestRecordLinkTaskHandler(
    ITaskCommandDispatcher commandDispatcher)
    : ITaskHandler<ExecuteReservationGuestRecordLinkPayload>
{
    public async Task HandleAsync(
        ExecuteReservationGuestRecordLinkPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (payload.OperationId == Guid.Empty ||
            payload.PropertyId == Guid.Empty ||
            payload.ReservationId == Guid.Empty ||
            payload.DispatchRevision <= 0 ||
            string.IsNullOrWhiteSpace(context.ScopeId))
        {
            throw new InvalidOperationException(
                ReservationsApplicationErrors.GuestRecordLinkTaskInvalid.Code);
        }

        Result<ReservationGuestRecordLinkProcessDto> advanced =
            await commandDispatcher.DispatchAsync<
                AdvanceReservationGuestRecordLinkCommand,
                ReservationGuestRecordLinkProcessDto>(
                    context,
                    new(
                        payload.OperationId,
                        payload.PropertyId,
                        payload.ReservationId,
                        payload.DispatchRevision,
                        context.Attempt >=
                            ExecuteReservationGuestRecordLinkPayload.MaximumAttempts),
                    cancellationToken).ConfigureAwait(false);
        if (advanced.IsFailure)
        {
            if (advanced.Error ==
                ReservationsApplicationErrors.GuestRecordLinkProcessNotFound)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{advanced.Error.Code}: {advanced.Error.Message}");
        }
    }
}

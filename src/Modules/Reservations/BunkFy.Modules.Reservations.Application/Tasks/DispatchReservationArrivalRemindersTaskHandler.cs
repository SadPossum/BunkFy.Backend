namespace BunkFy.Modules.Reservations.Application.Tasks;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;

internal sealed class DispatchReservationArrivalRemindersTaskHandler(ITaskCommandDispatcher commandDispatcher)
    : ITaskHandler<DispatchReservationArrivalRemindersPayload>
{
    public async Task HandleAsync(
        DispatchReservationArrivalRemindersPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (payload.BatchSize is <= 0 or > DispatchReservationArrivalRemindersPayload.MaximumBatchSize ||
            payload.MaxBatches is <= 0 or > DispatchReservationArrivalRemindersPayload.MaximumBatches ||
            string.IsNullOrWhiteSpace(context.ScopeId))
        {
            throw new InvalidOperationException(ReservationsApplicationErrors.ReminderTaskOptionsInvalid.Code);
        }

        for (int batch = 0; batch < payload.MaxBatches; batch++)
        {
            Result<ReservationArrivalReminderDispatchBatchResult> dispatched = await commandDispatcher
                .DispatchAsync<DispatchReservationArrivalRemindersCommand, ReservationArrivalReminderDispatchBatchResult>(
                    context,
                    new(payload.BatchSize),
                    cancellationToken).ConfigureAwait(false);
            if (dispatched.IsFailure)
            {
                throw new InvalidOperationException($"{dispatched.Error.Code}: {dispatched.Error.Message}");
            }

            if (dispatched.Value.ProcessedCount < payload.BatchSize)
            {
                return;
            }
        }
    }
}

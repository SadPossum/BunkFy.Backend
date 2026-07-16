namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class DispatchReservationArrivalRemindersCommandHandler(
    IReservationArrivalReminderRepository reminders,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock)
    : ICommandHandler<DispatchReservationArrivalRemindersCommand, ReservationArrivalReminderDispatchBatchResult>
{
    public async Task<Result<ReservationArrivalReminderDispatchBatchResult>> HandleAsync(
        DispatchReservationArrivalRemindersCommand command,
        CancellationToken cancellationToken)
    {
        if (command.BatchSize is <= 0 or > DispatchReservationArrivalRemindersPayload.MaximumBatchSize)
        {
            return Result.Failure<ReservationArrivalReminderDispatchBatchResult>(
                ReservationsApplicationErrors.ReminderTaskOptionsInvalid);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        ReservationArrivalReminderClaimResult claimed = await reminders
            .ClaimDueAsync(nowUtc, command.BatchSize, cancellationToken)
            .ConfigureAwait(false);
        IOutboxWriter outbox = outboxWriters.GetRequired(ReservationsModuleMetadata.Name);

        foreach (ReservationArrivalReminderDispatch reminder in claimed.Dispatches)
        {
            await outbox.EnqueueAsync(
                new ReservationArrivalReminderDueIntegrationEvent(
                    reminder.ReminderId,
                    reminder.ScopeId,
                    nowUtc,
                    reminder.ReservationId,
                    reminder.PropertyId,
                    reminder.PrimaryGuestName,
                    reminder.Arrival,
                    reminder.ExpectedArrivalTime,
                    reminder.TimeZoneId,
                    reminder.DetailsRevision),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new ReservationArrivalReminderDispatchBatchResult(
            claimed.ProcessedCount,
            claimed.Dispatches.Count));
    }
}

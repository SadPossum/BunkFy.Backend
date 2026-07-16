namespace BunkFy.Modules.Reservations.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record DispatchReservationArrivalRemindersCommand(int BatchSize)
    : ITransactionalCommand<ReservationArrivalReminderDispatchBatchResult>;

public sealed record ReservationArrivalReminderDispatchBatchResult(
    int ProcessedCount,
    int DispatchedCount);

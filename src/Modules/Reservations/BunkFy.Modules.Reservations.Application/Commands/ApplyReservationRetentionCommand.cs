namespace BunkFy.Modules.Reservations.Application.Commands;

using Gma.Framework.Cqrs;

internal sealed record ApplyReservationRetentionCommand(
    Guid ExecutionId,
    Guid PropertyId,
    Guid ReservationId,
    long ExpectedReservationVersion,
    long ExpectedDetailsRevision)
    : ITransactionalCommand<ReservationRetentionMutationResult>;

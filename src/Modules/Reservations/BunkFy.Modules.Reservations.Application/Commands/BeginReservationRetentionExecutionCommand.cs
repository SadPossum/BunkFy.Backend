namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;

internal sealed record BeginReservationRetentionExecutionCommand(
    RetentionContributionRequest Request)
    : ITransactionalCommand<ReservationRetentionExecutionStart>;

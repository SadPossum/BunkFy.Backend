namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Cqrs;

public sealed record RestoreReservationAnonymisationCommand(
    DataRightsAnonymisationRestoreRequest Request)
    : ITransactionalCommand<ReservationAnonymisationRestoreReceipt>;

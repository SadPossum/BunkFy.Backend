namespace BunkFy.Modules.Reservations.Application.Queries;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetReservationGuestRecordLinkQuery(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId) : IQuery<ReservationGuestRecordLinkProcessDto>;

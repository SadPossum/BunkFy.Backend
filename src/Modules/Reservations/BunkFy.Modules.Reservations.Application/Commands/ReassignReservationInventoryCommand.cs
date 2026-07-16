namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record ReassignReservationInventoryCommand(
    Guid PropertyId,
    Guid ReservationId,
    Guid AmendmentRequestId,
    IReadOnlyCollection<Guid> InventoryUnitIds,
    long ExpectedDetailsRevision,
    string ActorId)
    : ITransactionalCommand<ReservationDto>;

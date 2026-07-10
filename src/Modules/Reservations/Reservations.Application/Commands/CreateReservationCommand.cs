namespace Reservations.Application.Commands;

using Gma.Framework.Cqrs;
using Reservations.Contracts;

public sealed record CreateReservationCommand(
    Guid PropertyId,
    DateOnly Arrival,
    DateOnly Departure,
    IReadOnlyCollection<Guid> InventoryUnitIds,
    string PrimaryGuestName,
    string? Email,
    string? Phone,
    int GuestCount,
    ReservationSourceKind SourceKind,
    string? SourceSystem,
    string? SourceReference,
    string? Notes)
    : ITransactionalCommand<ReservationDto>;

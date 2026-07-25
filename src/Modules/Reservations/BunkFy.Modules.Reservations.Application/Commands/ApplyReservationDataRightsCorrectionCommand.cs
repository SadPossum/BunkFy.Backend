namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyReservationDataRightsCorrectionCommand(
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid CaseId,
    long ApprovalRevision,
    Guid ReservationId,
    long ExpectedVersion,
    long ExpectedDetailsRevision,
    string PrimaryGuestName,
    string? Email,
    string? Phone,
    int GuestCount,
    string? Notes,
    TimeOnly? ExpectedArrivalTime,
    TimeOnly? ExpectedDepartureTime,
    string ActorId) : ITransactionalCommand<ReservationDataRightsCorrectionReceiptDto>;

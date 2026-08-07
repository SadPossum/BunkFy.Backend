namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Results;

public interface IReservationGuestRecordLinkCapability
{
    Task<Result<ReservationGuestRecordLinkPreparationDto>> PrepareAsync(
        PrepareReservationGuestRecordLinkRequest request,
        CancellationToken cancellationToken);

    Task<Result<ReservationGuestRecordLinkProcessDto>> ConfirmGuestAsync(
        ConfirmReservationGuestRecordLinkRequest request,
        CancellationToken cancellationToken);

    Task<Result<ReservationGuestRecordLinkProcessDto>> RetryAsync(
        RetryReservationGuestRecordLinkRequest request,
        CancellationToken cancellationToken);

    Task<Result<ReservationGuestRecordLinkProcessDto>> GetAsync(
        GetReservationGuestRecordLinkRequest request,
        CancellationToken cancellationToken);
}

public sealed record PrepareReservationGuestRecordLinkRequest(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId,
    long ExpectedReservationVersion,
    string ActorId);

public sealed record ConfirmReservationGuestRecordLinkRequest(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId,
    Guid CreationConfirmationId);

public sealed record RetryReservationGuestRecordLinkRequest(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId);

public sealed record GetReservationGuestRecordLinkRequest(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId);

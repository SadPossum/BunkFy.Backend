namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Contracts;

public interface IReservationExternalOperationRepository
{
    Task<ReservationExternalOperationRecord?> GetAsync(Guid operationId, CancellationToken cancellationToken);
    Task AddAsync(ReservationExternalOperationRecord operation, CancellationToken cancellationToken);
}

public sealed record ReservationExternalOperationRecord(
    Guid OperationId,
    string ScopeId,
    Guid ReceiptId,
    Guid ConnectionId,
    Guid PropertyId,
    ExternalReservationOperationKind Kind,
    string RequestFingerprint,
    ExternalReservationOperationOutcome Outcome,
    Guid? ReservationId,
    long? DetailsRevision,
    long? ReservationVersion,
    string? ErrorCode,
    DateTimeOffset CompletedAtUtc);

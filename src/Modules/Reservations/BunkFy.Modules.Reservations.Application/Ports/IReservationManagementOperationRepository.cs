namespace BunkFy.Modules.Reservations.Application.Ports;

public interface IReservationManagementOperationRepository
{
    Task<ReservationManagementOperationRecord?> GetAsync(
        Guid reservationId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        ReservationManagementOperationRecord operation,
        CancellationToken cancellationToken);
}

public sealed record ReservationManagementOperationRecord(
    Guid OperationId,
    string ScopeId,
    Guid PropertyId,
    Guid ReservationId,
    ReservationManagementOperationKind Kind,
    long? ExpectedVersion,
    long? ExpectedDetailsRevision,
    DateOnly? BusinessDate,
    DateTimeOffset CreatedAtUtc)
{
    public bool MatchesLifecycle(
        ReservationManagementOperationKind kind,
        long expectedVersion,
        DateOnly? businessDate) =>
        this.Kind == kind &&
        this.ExpectedVersion == expectedVersion &&
        this.ExpectedDetailsRevision is null &&
        this.BusinessDate == businessDate;

    public bool MatchesGuestDetails(long expectedDetailsRevision) =>
        this.Kind == ReservationManagementOperationKind.GuestDetails &&
        this.ExpectedVersion is null &&
        this.ExpectedDetailsRevision == expectedDetailsRevision &&
        this.BusinessDate is null;
}

public enum ReservationManagementOperationKind
{
    Unknown = 0,
    Cancel = 1,
    CheckIn = 2,
    NoShow = 3,
    CheckOut = 4,
    GuestDetails = 5
}

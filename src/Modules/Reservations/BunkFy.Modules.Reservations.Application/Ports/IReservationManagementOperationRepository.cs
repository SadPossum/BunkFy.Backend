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
    long ExpectedVersion,
    DateOnly? BusinessDate,
    DateTimeOffset CreatedAtUtc)
{
    public bool Matches(
        ReservationManagementOperationKind kind,
        long expectedVersion,
        DateOnly? businessDate) =>
        this.Kind == kind &&
        this.ExpectedVersion == expectedVersion &&
        this.BusinessDate == businessDate;
}

public enum ReservationManagementOperationKind
{
    Unknown = 0,
    Cancel = 1,
    CheckIn = 2,
    NoShow = 3,
    CheckOut = 4
}

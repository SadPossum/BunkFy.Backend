namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Contracts;

public interface IGuestManagementOperationRepository
{
    Task<GuestManagementOperationRecord?> GetAsync(
        Guid guestId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        GuestManagementOperationRecord operation,
        CancellationToken cancellationToken);

    Task DeleteForGuestAsync(Guid guestId, CancellationToken cancellationToken);
}

public sealed record GuestManagementOperationRecord(
    Guid OperationId,
    string ScopeId,
    Guid PropertyId,
    Guid GuestId,
    GuestManagementOperationKind Kind,
    long ExpectedVersion,
    string? RequestFingerprint,
    GuestStatus ResultStatus,
    long ResultVersion,
    DateTimeOffset CompletedAtUtc)
{
    public bool MatchesUpdate(
        Guid propertyId,
        long expectedVersion,
        string requestFingerprint) =>
        this.PropertyId == propertyId &&
        this.Kind == GuestManagementOperationKind.Update &&
        this.ExpectedVersion == expectedVersion &&
        string.Equals(this.RequestFingerprint, requestFingerprint, StringComparison.Ordinal);

    public bool MatchesArchive(Guid propertyId, long expectedVersion) =>
        this.PropertyId == propertyId &&
        this.Kind == GuestManagementOperationKind.Archive &&
        this.ExpectedVersion == expectedVersion &&
        this.RequestFingerprint is null;

    public GuestMutationReceiptDto ToMutationReceipt() => new(
        this.GuestId,
        this.ResultStatus,
        this.ResultVersion,
        this.CompletedAtUtc);
}

public enum GuestManagementOperationKind
{
    Unknown = 0,
    Update = 1,
    Archive = 2
}

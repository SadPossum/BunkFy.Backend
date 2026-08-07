namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Contracts;

public interface IStaffProfileUpdateOperationRepository
{
    Task<StaffProfileUpdateOperationRecord?> GetAsync(
        Guid staffMemberId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        StaffProfileUpdateOperationRecord operation,
        CancellationToken cancellationToken);

    Task DeleteForStaffMemberAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);
}

public sealed record StaffProfileUpdateOperationRecord(
    Guid OperationId,
    string ScopeId,
    Guid StaffMemberId,
    long ExpectedVersion,
    string RequestFingerprint,
    StaffStatus ResultStatus,
    long ResultVersion,
    DateTimeOffset CompletedAtUtc)
{
    public bool Matches(
        Guid staffMemberId,
        long expectedVersion,
        string requestFingerprint) =>
        this.StaffMemberId == staffMemberId &&
        this.ExpectedVersion == expectedVersion &&
        string.Equals(
            this.RequestFingerprint,
            requestFingerprint,
            StringComparison.Ordinal);

    public StaffProfileMutationReceiptDto ToReceipt() => new(
        this.StaffMemberId,
        this.ResultStatus,
        this.ResultVersion,
        this.CompletedAtUtc);
}

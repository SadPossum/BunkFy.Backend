namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Contracts;

public interface IStaffMemberMutationOperationRepository
{
    Task<StaffMemberMutationOperationRecord?> GetAsync(
        Guid staffMemberId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        StaffMemberMutationOperationRecord operation,
        CancellationToken cancellationToken);

    Task DeleteForStaffMemberAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);
}

public sealed record StaffMemberMutationOperationRecord(
    Guid OperationId,
    string ScopeId,
    Guid StaffMemberId,
    StaffMemberMutationKind Kind,
    long ExpectedVersion,
    string RequestFingerprint,
    StaffStatus ResultStatus,
    long ResultVersion,
    DateTimeOffset CompletedAtUtc)
{
    public bool Matches(
        StaffMemberMutationKind kind,
        Guid staffMemberId,
        long expectedVersion,
        string requestFingerprint) =>
        this.Kind == kind &&
        this.StaffMemberId == staffMemberId &&
        this.ExpectedVersion == expectedVersion &&
        string.Equals(
            this.RequestFingerprint,
            requestFingerprint,
            StringComparison.Ordinal);

    public StaffMemberMutationReceiptDto ToReceipt() => new(
        this.StaffMemberId,
        this.ResultStatus,
        this.ResultVersion,
        this.CompletedAtUtc);
}

public enum StaffMemberMutationKind
{
    ProfileUpdate = 1,
    AuthSubjectChange = 2
}

namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffAnonymisationRestoreStateReader
{
    Task<StaffAnonymisationRestoreState?> ReadAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken);
}

public sealed record StaffAnonymisationRestoreState(
    Guid StaffMemberId,
    long Version,
    StaffAnonymisationRestoreRecordState State,
    string? AuthSubjectId,
    DateTimeOffset? AnonymisedAtUtc);

public enum StaffAnonymisationRestoreRecordState
{
    Unknown = 0,
    Active = 1,
    Suspended = 2,
    Departed = 3,
    Anonymised = 4
}

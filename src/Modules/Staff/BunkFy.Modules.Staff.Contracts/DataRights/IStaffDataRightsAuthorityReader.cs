namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffDataRightsAuthorityReader
{
    Task<StaffDataRightsAuthorityState?> ReadAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken);
}

public sealed record StaffDataRightsAuthorityState(
    Guid StaffMemberId,
    long Version,
    StaffDataRightsAuthorityRecordState State);

public enum StaffDataRightsAuthorityRecordState
{
    Unknown = 0,
    Active = 1,
    Suspended = 2,
    Departed = 3,
    Anonymised = 4
}

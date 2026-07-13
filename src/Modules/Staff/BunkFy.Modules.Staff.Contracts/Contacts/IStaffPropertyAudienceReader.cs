namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffPropertyAudienceReader
{
    Task<IReadOnlyList<string>> ListActiveAuthSubjectIdsAsync(
        string scopeId,
        Guid propertyId,
        CancellationToken cancellationToken);

    Task<string?> GetAuthSubjectIdAsync(
        string scopeId,
        Guid staffMemberId,
        CancellationToken cancellationToken);
}

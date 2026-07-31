namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffNotificationRecipientResolver
{
    Task<IReadOnlyList<StaffNotificationRecipient>> ResolveActiveAsync(
        string scopeId,
        IReadOnlyCollection<string> authSubjectIds,
        CancellationToken cancellationToken);
}

public sealed record StaffNotificationRecipient(
    Guid StaffMemberId,
    string AuthSubjectId);

public static class StaffNotificationRecipientContract
{
    public const int MaximumCandidateCount = 200;
}

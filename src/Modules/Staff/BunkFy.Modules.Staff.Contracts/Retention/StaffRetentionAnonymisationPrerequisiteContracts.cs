namespace BunkFy.Modules.Staff.Contracts;

public static class StaffRetentionAnonymisationPrerequisiteContract
{
    public const int CurrentVersion = 2;
    public const int ContributorKeyMaxLength = 64;
    public const int OutcomeCodeMaxLength = 100;
}

public enum StaffRetentionAnonymisationPrerequisiteStatus
{
    Unknown = 0,
    Completed = 1,
    Blocked = 2,
    RetryRequired = 3
}

public sealed record StaffRetentionAnonymisationPrerequisiteRequest(
    int ContractVersion,
    Guid ExecutionId,
    string TenantId,
    Guid StaffMemberId,
    long SelectedStaffVersion);

public sealed record StaffRetentionAnonymisationPrerequisiteResult(
    int ContractVersion,
    StaffRetentionAnonymisationPrerequisiteStatus Status,
    string OutcomeCode)
{
    public static StaffRetentionAnonymisationPrerequisiteResult Completed() =>
        new(
            StaffRetentionAnonymisationPrerequisiteContract.CurrentVersion,
            StaffRetentionAnonymisationPrerequisiteStatus.Completed,
            "completed");

    public static StaffRetentionAnonymisationPrerequisiteResult Blocked(
        string outcomeCode) =>
        new(
            StaffRetentionAnonymisationPrerequisiteContract.CurrentVersion,
            StaffRetentionAnonymisationPrerequisiteStatus.Blocked,
            outcomeCode);

    public static StaffRetentionAnonymisationPrerequisiteResult RetryRequired(
        string outcomeCode) =>
        new(
            StaffRetentionAnonymisationPrerequisiteContract.CurrentVersion,
            StaffRetentionAnonymisationPrerequisiteStatus.RetryRequired,
            outcomeCode);
}

public interface IStaffRetentionAnonymisationPrerequisite
{
    string ContributorKey { get; }

    Task<StaffRetentionAnonymisationPrerequisiteResult> PrepareAsync(
        StaffRetentionAnonymisationPrerequisiteRequest request,
        CancellationToken cancellationToken);

    Task<StaffRetentionAnonymisationPrerequisiteResult> VerifyAsync(
        StaffRetentionAnonymisationPrerequisiteRequest request,
        CancellationToken cancellationToken);
}

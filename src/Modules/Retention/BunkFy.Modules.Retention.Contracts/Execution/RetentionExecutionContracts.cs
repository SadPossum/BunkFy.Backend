namespace BunkFy.Modules.Retention.Contracts;

public static class RetentionExecutionContract
{
    public const int CurrentVersion = 1;
    public const int KeyMaxLength = 64;
    public const int OutcomeCodeMaxLength = 100;
    public static TimeSpan MaximumContributorExecutionTimeout { get; } =
        TimeSpan.FromHours(1);
}

public enum RetentionTargetScopeKind
{
    Unknown = 0,
    Tenant = 1,
    Property = 2
}

public enum RetentionContributionStatus
{
    Unknown = 0,
    Completed = 1,
    Blocked = 2,
    Failed = 3
}

public sealed record RetentionScheduleDescriptor
{
    public RetentionScheduleDescriptor(
        string ownerKey,
        string dataClassKey,
        RetentionTargetScopeKind targetScopeKind,
        int executionPolicyVersion,
        TimeSpan interval,
        int maxAttempts = 3,
        TimeSpan? executionTimeout = null)
    {
        this.OwnerKey = NormalizeKey(ownerKey, nameof(ownerKey));
        this.DataClassKey = NormalizeKey(dataClassKey, nameof(dataClassKey));
        this.TargetScopeKind = targetScopeKind is
            RetentionTargetScopeKind.Tenant or RetentionTargetScopeKind.Property
                ? targetScopeKind
                : throw new ArgumentOutOfRangeException(nameof(targetScopeKind));
        this.ExecutionPolicyVersion = executionPolicyVersion > 0
            ? executionPolicyVersion
            : throw new ArgumentOutOfRangeException(nameof(executionPolicyVersion));
        this.Interval = interval > TimeSpan.Zero
            ? interval
            : throw new ArgumentOutOfRangeException(nameof(interval));
        this.MaxAttempts = maxAttempts is > 0 and <= 10
            ? maxAttempts
            : throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        this.ExecutionTimeout = executionTimeout ?? TimeSpan.FromMinutes(5);
        if (this.ExecutionTimeout <= TimeSpan.Zero ||
            this.ExecutionTimeout >
                RetentionExecutionContract.MaximumContributorExecutionTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(executionTimeout));
        }
    }

    public string OwnerKey { get; }
    public string DataClassKey { get; }
    public RetentionTargetScopeKind TargetScopeKind { get; }
    public int ExecutionPolicyVersion { get; }
    public TimeSpan Interval { get; }
    public int MaxAttempts { get; }
    public TimeSpan ExecutionTimeout { get; }
    public string ContributorKey =>
        $"{this.OwnerKey}.{this.DataClassKey}.v{this.ExecutionPolicyVersion}";

    private static string NormalizeKey(string value, string parameterName)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > RetentionExecutionContract.KeyMaxLength ||
            normalized.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '.')))
        {
            throw new ArgumentException(
                "Retention keys must be bounded lower-case ASCII identifiers.",
                parameterName);
        }

        return normalized;
    }
}

public interface IRetentionExecutionContributor
{
    RetentionScheduleDescriptor Schedule { get; }

    Task<RetentionContributionResult> ExecuteAsync(
        RetentionContributionRequest request,
        CancellationToken cancellationToken);
}

public sealed record RetentionContributionRequest(
    int ContractVersion,
    Guid ExecutionId,
    string TenantId,
    Guid? PropertyId,
    string OwnerKey,
    string DataClassKey,
    int ExecutionPolicyVersion,
    int Attempt,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineUtc);

public sealed record RetentionContributionResult(
    int ContractVersion,
    RetentionContributionStatus Status,
    int ScannedCount,
    int AffectedCount,
    int RemainingCount,
    string OutcomeCode,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset? HoldReviewDueAtUtc = null);

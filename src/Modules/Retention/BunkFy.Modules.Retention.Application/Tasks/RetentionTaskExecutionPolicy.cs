namespace BunkFy.Modules.Retention.Application.Tasks;

using BunkFy.Modules.Retention.Contracts;

internal static class RetentionTaskExecutionPolicy
{
    public static TimeSpan CompletionGrace { get; } =
        TimeSpan.FromMinutes(1);

    public static TimeSpan HandlerTimeout { get; } =
        RetentionExecutionContract.MaximumContributorExecutionTimeout +
        CompletionGrace;
}

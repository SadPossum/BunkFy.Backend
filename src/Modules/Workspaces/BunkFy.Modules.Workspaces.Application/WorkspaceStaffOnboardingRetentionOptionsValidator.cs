namespace BunkFy.Modules.Workspaces.Application;

using Microsoft.Extensions.Options;

internal sealed class WorkspaceStaffOnboardingRetentionOptionsValidator
    : IValidateOptions<WorkspaceStaffOnboardingRetentionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        WorkspaceStaffOnboardingRetentionOptions options)
    {
        List<string> failures = [];
        if (options.GracePeriodHours is < 1 or > 12)
        {
            failures.Add(
                $"{WorkspaceStaffOnboardingRetentionOptions.SectionName}:GracePeriodHours must be between 1 and 12.");
        }

        if (options.AuthorityWindowHours is < 4 or > 20)
        {
            failures.Add(
                $"{WorkspaceStaffOnboardingRetentionOptions.SectionName}:AuthorityWindowHours must be between 4 and 20.");
        }

        if (options.IntervalMinutes is < 15 or > 240)
        {
            failures.Add(
                $"{WorkspaceStaffOnboardingRetentionOptions.SectionName}:IntervalMinutes must be between 15 and 240.");
        }

        if (options.BatchSize is < 1 or > 500)
        {
            failures.Add(
                $"{WorkspaceStaffOnboardingRetentionOptions.SectionName}:BatchSize must be between 1 and 500.");
        }

        if (options.AuthorityWindowHours * 60 <=
            (options.GracePeriodHours * 60) + options.IntervalMinutes)
        {
            failures.Add(
                $"{WorkspaceStaffOnboardingRetentionOptions.SectionName}:AuthorityWindowHours must exceed the grace period plus one schedule interval.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

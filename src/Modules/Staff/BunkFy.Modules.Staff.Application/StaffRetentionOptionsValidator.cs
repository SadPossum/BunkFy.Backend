namespace BunkFy.Modules.Staff.Application;

using Microsoft.Extensions.Options;

internal sealed class StaffRetentionOptionsValidator
    : IValidateOptions<StaffRetentionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        StaffRetentionOptions options)
    {
        List<string> failures = [];
        if (options.IntervalMinutes is < 15 or > 1440)
        {
            failures.Add(
                $"{StaffRetentionOptions.SectionName}:IntervalMinutes must be between 15 and 1440.");
        }

        if (options.ScanSize is < 1 or > 1000)
        {
            failures.Add(
                $"{StaffRetentionOptions.SectionName}:ScanSize must be between 1 and 1000.");
        }

        if (options.MutationBatchSize is < 1 or > 250)
        {
            failures.Add(
                $"{StaffRetentionOptions.SectionName}:MutationBatchSize must be between 1 and 250.");
        }

        if (options.MutationBatchSize > options.ScanSize)
        {
            failures.Add(
                $"{StaffRetentionOptions.SectionName}:MutationBatchSize cannot exceed ScanSize.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

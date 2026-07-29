namespace BunkFy.Modules.Guests.Application;

using Microsoft.Extensions.Options;

internal sealed class GuestRetentionOptionsValidator
    : IValidateOptions<GuestRetentionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        GuestRetentionOptions options)
    {
        List<string> failures = [];
        if (options.IntervalMinutes is < 15 or > 1440)
        {
            failures.Add(
                $"{GuestRetentionOptions.SectionName}:IntervalMinutes must be between 15 and 1440.");
        }

        if (options.ScanSize is < 1 or > 1000)
        {
            failures.Add(
                $"{GuestRetentionOptions.SectionName}:ScanSize must be between 1 and 1000.");
        }

        if (options.MutationBatchSize is < 1 or > 250)
        {
            failures.Add(
                $"{GuestRetentionOptions.SectionName}:MutationBatchSize must be between 1 and 250.");
        }

        if (options.MutationBatchSize > options.ScanSize)
        {
            failures.Add(
                $"{GuestRetentionOptions.SectionName}:MutationBatchSize cannot exceed ScanSize.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

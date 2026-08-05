namespace BunkFy.Modules.DataRights.Persistence;

using Microsoft.Extensions.Options;

internal sealed class DataRightsTenantTerminationReplayOptionsValidator(
    bool isProduction)
    : IValidateOptions<DataRightsTenantTerminationReplayOptions>
{
    internal const int MaximumAllowedPageSize = 1000;

    public ValidateOptionsResult Validate(
        string? name,
        DataRightsTenantTerminationReplayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        if (!Enum.IsDefined(options.Provider) ||
            options.Provider ==
                DataRightsTenantTerminationReplayProvider.Unknown)
        {
            failures.Add(
                $"{DataRightsTenantTerminationReplayOptions.SectionName}:Provider must select LocalFile or External.");
        }

        if (options.MaximumPageSize is <= 0 or > MaximumAllowedPageSize)
        {
            failures.Add(
                $"{DataRightsTenantTerminationReplayOptions.SectionName}:MaximumPageSize must be between 1 and {MaximumAllowedPageSize}.");
        }

        if (options.Provider ==
            DataRightsTenantTerminationReplayProvider.LocalFile)
        {
            if (isProduction)
            {
                failures.Add(
                    $"{DataRightsTenantTerminationReplayOptions.SectionName}:Provider LocalFile is not allowed in Production.");
            }

            if (string.IsNullOrWhiteSpace(options.LocalFilePath))
            {
                failures.Add(
                    $"{DataRightsTenantTerminationReplayOptions.SectionName}:LocalFilePath is required for LocalFile.");
            }
        }
        else if (options.Provider ==
            DataRightsTenantTerminationReplayProvider.External &&
            !string.IsNullOrWhiteSpace(options.LocalFilePath))
        {
            failures.Add(
                $"{DataRightsTenantTerminationReplayOptions.SectionName}:LocalFilePath must be absent when Provider is External.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

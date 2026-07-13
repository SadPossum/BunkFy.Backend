namespace BunkFy.Host.ServiceDefaults.Security;

using Microsoft.Extensions.Options;

public sealed class BunkFyDataProtectionOptionsValidator : IValidateOptions<BunkFyDataProtectionOptions>
{
    public ValidateOptionsResult Validate(string? name, BunkFyDataProtectionOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            failures.Add("DataProtection:ApplicationName is required.");
        }

        if (options.RequirePersistentKeys && string.IsNullOrWhiteSpace(options.KeyRingPath))
        {
            failures.Add("DataProtection:KeyRingPath is required when persistent keys are required.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

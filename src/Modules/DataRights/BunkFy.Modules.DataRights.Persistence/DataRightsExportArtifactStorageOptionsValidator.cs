namespace BunkFy.Modules.DataRights.Persistence;

using Gma.Framework.FileManagement;
using Microsoft.Extensions.Options;

internal sealed class DataRightsExportArtifactStorageOptionsValidator(
    IOptions<DataRightsExportArtifactOptions> exportOptions)
    : IValidateOptions<FileManagementOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        FileManagementOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        if (!options.Enabled)
        {
            failures.Add(
                "FileManagement must be enabled for protected data-rights exports.");
        }

        if (options.AllowedContentTypes is null ||
            !options.AllowedContentTypes.Contains(
                "application/octet-stream",
                StringComparer.OrdinalIgnoreCase))
        {
            failures.Add(
                "FileManagement:AllowedContentTypes must include application/octet-stream for protected data-rights exports.");
        }

        long requiredBytes = checked(
            (long)exportOptions.Value.MaximumPlaintextBytes +
            DataRightsExportArtifactOptions.MaximumEncryptionOverheadBytes);
        if (options.MaximumObjectBytes < requiredBytes)
        {
            failures.Add(
                $"FileManagement:MaximumObjectBytes must be at least {requiredBytes} bytes for the configured protected data-rights export limit.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

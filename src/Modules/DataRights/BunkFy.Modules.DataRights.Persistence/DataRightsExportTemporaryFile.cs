namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;

internal static class DataRightsExportTemporaryFile
{
    private const UnixFileMode DirectoryMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute;
    private const UnixFileMode FileMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite;
    private static readonly Lazy<string> ProcessDirectory =
        new(CreateProcessDirectory);

    public static FileStream Create()
    {
        FileStreamOptions options = new()
        {
            Mode = System.IO.FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 64 * 1024,
            Options = FileOptions.Asynchronous |
                FileOptions.SequentialScan |
                FileOptions.DeleteOnClose
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = FileMode;
        }

        return new FileStream(
            Path.Combine(ProcessDirectory.Value, $"{Guid.NewGuid():N}.tmp"),
            options);
    }

    private static string CreateProcessDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"bunkfy-data-rights-exports-{Environment.ProcessId}-" +
            Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, DirectoryMode);
        }

        return directory;
    }
}

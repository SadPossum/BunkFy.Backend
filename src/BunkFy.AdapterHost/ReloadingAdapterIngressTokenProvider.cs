namespace BunkFy.AdapterHost;

using BunkFy.Adapters.Http;

internal sealed class ReloadingAdapterIngressTokenProvider(AdapterHostOptions options)
    : IAdapterIngressTokenProvider
{
    private const int MaximumTokenBytes = 512;

    public async ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.IngressTokenEnvironmentVariable is not null)
        {
            return Normalize(Environment.GetEnvironmentVariable(options.IngressTokenEnvironmentVariable));
        }

        string path = options.IngressTokenFilePath!;
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The adapter ingress token file is unavailable or unsafe.");
        }

        RejectReparsePath(path);

        await using FileStream stream = new(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        if (stream.Length is <= 0 or > MaximumTokenBytes)
        {
            throw new InvalidOperationException("The adapter ingress token file size is invalid.");
        }

        using StreamReader reader = new(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        string token = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return Normalize(token);
    }

    public override string ToString() => nameof(ReloadingAdapterIngressTokenProvider);

    private static string Normalize(string? token)
    {
        string selected = token?.Trim() ?? string.Empty;
        if (selected.Length is 0 or > MaximumTokenBytes ||
            selected.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new InvalidOperationException("The adapter ingress token source is invalid.");
        }

        return selected;
    }

    private static void RejectReparsePath(string path)
    {
        FileSystemInfo? current = new FileInfo(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "The adapter ingress token path cannot contain linked or reparse points.");
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null
            };
        }
    }
}

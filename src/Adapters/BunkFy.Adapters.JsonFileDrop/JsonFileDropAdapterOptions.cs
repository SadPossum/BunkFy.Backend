namespace BunkFy.Adapters.JsonFileDrop;

public sealed class JsonFileDropAdapterOptions
{
    public static readonly TimeSpan DefaultProcessedArchiveRetention = TimeSpan.FromDays(7);
    public static readonly TimeSpan DefaultFailedQuarantineRetention = TimeSpan.FromDays(30);
    public const int DefaultMaximumDeletesPerRun = 100;
    private static readonly TimeSpan MinimumRetention = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumRetention = TimeSpan.FromDays(3650);

    public JsonFileDropAdapterOptions(string rootPath)
        : this(
            rootPath,
            DefaultProcessedArchiveRetention,
            DefaultFailedQuarantineRetention,
            DefaultMaximumDeletesPerRun,
            retentionEnabled: true)
    {
    }

    public JsonFileDropAdapterOptions(
        string rootPath,
        TimeSpan processedArchiveRetention,
        TimeSpan failedQuarantineRetention,
        int maximumDeletesPerRun,
        bool retentionEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("A JSON file-drop root path is required.", nameof(rootPath));
        }

        try
        {
            this.RootPath = Path.GetFullPath(rootPath.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("The JSON file-drop root path is invalid.", nameof(rootPath));
        }

        if (processedArchiveRetention < MinimumRetention || processedArchiveRetention > MaximumRetention)
        {
            throw new ArgumentOutOfRangeException(nameof(processedArchiveRetention));
        }

        if (failedQuarantineRetention < MinimumRetention || failedQuarantineRetention > MaximumRetention)
        {
            throw new ArgumentOutOfRangeException(nameof(failedQuarantineRetention));
        }

        if (maximumDeletesPerRun is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDeletesPerRun));
        }

        this.ProcessedArchiveRetention = processedArchiveRetention;
        this.FailedQuarantineRetention = failedQuarantineRetention;
        this.MaximumDeletesPerRun = maximumDeletesPerRun;
        this.RetentionEnabled = retentionEnabled;
    }

    public string RootPath { get; }
    public TimeSpan ProcessedArchiveRetention { get; }
    public TimeSpan FailedQuarantineRetention { get; }
    public int MaximumDeletesPerRun { get; }
    public bool RetentionEnabled { get; }

    public override string ToString() => nameof(JsonFileDropAdapterOptions);
}

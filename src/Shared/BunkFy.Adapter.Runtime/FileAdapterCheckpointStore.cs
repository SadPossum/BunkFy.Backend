namespace BunkFy.Adapter.Runtime;

using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Adapter.Abstractions;

public sealed class FileAdapterCheckpointStore
    : IAdapterCheckpointStore
{
    public const int MaximumStateFileBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly string statePath;
    private readonly string lockPath;

    public FileAdapterCheckpointStore(string stateFilePath)
    {
        string selectedPath = stateFilePath?.Trim() ?? string.Empty;
        if (selectedPath.Length == 0)
        {
            throw new ArgumentException("A checkpoint state path is required.", nameof(stateFilePath));
        }

        this.statePath = Path.GetFullPath(selectedPath);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(this.statePath)))
        {
            throw new ArgumentException("The checkpoint state path must identify a file.", nameof(stateFilePath));
        }

        this.lockPath = this.statePath + ".lock";
    }

    public ValueTask<IAdapterCheckpointLease> AcquireAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("A connection id is required.", nameof(connectionId));
        }

        string directory = Path.GetDirectoryName(this.statePath)
            ?? throw new AdapterCheckpointException("The checkpoint state directory is invalid.");
        Directory.CreateDirectory(directory);
        RejectReparsePath(directory, "checkpoint state directory");
        RejectReparsePointIfPresent(this.statePath, "checkpoint state file");
        RejectReparsePointIfPresent(this.lockPath, "checkpoint lock file");

        FileStream lockStream;
        try
        {
            lockStream = new FileStream(this.lockPath, new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.WriteThrough
            });
        }
        catch (IOException exception)
        {
            throw new AdapterCheckpointException(
                "The checkpoint state is already leased by another process.",
                exception);
        }

        try
        {
            CheckpointState? state = this.ReadState(connectionId);
            return ValueTask.FromResult<IAdapterCheckpointLease>(new Lease(
                this.statePath,
                connectionId,
                state,
                lockStream));
        }
        catch
        {
            lockStream.Dispose();
            throw;
        }
    }

    private CheckpointState? ReadState(Guid connectionId)
    {
        if (!File.Exists(this.statePath))
        {
            return null;
        }

        RejectReparsePath(this.statePath, "checkpoint state file");
        byte[] content = File.ReadAllBytes(this.statePath);
        if (content.Length is 0 or > MaximumStateFileBytes)
        {
            throw new AdapterCheckpointException("The checkpoint state file size is invalid.");
        }

        try
        {
            CheckpointState? state = JsonSerializer.Deserialize<CheckpointState>(content, JsonOptions);
            if (state is null || state.SchemaVersion != CheckpointState.CurrentSchemaVersion ||
                state.ConnectionId != connectionId || state.Generation <= 0 ||
                state.UpdatedAtUtc == default || string.IsNullOrWhiteSpace(state.Checkpoint) ||
                state.Checkpoint.Trim().Length > AdapterProtocolLimits.CheckpointMaxLength)
            {
                throw new AdapterCheckpointException("The checkpoint state file is invalid.");
            }

            return state with { Checkpoint = state.Checkpoint.Trim() };
        }
        catch (JsonException exception)
        {
            throw new AdapterCheckpointException("The checkpoint state file is not valid JSON.", exception);
        }
    }

    private static void RejectReparsePointIfPresent(string path, string description)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            RejectReparsePath(path, description);
        }
    }

    private static void RejectReparsePath(string path, string description)
    {
        FileSystemInfo? current = File.Exists(path)
            ? new FileInfo(path)
            : new DirectoryInfo(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new AdapterCheckpointException(
                    $"The {description} path cannot contain linked or reparse points.");
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null
            };
        }
    }

    private sealed class Lease(
        string statePath,
        Guid connectionId,
        CheckpointState? state,
        FileStream lockStream)
        : IAdapterCheckpointLease
    {
        private bool disposed;

        public Guid ConnectionId { get; } = connectionId;
        public string? Checkpoint { get; private set; } = state?.Checkpoint;
        public long Generation { get; private set; } = state?.Generation ?? 0;

        public async Task SaveAsync(
            string checkpoint,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            string normalizedCheckpoint = checkpoint?.Trim() ?? string.Empty;
            if (normalizedCheckpoint.Length is 0 or > AdapterProtocolLimits.CheckpointMaxLength ||
                updatedAtUtc == default)
            {
                throw new AdapterCheckpointException("The checkpoint update is invalid.");
            }

            RejectReparsePointIfPresent(statePath, "checkpoint state file");
            CheckpointState next = new(
                CheckpointState.CurrentSchemaVersion,
                this.ConnectionId,
                normalizedCheckpoint,
                checked(this.Generation + 1),
                updatedAtUtc);
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(next, JsonOptions);
            if (content.Length > MaximumStateFileBytes)
            {
                throw new AdapterCheckpointException("The checkpoint state exceeds its size limit.");
            }

            string temporaryPath = $"{statePath}.tmp.{Guid.NewGuid():N}";
            try
            {
                await using (FileStream stream = new(temporaryPath, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.Asynchronous | FileOptions.WriteThrough
                }))
                {
                    await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporaryPath, statePath, overwrite: true);
                this.Checkpoint = normalizedCheckpoint;
                this.Generation = next.Generation;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new AdapterCheckpointException("The checkpoint state could not be persisted.", exception);
            }
            finally
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // The primary checkpoint result is more useful than temporary-file cleanup failure.
                }
                catch (UnauthorizedAccessException)
                {
                    // The primary checkpoint result is more useful than temporary-file cleanup failure.
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (!this.disposed)
            {
                this.disposed = true;
                lockStream.Dispose();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed record CheckpointState(
        int SchemaVersion,
        Guid ConnectionId,
        string Checkpoint,
        long Generation,
        DateTimeOffset UpdatedAtUtc)
    {
        public const int CurrentSchemaVersion = 1;
    }
}

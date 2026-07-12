namespace BunkFy.Adapters.JsonFileDrop;

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Adapter.Abstractions;

internal sealed class JsonFileDropAdapterRunner : IAdapterRunner
{
    private const int EnvelopeOverheadBytes = 64 * 1024;
    private const int MaximumEnvelopeBytes = AdapterProtocolLimits.MaximumInlinePayloadBytes + EnvelopeOverheadBytes;
    private const int CopyBufferBytes = 64 * 1024;
    private const int MaximumFailureMetadataBytes = 8 * 1024;
    private const string FailureMetadataSuffix = ".failure.json";
    private static readonly HashSet<string> QuarantineErrorCodes = new(StringComparer.Ordinal)
    {
        "json-file-drop.symbolic-link",
        "json-file-drop.envelope-too-large",
        "json-file-drop.invalid-json",
        "json-file-drop.unsupported-envelope",
        "json-file-drop.protocol-invalid"
    };
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly JsonFileDropAdapterOptions options;
    private readonly TimeProvider timeProvider;

    public JsonFileDropAdapterRunner(JsonFileDropAdapterOptions options)
        : this(options, TimeProvider.System)
    {
    }

    internal JsonFileDropAdapterRunner(JsonFileDropAdapterOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.options = options;
        this.timeProvider = timeProvider;
    }

    public AdapterDescriptor Descriptor => JsonFileDropAdapterDescriptor.Value;

    public async Task<AdapterRunCompletion> RunAsync(
        AdapterRunAssignment assignment,
        AdapterConfigurationMaterial material,
        IAdapterObservationSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(sink);
        if (assignment.AdapterType != JsonFileDropAdapterDescriptor.AdapterType ||
            assignment.ExecutionMode != AdapterExecutionMode.Polling)
        {
            throw new InvalidOperationException("The JSON file-drop runner received an incompatible assignment.");
        }

        ValidateMaterial(material);
        string connectionDirectory = Path.Combine(this.options.RootPath, assignment.ConnectionId.ToString("N"));
        string pendingDirectory = Path.Combine(connectionDirectory, "pending");
        string processedDirectory = Path.Combine(connectionDirectory, "processed");
        string failedDirectory = Path.Combine(connectionDirectory, "failed");
        Directory.CreateDirectory(this.options.RootPath);
        EnsureOwnedDirectory(connectionDirectory);
        EnsureOwnedDirectory(pendingDirectory);
        EnsureOwnedDirectory(processedDirectory);
        EnsureOwnedDirectory(failedDirectory);
        LocalArtifactMaintenanceResult maintenance = await this.MaintainLocalArtifactsAsync(
            processedDirectory,
            failedDirectory,
            cancellationToken).ConfigureAwait(false);

        string[] paths = Directory.EnumerateFiles(pendingDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Take(AdapterProtocolLimits.MaximumRecordsPerSubmission)
            .ToArray();
        if (paths.Length == 0)
        {
            return WithMaintenance(CompletedEmpty(assignment), maintenance);
        }

        List<PendingObservation> pending = new(paths.Length);
        int quarantined = 0;
        long aggregatePayloadBytes = 0;
        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PendingObservation observation;
            try
            {
                observation = await ReadObservationAsync(
                    assignment.ConnectionId, path, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonFileDropInputException exception)
            {
                await this.QuarantineAsync(
                    path, failedDirectory, exception.ErrorCode, cancellationToken).ConfigureAwait(false);
                quarantined++;
                continue;
            }

            if (aggregatePayloadBytes + observation.Record.Payload.Length >
                AdapterProtocolLimits.MaximumSubmissionPayloadBytes)
            {
                break;
            }

            aggregatePayloadBytes += observation.Record.Payload.Length;
            pending.Add(observation);
        }

        if (pending.Count == 0)
        {
            return WithMaintenance(quarantined == 0
                ? CompletedEmpty(assignment)
                : CompletedWithQuarantine(assignment, quarantined), maintenance);
        }

        string proposedCheckpoint = Path.GetFileName(pending[^1].Path);
        AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
            new AdapterObservationSubmission(
                assignment.RunId,
                assignment.LeaseId,
                pending.Select(item => item.Record).ToArray(),
                proposedCheckpoint),
            cancellationToken).ConfigureAwait(false);
        if (!MatchesSubmission(assignment, pending, acknowledgement))
        {
            return WithMaintenance(Failed(
                assignment,
                pending.Count,
                acceptedCount: 0,
                rejectedCount: 0,
                "json-file-drop.acknowledgement-mismatch",
                "The receipt acknowledgement did not match the file-drop submission."), maintenance);
        }

        Dictionary<Guid, AdapterObservationResult> results = acknowledgement.Results
            .ToDictionary(result => result.OperationId);
        int accepted = 0;
        int rejected = 0;
        foreach (PendingObservation item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AdapterObservationResult result = results[item.Record.OperationId];
            if (result.Disposition == AdapterObservationDisposition.Rejected)
            {
                rejected++;
                continue;
            }

            await ArchiveAsync(
                item,
                processedDirectory,
                this.timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            accepted++;
        }

        if (rejected > 0)
        {
            return WithMaintenance(new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.PartiallySucceeded,
                pending.Count,
                accepted,
                rejected,
                acknowledgement.AcceptedCheckpoint ?? assignment.Checkpoint,
                "json-file-drop.observation-rejected",
                quarantined == 0
                    ? "One or more file-drop observations were rejected before durable receipt."
                    : $"{QuarantineMessage(quarantined)} One or more observations were rejected."), maintenance);
        }

        if (!acknowledgement.CheckpointAccepted)
        {
            return WithMaintenance(Failed(
                assignment,
                pending.Count,
                accepted,
                rejectedCount: 0,
                "json-file-drop.checkpoint-not-accepted",
                "The observations were durable but their checkpoint was not accepted."), maintenance);
        }

        return WithMaintenance(new AdapterRunCompletion(
            assignment.RunId,
            assignment.LeaseId,
            quarantined == 0 ? AdapterRunOutcome.Succeeded : AdapterRunOutcome.PartiallySucceeded,
            pending.Count,
            accepted,
            rejectedCount: 0,
            acknowledgement.AcceptedCheckpoint,
            quarantined == 0 ? null : "json-file-drop.input-quarantined",
            quarantined == 0 ? null : QuarantineMessage(quarantined)), maintenance);
    }

    private static void ValidateMaterial(AdapterConfigurationMaterial material)
    {
        if (material.SchemaVersion != JsonFileDropAdapterDescriptor.Value.ConfigurationSchemaVersion ||
            !string.Equals(material.ConfigurationContentType, "application/json", StringComparison.Ordinal) ||
            material.HasSecret)
        {
            throw new InvalidOperationException("The JSON file-drop adapter material is incompatible.");
        }

        try
        {
            _ = JsonSerializer.Deserialize<EmptyConfiguration>(material.Configuration.Span, SerializerOptions) ??
                throw new InvalidOperationException("The JSON file-drop configuration must be an object.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The JSON file-drop configuration is invalid.");
        }
    }

    private static async Task<PendingObservation> ReadObservationAsync(
        Guid connectionId,
        string path,
        CancellationToken cancellationToken)
    {
        EnsureOwnedDirectory(Path.GetDirectoryName(path)!);
        byte[] envelopeBytes = await ReadBoundedAsync(path, cancellationToken).ConfigureAwait(false);
        JsonFileEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<JsonFileEnvelope>(envelopeBytes, SerializerOptions) ??
                throw new JsonFileDropInputException(
                    "json-file-drop.invalid-json",
                    "The JSON file-drop envelope is invalid.");
        }
        catch (JsonException)
        {
            throw new JsonFileDropInputException(
                "json-file-drop.invalid-json",
                "The JSON file-drop envelope is invalid.");
        }

        if (envelope.SchemaVersion != 1 || envelope.ObservedAtUtc == default ||
            envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new JsonFileDropInputException(
                "json-file-drop.unsupported-envelope",
                "The JSON file-drop envelope is incomplete or unsupported.");
        }

        byte[] payload = Encoding.UTF8.GetBytes(envelope.Payload.GetRawText());
        string payloadHash = AdapterPayloadHash.ComputeSha256(payload);
        AdapterObservedRecord record;
        try
        {
            record = new AdapterObservedRecord(
                CreateOperationId(
                    connectionId,
                    Path.GetFileName(path),
                    envelope.RecordType,
                    envelope.ExternalRecordId,
                    envelope.SourceRevision,
                    payloadHash),
                envelope.RecordType,
                envelope.ExternalRecordId,
                NormalizeOptional(envelope.SourceRevision),
                envelope.SourceUpdatedAtUtc,
                envelope.ObservedAtUtc,
                "application/json",
                payload,
                payloadHash);
        }
        catch (ArgumentException)
        {
            throw new JsonFileDropInputException(
                "json-file-drop.protocol-invalid",
                "The JSON file-drop envelope violates the observation protocol.");
        }

        return new PendingObservation(path, envelopeBytes, record);
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new JsonFileDropInputException(
                    "json-file-drop.symbolic-link",
                    "The JSON file-drop input cannot be a symbolic link.");
            }

            await using FileStream source = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (source.Length is <= 0 or > MaximumEnvelopeBytes)
            {
                throw new JsonFileDropInputException(
                    "json-file-drop.envelope-too-large",
                    "The JSON file-drop envelope exceeds the allowed size.");
            }

            using MemoryStream destination = new((int)source.Length);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferBytes);
            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(
                        buffer.AsMemory(0, CopyBufferBytes), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    if (destination.Length + read > MaximumEnvelopeBytes)
                    {
                        throw new JsonFileDropInputException(
                            "json-file-drop.envelope-too-large",
                            "The JSON file-drop envelope exceeds the allowed size.");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                return destination.ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The JSON file-drop input could not be read.");
        }
    }

    private async Task QuarantineAsync(
        string sourcePath,
        string failedDirectory,
        string errorCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOwnedDirectory(Path.GetDirectoryName(sourcePath)!);
        EnsureOwnedDirectory(failedDirectory);
        string originalFileName = Path.GetFileName(sourcePath);
        string destinationPath = Path.Combine(failedDirectory, originalFileName);
        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            destinationPath = Path.Combine(
                failedDirectory,
                $"{Path.GetFileNameWithoutExtension(originalFileName)}.{Guid.NewGuid():N}{Path.GetExtension(originalFileName)}");
        }

        string metadataPath = destinationPath + FailureMetadataSuffix;
        string temporaryMetadataPath = metadataPath + $".tmp.{Guid.NewGuid():N}";
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(
            new QuarantineMetadata(
                SchemaVersion: 1,
                originalFileName,
                errorCode,
                this.timeProvider.GetUtcNow()),
            SerializerOptions);
        if (metadata.Length > MaximumFailureMetadataBytes)
        {
            throw new InvalidOperationException("The JSON file-drop quarantine metadata is invalid.");
        }

        try
        {
            await WriteDurableAsync(temporaryMetadataPath, metadata, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryMetadataPath, metadataPath, overwrite: false);
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: false);
            }
            catch
            {
                File.Delete(metadataPath);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The JSON file-drop input could not be quarantined.");
        }
        finally
        {
            if (File.Exists(temporaryMetadataPath))
            {
                File.Delete(temporaryMetadataPath);
            }
        }
    }

    private static async Task WriteDurableAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferBytes,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private async Task<LocalArtifactMaintenanceResult> MaintainLocalArtifactsAsync(
        string processedDirectory,
        string failedDirectory,
        CancellationToken cancellationToken)
    {
        if (!this.options.RetentionEnabled)
        {
            return new(FailureCount: 0);
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        int failures = MaintainProcessedArtifacts(
            processedDirectory,
            now - this.options.ProcessedArchiveRetention,
            this.options.MaximumDeletesPerRun,
            cancellationToken);
        failures += await MaintainFailedArtifactsAsync(
            failedDirectory,
            now - this.options.FailedQuarantineRetention,
            this.options.MaximumDeletesPerRun,
            cancellationToken).ConfigureAwait(false);
        return new(failures);
    }

    private static int MaintainProcessedArtifacts(
        string processedDirectory,
        DateTimeOffset cutoff,
        int maximumDeletes,
        CancellationToken cancellationToken)
    {
        int deleted = 0;
        int failures = 0;
        foreach (string path in Directory.EnumerateFiles(
                     processedDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deleted >= maximumDeletes)
            {
                break;
            }

            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    failures++;
                    continue;
                }

                if (File.GetLastWriteTimeUtc(path) > cutoff.UtcDateTime)
                {
                    continue;
                }

                File.Delete(path);
                deleted++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures++;
            }
        }

        return failures;
    }

    private static async Task<int> MaintainFailedArtifactsAsync(
        string failedDirectory,
        DateTimeOffset cutoff,
        int maximumDeletes,
        CancellationToken cancellationToken)
    {
        int deleted = 0;
        int failures = 0;
        foreach (string metadataPath in Directory.EnumerateFiles(
                     failedDirectory,
                     $"*{FailureMetadataSuffix}",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deleted >= maximumDeletes)
            {
                break;
            }

            try
            {
                if ((File.GetAttributes(metadataPath) & FileAttributes.ReparsePoint) != 0)
                {
                    failures++;
                    continue;
                }

                QuarantineMetadata? metadata = await ReadQuarantineMetadataAsync(
                    metadataPath,
                    cancellationToken).ConfigureAwait(false);
                if (!IsValidQuarantineMetadata(metadata) || metadata!.QuarantinedAtUtc > cutoff)
                {
                    if (metadata is null || !IsValidQuarantineMetadata(metadata))
                    {
                        failures++;
                    }

                    continue;
                }

                string rawPath = metadataPath[..^FailureMetadataSuffix.Length];
                if (Directory.Exists(rawPath))
                {
                    failures++;
                    continue;
                }

                if (File.Exists(rawPath))
                {
                    File.Delete(rawPath);
                }

                File.Delete(metadataPath);
                deleted++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures++;
            }
        }

        return failures;
    }

    private static async Task<QuarantineMetadata?> ReadQuarantineMetadataAsync(
        string path,
        CancellationToken cancellationToken)
    {
        byte[]? bytes = null;
        try
        {
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > MaximumFailureMetadataBytes)
            {
                return null;
            }

            bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<QuarantineMetadata>(bytes, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            if (bytes is not null)
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    private static bool IsValidQuarantineMetadata(QuarantineMetadata? metadata)
    {
        if (metadata is null || metadata.SchemaVersion != 1 || metadata.QuarantinedAtUtc == default ||
            string.IsNullOrWhiteSpace(metadata.OriginalFileName) ||
            !string.Equals(
                Path.GetFileName(metadata.OriginalFileName),
                metadata.OriginalFileName,
                StringComparison.Ordinal) ||
            metadata.OriginalFileName.IndexOfAny(['/', '\\']) >= 0 ||
            !QuarantineErrorCodes.Contains(metadata.ErrorCode))
        {
            return false;
        }

        return !metadata.OriginalFileName.Any(char.IsControl) &&
               !metadata.ErrorCode.Any(char.IsControl);
    }

    private static void EnsureOwnedDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("A JSON file-drop working directory cannot be a symbolic link.");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("A JSON file-drop working directory is unavailable.");
        }
    }

    private static async Task ArchiveAsync(
        PendingObservation item,
        string processedDirectory,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken)
    {
        EnsureOwnedDirectory(Path.GetDirectoryName(item.Path)!);
        EnsureOwnedDirectory(processedDirectory);
        byte[] current = await ReadBoundedAsync(item.Path, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!ContentEquals(item.EnvelopeBytes, current))
            {
                throw new InvalidOperationException("The JSON file-drop input changed while it was being processed.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(current);
        }

        string destination = Path.Combine(processedDirectory, Path.GetFileName(item.Path));
        if (File.Exists(destination))
        {
            byte[] archived = await ReadBoundedAsync(destination, cancellationToken).ConfigureAwait(false);
            try
            {
                if (!ContentEquals(item.EnvelopeBytes, archived))
                {
                    throw new InvalidOperationException("The JSON file-drop archive contains a conflicting filename.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(archived);
            }

            File.Delete(item.Path);
            return;
        }

        try
        {
            File.SetLastWriteTimeUtc(item.Path, archivedAtUtc.UtcDateTime);
            File.Move(item.Path, destination, overwrite: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The JSON file-drop input could not be archived.");
        }
    }

    private static bool MatchesSubmission(
        AdapterRunAssignment assignment,
        IReadOnlyCollection<PendingObservation> pending,
        AdapterObservationAcknowledgement acknowledgement)
    {
        if (acknowledgement.RunId != assignment.RunId || acknowledgement.LeaseId != assignment.LeaseId ||
            acknowledgement.Results.Count != pending.Count)
        {
            return false;
        }

        HashSet<Guid> submitted = pending.Select(item => item.Record.OperationId).ToHashSet();
        return acknowledgement.Results.All(result => submitted.Contains(result.OperationId));
    }

    private static Guid CreateOperationId(
        Guid connectionId,
        string fileName,
        string recordType,
        string externalRecordId,
        string? sourceRevision,
        string contentHash)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, connectionId.ToString("N"));
        Append(hash, fileName);
        Append(hash, recordType);
        Append(hash, externalRecordId);
        Append(hash, NormalizeOptional(sourceRevision) ?? string.Empty);
        Append(hash, contentHash);
        byte[] digest = hash.GetHashAndReset();
        return new Guid(digest.AsSpan(0, 16));
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static bool ContentEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        byte[] leftHash = SHA256.HashData(left);
        byte[] rightHash = SHA256.HashData(right);
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    private static AdapterRunCompletion CompletedEmpty(AdapterRunAssignment assignment) => new(
        assignment.RunId,
        assignment.LeaseId,
        AdapterRunOutcome.Succeeded,
        observedCount: 0,
        acceptedCount: 0,
        rejectedCount: 0,
        assignment.Checkpoint,
        errorCode: null,
        errorMessage: null);

    private static AdapterRunCompletion CompletedWithQuarantine(
        AdapterRunAssignment assignment,
        int quarantined) => new(
            assignment.RunId,
            assignment.LeaseId,
            AdapterRunOutcome.PartiallySucceeded,
            observedCount: 0,
            acceptedCount: 0,
            rejectedCount: 0,
            assignment.Checkpoint,
            "json-file-drop.input-quarantined",
            QuarantineMessage(quarantined));

    private static AdapterRunCompletion Failed(
        AdapterRunAssignment assignment,
        int observedCount,
        int acceptedCount,
        int rejectedCount,
        string errorCode,
        string errorMessage) => new(
            assignment.RunId,
            assignment.LeaseId,
            AdapterRunOutcome.Failed,
            observedCount,
            acceptedCount,
            rejectedCount,
            assignment.Checkpoint,
        errorCode,
        errorMessage);

    private static AdapterRunCompletion WithMaintenance(
        AdapterRunCompletion completion,
        LocalArtifactMaintenanceResult maintenance)
    {
        if (maintenance.FailureCount == 0)
        {
            return completion;
        }

        string maintenanceMessage = maintenance.FailureCount == 1
            ? "1 local artifact retention item could not be processed."
            : $"{maintenance.FailureCount} local artifact retention items could not be processed.";
        string? errorCode = completion.ErrorCode ?? "json-file-drop.retention-maintenance-failed";
        string errorMessage = string.IsNullOrWhiteSpace(completion.ErrorMessage)
            ? maintenanceMessage
            : $"{completion.ErrorMessage} {maintenanceMessage}";
        AdapterRunOutcome outcome = completion.Outcome == AdapterRunOutcome.Succeeded
            ? AdapterRunOutcome.PartiallySucceeded
            : completion.Outcome;
        return new AdapterRunCompletion(
            completion.RunId,
            completion.LeaseId,
            outcome,
            completion.ObservedCount,
            completion.AcceptedCount,
            completion.RejectedCount,
            completion.AcceptedCheckpoint,
            errorCode,
            errorMessage);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string QuarantineMessage(int quarantined) => quarantined == 1
        ? "1 file-drop input was quarantined."
        : $"{quarantined} file-drop inputs were quarantined.";

    private sealed class EmptyConfiguration;

    private sealed class JsonFileDropInputException(string errorCode, string message)
        : InvalidOperationException(message)
    {
        public string ErrorCode { get; } = errorCode;
    }

    private sealed record QuarantineMetadata(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("originalFileName")] string OriginalFileName,
        [property: JsonPropertyName("errorCode")] string ErrorCode,
        [property: JsonPropertyName("quarantinedAtUtc")] DateTimeOffset QuarantinedAtUtc);

    private readonly record struct LocalArtifactMaintenanceResult(int FailureCount);

    private sealed class JsonFileEnvelope
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("recordType")]
        public string RecordType { get; init; } = string.Empty;

        [JsonPropertyName("externalRecordId")]
        public string ExternalRecordId { get; init; } = string.Empty;

        [JsonPropertyName("sourceRevision")]
        public string? SourceRevision { get; init; }

        [JsonPropertyName("sourceUpdatedAtUtc")]
        public DateTimeOffset? SourceUpdatedAtUtc { get; init; }

        [JsonPropertyName("observedAtUtc")]
        public DateTimeOffset ObservedAtUtc { get; init; }

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; init; }
    }

    private sealed record PendingObservation(
        string Path,
        byte[] EnvelopeBytes,
        AdapterObservedRecord Record);
}

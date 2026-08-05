namespace BunkFy.Modules.DataRights.Persistence;

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Modules.DataRights.Application.Ports;
using Gma.Framework.Naming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed partial class LocalFileTenantTerminationReplayStore
    : ITenantTerminationReplayStore
{
    internal const string ProviderName = "local-file";
    internal const string InvalidCode =
        "data-rights.tenant-termination-replay.invalid";
    internal const string ConflictCode =
        "data-rights.tenant-termination-replay.conflict";
    internal const string IntegrityCode =
        "data-rights.tenant-termination-replay.integrity-failed";
    internal const string UnavailableCode =
        "data-rights.tenant-termination-replay.unavailable";
    private const int StorageContractVersion = 1;
    private const int NonceSizeBytes = 12;
    private const int AuthenticationTagSizeBytes = 16;
    private const int MaximumPlaintextBytes = 128 * 1024;
    private const int MaximumRecordBytes = 256 * 1024;
    private const int MaximumCheckpointBytes = 16 * 1024;
    private const string AesAlgorithm = "A256GCM";
    private const string RecordSuffix = ".replay.json";
    private const string CheckpointFileName = "checkpoint.json";
    private const string LockFileName = ".append.lock";
    private const string EncryptionKeyDomain =
        "bunkfy.data-rights.tenant-termination-replay.encryption-key.v1";
    private const string NonceKeyDomain =
        "bunkfy.data-rights.tenant-termination-replay.nonce-key.v1";
    private const string CheckpointKeyDomain =
        "bunkfy.data-rights.tenant-termination-replay.checkpoint-key.v1";
    private const string RecordBindingDomain =
        "bunkfy.data-rights.tenant-termination-replay.record-binding.v1";
    private const string RecordHashDomain =
        "bunkfy.data-rights.tenant-termination-replay.record-hash.v1";
    private const string CheckpointDomain =
        "bunkfy.data-rights.tenant-termination-replay.checkpoint.v1";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim>
        ProcessGates = new();
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly DataRightsTenantTerminationReplayOptions options;
    private readonly DataRightsReplayEnvelopeOptions keyOptions;
    private readonly string rootPath;
    private readonly TimeProvider timeProvider;

    public LocalFileTenantTerminationReplayStore(
        IOptions<DataRightsTenantTerminationReplayOptions> options,
        IOptions<DataRightsReplayEnvelopeOptions> keyOptions,
        IHostEnvironment environment,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(keyOptions);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.options = options.Value;
        this.keyOptions = keyOptions.Value;
        string configuredPath =
            this.options.LocalFilePath?.Trim() ?? string.Empty;
        this.rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));
        this.timeProvider = timeProvider;
    }

    public async Task<TenantTerminationReplayStoreReadiness>
        CheckReadinessAsync(CancellationToken cancellationToken)
    {
        try
        {
            EnsureSecureDirectory(this.rootPath);
            if (!this.TryGetMasterKey(
                    this.keyOptions.ActiveKeyVersion,
                    out byte[] key))
            {
                return new(
                    ProviderName,
                    IsReady: false,
                    IsProductionGrade: false,
                    UnavailableCode);
            }

            CryptographicOperations.ZeroMemory(key);
            string probePath = Path.Combine(
                this.rootPath,
                $".readiness-{Guid.NewGuid():N}.tmp");
            try
            {
                await WriteNewFileAsync(
                    probePath,
                    [0x42],
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                TryDelete(probePath);
            }

            return new(
                ProviderName,
                IsReady: true,
                IsProductionGrade: false,
                FailureCode: null);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            TenantTerminationReplayStoreException)
        {
            return new(
                ProviderName,
                IsReady: false,
                IsProductionGrade: false,
                UnavailableCode);
        }
    }

    public async Task<TenantTerminationReplayAppendReceipt> AppendAsync(
        TenantTerminationReplayJournalEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry is null || !entry.HasValidProof())
        {
            throw Invalid("The tenant-termination replay entry is invalid.");
        }

        string tenantId = NormalizeTenant(entry.TenantId);
        Guid processId = entry.ProcessId;
        await using ProcessLease lease = await this.AcquireAsync(
            processId,
            cancellationToken).ConfigureAwait(false);
        TrustedJournalState state = await this.LoadTrustedStateAsync(
            lease.DirectoryPath,
            tenantId,
            processId,
            cancellationToken).ConfigureAwait(false);

        LoadedReplayRecord? existing = state.Records.SingleOrDefault(record =>
            string.Equals(
                record.Entry.LogicalEntryId,
                entry.LogicalEntryId,
                StringComparison.Ordinal));
        if (existing is not null)
        {
            if (!Equals(existing.Entry, entry))
            {
                throw Conflict(
                    "The replay identity is already bound to another entry.");
            }

            return CreateReceipt(existing.Storage, state.Checkpoint);
        }

        if (entry.Kind == TenantTerminationReplayEntryKind.Result)
        {
            string dispatchId = TenantTerminationReplayProof
                .ComputeLogicalEntryId(
                    entry.Dispatch!.Coordinate,
                    TenantTerminationReplayEntryKind.Dispatch);
            LoadedReplayRecord? dispatch = state.Records.SingleOrDefault(
                record => string.Equals(
                    record.Entry.LogicalEntryId,
                    dispatchId,
                    StringComparison.Ordinal));
            if (dispatch?.Entry.Dispatch is null ||
                dispatch.Entry.Kind !=
                    TenantTerminationReplayEntryKind.Dispatch ||
                !Equals(dispatch.Entry.Dispatch, entry.Dispatch))
            {
                throw Conflict(
                    "A result requires its exact durable replay dispatch.");
            }
        }

        if (state.Checkpoint.Sequence == long.MaxValue)
        {
            throw Conflict("The replay sequence is exhausted.");
        }

        long sequence = state.Checkpoint.Sequence + 1;
        DateTimeOffset flushedAtUtc = this.timeProvider.GetUtcNow();
        StoredReplayRecord stored = this.CreateRecord(
            processId,
            sequence,
            state.Checkpoint.RecordSha256,
            entry,
            flushedAtUtc);
        await WriteNewFileAsync(
            RecordPath(lease.DirectoryPath, sequence),
            JsonSerializer.SerializeToUtf8Bytes(stored, SerializerOptions),
            cancellationToken).ConfigureAwait(false);

        StoredReplayCheckpoint checkpoint = this.CreateCheckpoint(
            processId,
            sequence,
            stored.RecordSha256,
            flushedAtUtc);
        await WriteAtomicReplaceFileAsync(
            Path.Combine(lease.DirectoryPath, CheckpointFileName),
            JsonSerializer.SerializeToUtf8Bytes(
                checkpoint,
                SerializerOptions),
            cancellationToken).ConfigureAwait(false);
        return CreateReceipt(stored, checkpoint);
    }

    public async Task<TenantTerminationReplayAttempt?> ReadAttemptAsync(
        TenantTerminationReplayAttemptCoordinate coordinate,
        CancellationToken cancellationToken)
    {
        if (coordinate is null || !coordinate.HasValidShape())
        {
            throw Invalid("The replay attempt coordinate is invalid.");
        }

        string tenantId = NormalizeTenant(coordinate.TenantId);
        await using ProcessLease lease = await this.AcquireAsync(
            coordinate.ProcessId,
            cancellationToken).ConfigureAwait(false);
        TrustedJournalState state = await this.LoadTrustedStateAsync(
            lease.DirectoryPath,
            tenantId,
            coordinate.ProcessId,
            cancellationToken).ConfigureAwait(false);
        string dispatchId = TenantTerminationReplayProof.ComputeLogicalEntryId(
            coordinate,
            TenantTerminationReplayEntryKind.Dispatch);
        string resultId = TenantTerminationReplayProof.ComputeLogicalEntryId(
            coordinate,
            TenantTerminationReplayEntryKind.Result);
        TenantTerminationReplayDispatch? dispatch = state.Records
            .Select(record => record.Entry)
            .SingleOrDefault(entry => string.Equals(
                entry.LogicalEntryId,
                dispatchId,
                StringComparison.Ordinal))
            ?.Dispatch;
        if (dispatch is null)
        {
            return null;
        }

        TenantTerminationReplayResult? result = state.Records
            .Select(record => record.Entry)
            .SingleOrDefault(entry => string.Equals(
                entry.LogicalEntryId,
                resultId,
                StringComparison.Ordinal))
            ?.Result;
        return new(dispatch, result);
    }

    public async Task<TenantTerminationReplayCheckpoint>
        ReadTrustedCheckpointAsync(
            string tenantId,
            Guid processId,
            CancellationToken cancellationToken)
    {
        string normalizedTenant = NormalizeTenant(tenantId);
        RequireProcessId(processId);
        await using ProcessLease lease = await this.AcquireAsync(
            processId,
            cancellationToken).ConfigureAwait(false);
        TrustedJournalState state = await this.LoadTrustedStateAsync(
            lease.DirectoryPath,
            normalizedTenant,
            processId,
            cancellationToken).ConfigureAwait(false);
        return ToPublicCheckpoint(state.Checkpoint);
    }

    public async Task<TenantTerminationReplayPage> ReadAfterAsync(
        string tenantId,
        Guid processId,
        TenantTerminationReplayCursor cursor,
        int pageSize,
        CancellationToken cancellationToken)
    {
        string normalizedTenant = NormalizeTenant(tenantId);
        RequireProcessId(processId);
        if (cursor is null ||
            cursor.Sequence < 0 ||
            !TenantTerminationReplayProof.IsSha256(cursor.RecordSha256) ||
            pageSize <= 0 ||
            pageSize > this.options.MaximumPageSize)
        {
            throw Invalid("The replay cursor or page size is invalid.");
        }

        await using ProcessLease lease = await this.AcquireAsync(
            processId,
            cancellationToken).ConfigureAwait(false);
        TrustedJournalState state = await this.LoadTrustedStateAsync(
            lease.DirectoryPath,
            normalizedTenant,
            processId,
            cancellationToken).ConfigureAwait(false);
        if (!CursorMatches(state.Records, cursor))
        {
            throw Conflict("The replay cursor is not on the trusted chain.");
        }

        LoadedReplayRecord[] selected = [..
            state.Records
                .Skip(checked((int)cursor.Sequence))
                .Take(pageSize)];
        TenantTerminationReplayCursor next = selected.Length == 0
            ? cursor
            : new(
                selected[^1].Storage.Sequence,
                selected[^1].Storage.RecordSha256);
        bool hasMore = next.Sequence < state.Records.Count;
        return new(
            TenantTerminationReplayPage.CurrentContractVersion,
            selected.Select(record => record.Entry).ToArray(),
            next,
            hasMore);
    }

    private async Task<TrustedJournalState> LoadTrustedStateAsync(
        string directoryPath,
        string tenantId,
        Guid processId,
        CancellationToken cancellationToken)
    {
        string checkpointPath = Path.Combine(
            directoryPath,
            CheckpointFileName);
        string[] recordPaths = Directory
            .EnumerateFiles(directoryPath, $"*{RecordSuffix}")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        StoredReplayCheckpoint checkpoint;
        if (!File.Exists(checkpointPath))
        {
            if (recordPaths.Length > 0)
            {
                throw Integrity(
                    "Replay records exist without a trusted checkpoint.");
            }

            DateTimeOffset nowUtc = this.timeProvider.GetUtcNow();
            checkpoint = this.CreateCheckpoint(
                processId,
                sequence: 0,
                TenantTerminationReplayProof.GenesisSha256,
                nowUtc);
            await WriteAtomicReplaceFileAsync(
                checkpointPath,
                JsonSerializer.SerializeToUtf8Bytes(
                    checkpoint,
                    SerializerOptions),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            checkpoint = await ReadJsonAsync<StoredReplayCheckpoint>(
                checkpointPath,
                MaximumCheckpointBytes,
                cancellationToken).ConfigureAwait(false);
            if (!this.HasValidCheckpoint(checkpoint, processId))
            {
                throw Integrity("The replay checkpoint proof is invalid.");
            }
        }

        List<LoadedReplayRecord> records = [];
        Dictionary<string, TenantTerminationReplayJournalEntry> byLogicalId =
            new(StringComparer.Ordinal);
        string previousSha256 = TenantTerminationReplayProof.GenesisSha256;
        string? checkpointHead = checkpoint.Sequence == 0
            ? previousSha256
            : null;
        for (int index = 0; index < recordPaths.Length; index++)
        {
            long expectedSequence = index + 1L;
            if (!TryParseRecordSequence(
                    recordPaths[index],
                    out long fileSequence) ||
                fileSequence != expectedSequence)
            {
                throw Integrity(
                    "The replay record sequence contains a gap or invalid name.");
            }

            StoredReplayRecord stored = await ReadJsonAsync<StoredReplayRecord>(
                recordPaths[index],
                MaximumRecordBytes,
                cancellationToken).ConfigureAwait(false);
            TenantTerminationReplayJournalEntry entry = this.UnprotectRecord(
                stored,
                tenantId,
                processId,
                expectedSequence,
                previousSha256);
            if (!byLogicalId.TryAdd(entry.LogicalEntryId, entry))
            {
                throw Integrity(
                    "The replay chain contains a duplicate logical entry.");
            }

            if (entry.Kind == TenantTerminationReplayEntryKind.Result)
            {
                string dispatchId = TenantTerminationReplayProof
                    .ComputeLogicalEntryId(
                        entry.Dispatch!.Coordinate,
                        TenantTerminationReplayEntryKind.Dispatch);
                if (!byLogicalId.TryGetValue(
                        dispatchId,
                        out TenantTerminationReplayJournalEntry? dispatch) ||
                    dispatch.Kind !=
                        TenantTerminationReplayEntryKind.Dispatch ||
                    !Equals(dispatch.Dispatch, entry.Dispatch))
                {
                    throw Integrity(
                        "A replay result is not preceded by its exact dispatch.");
                }
            }

            records.Add(new(stored, entry));
            previousSha256 = stored.RecordSha256;
            if (expectedSequence == checkpoint.Sequence)
            {
                checkpointHead = previousSha256;
            }
        }

        if (checkpoint.Sequence > records.Count ||
            checkpointHead is null ||
            !TenantTerminationReplayProof.FixedTimeSha256Equals(
                checkpoint.RecordSha256,
                checkpointHead))
        {
            throw Integrity(
                "The replay checkpoint is ahead of or outside the record chain.");
        }

        if (checkpoint.Sequence < records.Count)
        {
            DateTimeOffset recoveredAtUtc = this.timeProvider.GetUtcNow();
            checkpoint = this.CreateCheckpoint(
                processId,
                records.Count,
                previousSha256,
                recoveredAtUtc);
            await WriteAtomicReplaceFileAsync(
                checkpointPath,
                JsonSerializer.SerializeToUtf8Bytes(
                    checkpoint,
                    SerializerOptions),
                cancellationToken).ConfigureAwait(false);
        }

        return new(checkpoint, records);
    }

    private StoredReplayRecord CreateRecord(
        Guid processId,
        long sequence,
        string previousRecordSha256,
        TenantTerminationReplayJournalEntry entry,
        DateTimeOffset flushedAtUtc)
    {
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
            entry,
            SerializerOptions);
        if (plaintext.Length is <= 0 or > MaximumPlaintextBytes)
        {
            throw Invalid("The replay entry exceeds the protected size limit.");
        }

        int keyVersion = this.keyOptions.ActiveKeyVersion;
        if (!this.TryGetMasterKey(keyVersion, out byte[] masterKey))
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw Unavailable("The active replay encryption key is unavailable.");
        }

        string plaintextSha256 = Hash(plaintext);
        byte[] binding = CreateRecordBinding(
            processId,
            sequence,
            previousRecordSha256,
            entry.LogicalEntryId,
            entry.Kind,
            keyVersion,
            plaintextSha256,
            flushedAtUtc);
        byte[] encryptionKey = Derive(masterKey, EncryptionKeyDomain);
        byte[] nonceKey = Derive(masterKey, NonceKeyDomain);
        byte[] nonceDigest = HMACSHA256.HashData(nonceKey, binding);
        byte[] nonce = nonceDigest[..NonceSizeBytes];
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[AuthenticationTagSizeBytes];
        try
        {
            using AesGcm aes = new(encryptionKey, AuthenticationTagSizeBytes);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, binding);
            StoredReplayRecord unsigned = new(
                StorageContractVersion,
                processId,
                sequence,
                previousRecordSha256,
                entry.LogicalEntryId,
                entry.Kind,
                keyVersion,
                AesAlgorithm,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(ciphertext),
                Convert.ToBase64String(tag),
                plaintextSha256,
                RecordSha256: string.Empty,
                flushedAtUtc);
            return unsigned with
            {
                RecordSha256 = ComputeRecordSha256(unsigned)
            };
        }
        catch (CryptographicException exception)
        {
            throw new TenantTerminationReplayStoreException(
                InvalidCode,
                "The replay entry could not be protected.",
                exception);
        }
        finally
        {
            Zero(
                plaintext,
                masterKey,
                binding,
                encryptionKey,
                nonceKey,
                nonceDigest,
                nonce,
                ciphertext,
                tag);
        }
    }

    private TenantTerminationReplayJournalEntry UnprotectRecord(
        StoredReplayRecord stored,
        string tenantId,
        Guid processId,
        long expectedSequence,
        string expectedPreviousSha256)
    {
        if (!HasValidStoredShape(
                stored,
                processId,
                expectedSequence,
                expectedPreviousSha256) ||
            !TenantTerminationReplayProof.FixedTimeSha256Equals(
                stored.RecordSha256,
                ComputeRecordSha256(stored)) ||
            !this.TryGetMasterKey(
                stored.EncryptionKeyVersion,
                out byte[] masterKey))
        {
            throw Integrity("The replay record proof is invalid.");
        }

        byte[] binding = CreateRecordBinding(
            stored.ProcessId,
            stored.Sequence,
            stored.PreviousRecordSha256,
            stored.LogicalEntryId,
            stored.Kind,
            stored.EncryptionKeyVersion,
            stored.PlaintextSha256,
            stored.FlushedAtUtc);
        byte[] encryptionKey = Derive(masterKey, EncryptionKeyDomain);
        byte[] nonceKey = Derive(masterKey, NonceKeyDomain);
        byte[] nonceDigest = HMACSHA256.HashData(nonceKey, binding);
        byte[] expectedNonce = nonceDigest[..NonceSizeBytes];
        byte[] nonce = Convert.FromBase64String(stored.NonceBase64);
        byte[] ciphertext = Convert.FromBase64String(stored.CiphertextBase64);
        byte[] tag = Convert.FromBase64String(stored.AuthenticationTagBase64);
        byte[] plaintext = new byte[ciphertext.Length];
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    nonce,
                    expectedNonce))
            {
                throw Integrity("The replay record nonce is invalid.");
            }

            using AesGcm aes = new(encryptionKey, AuthenticationTagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, binding);
            if (!TenantTerminationReplayProof.FixedTimeSha256Equals(
                    stored.PlaintextSha256,
                    Hash(plaintext)))
            {
                throw Integrity("The replay plaintext digest is invalid.");
            }

            TenantTerminationReplayJournalEntry? entry =
                JsonSerializer.Deserialize<TenantTerminationReplayJournalEntry>(
                    plaintext,
                    SerializerOptions);
            if (entry is null ||
                !entry.HasValidProof() ||
                !string.Equals(
                    entry.LogicalEntryId,
                    stored.LogicalEntryId,
                    StringComparison.Ordinal) ||
                entry.Kind != stored.Kind ||
                entry.ProcessId != processId ||
                !string.Equals(
                    NormalizeTenant(entry.TenantId),
                    tenantId,
                    StringComparison.Ordinal))
            {
                throw Integrity("The replay plaintext proof is invalid.");
            }

            return entry;
        }
        catch (CryptographicException exception)
        {
            throw new TenantTerminationReplayStoreException(
                IntegrityCode,
                "The replay record authentication failed.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new TenantTerminationReplayStoreException(
                IntegrityCode,
                "The replay record payload is malformed.",
                exception);
        }
        finally
        {
            Zero(
                masterKey,
                binding,
                encryptionKey,
                nonceKey,
                nonceDigest,
                expectedNonce,
                nonce,
                ciphertext,
                tag,
                plaintext);
        }
    }

    private StoredReplayCheckpoint CreateCheckpoint(
        Guid processId,
        long sequence,
        string recordSha256,
        DateTimeOffset flushedAtUtc)
    {
        int keyVersion = this.keyOptions.ActiveKeyVersion;
        if (!this.TryGetMasterKey(keyVersion, out byte[] masterKey))
        {
            throw Unavailable("The active replay checkpoint key is unavailable.");
        }

        try
        {
            StoredReplayCheckpoint unsigned = new(
                StorageContractVersion,
                processId,
                sequence,
                recordSha256,
                keyVersion,
                CheckpointProofSha256: string.Empty,
                flushedAtUtc);
            return unsigned with
            {
                CheckpointProofSha256 =
                    ComputeCheckpointProofSha256(unsigned, masterKey)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    private bool HasValidCheckpoint(
        StoredReplayCheckpoint checkpoint,
        Guid processId)
    {
        if (checkpoint is null ||
            checkpoint.StorageContractVersion != StorageContractVersion ||
            checkpoint.ProcessId != processId ||
            checkpoint.Sequence < 0 ||
            !TenantTerminationReplayProof.IsSha256(
                checkpoint.RecordSha256) ||
            checkpoint.IntegrityKeyVersion <= 0 ||
            !TenantTerminationReplayProof.IsSha256(
                checkpoint.CheckpointProofSha256) ||
            checkpoint.FlushedAtUtc == default ||
            !this.TryGetMasterKey(
                checkpoint.IntegrityKeyVersion,
                out byte[] masterKey))
        {
            return false;
        }

        try
        {
            return TenantTerminationReplayProof.FixedTimeSha256Equals(
                checkpoint.CheckpointProofSha256,
                ComputeCheckpointProofSha256(checkpoint, masterKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    private async Task<ProcessLease> AcquireAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        RequireProcessId(processId);
        SemaphoreSlim gate = ProcessGates.GetOrAdd(
            processId,
            static _ => new(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string directoryPath = Path.Combine(
            this.rootPath,
            processId.ToString("N"));
        try
        {
            EnsureSecureDirectory(directoryPath);
            string lockPath = Path.Combine(directoryPath, LockFileName);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    FileStream lockStream = new(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 1,
                        FileOptions.Asynchronous);
                    SetSecureFileMode(lockPath);
                    return new(directoryPath, lockStream, gate);
                }
                catch (IOException)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(50),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    private bool TryGetMasterKey(int keyVersion, out byte[] key)
    {
        key = [];
        return keyVersion > 0 &&
            this.keyOptions.Keys.TryGetValue(
                keyVersion,
                out string? encodedKey) &&
            DataRightsPseudonymisationOptionsValidator.TryDecode(
                encodedKey,
                out key);
    }

    private static bool HasValidStoredShape(
        StoredReplayRecord stored,
        Guid processId,
        long expectedSequence,
        string expectedPreviousSha256) =>
        stored is not null &&
        stored.StorageContractVersion == StorageContractVersion &&
        stored.ProcessId == processId &&
        stored.Sequence == expectedSequence &&
        TenantTerminationReplayProof.FixedTimeSha256Equals(
            stored.PreviousRecordSha256,
            expectedPreviousSha256) &&
        TenantTerminationReplayProof.IsSha256(stored.LogicalEntryId) &&
        Enum.IsDefined(stored.Kind) &&
        stored.Kind != TenantTerminationReplayEntryKind.Unknown &&
        stored.EncryptionKeyVersion > 0 &&
        string.Equals(stored.Algorithm, AesAlgorithm, StringComparison.Ordinal) &&
        TryDecode(stored.NonceBase64, NonceSizeBytes, NonceSizeBytes) &&
        TryDecode(
            stored.CiphertextBase64,
            minimumBytes: 1,
            MaximumPlaintextBytes) &&
        TryDecode(
            stored.AuthenticationTagBase64,
            AuthenticationTagSizeBytes,
            AuthenticationTagSizeBytes) &&
        TenantTerminationReplayProof.IsSha256(stored.PlaintextSha256) &&
        TenantTerminationReplayProof.IsSha256(stored.RecordSha256) &&
        stored.FlushedAtUtc != default;

    private static string ComputeRecordSha256(StoredReplayRecord record)
    {
        StringBuilder canonical = new();
        Append(canonical, RecordHashDomain);
        AppendRecordMetadata(canonical, record);
        Append(canonical, record.NonceBase64);
        Append(canonical, record.CiphertextBase64);
        Append(canonical, record.AuthenticationTagBase64);
        return Hash(canonical);
    }

    private static byte[] CreateRecordBinding(
        Guid processId,
        long sequence,
        string previousRecordSha256,
        string logicalEntryId,
        TenantTerminationReplayEntryKind kind,
        int keyVersion,
        string plaintextSha256,
        DateTimeOffset flushedAtUtc)
    {
        StringBuilder canonical = new();
        Append(canonical, RecordBindingDomain);
        Append(canonical, StorageContractVersion);
        Append(canonical, processId);
        Append(canonical, sequence);
        Append(canonical, previousRecordSha256);
        Append(canonical, logicalEntryId);
        Append(canonical, (int)kind);
        Append(canonical, keyVersion);
        Append(canonical, AesAlgorithm);
        Append(canonical, plaintextSha256);
        Append(canonical, flushedAtUtc);
        return Encoding.UTF8.GetBytes(canonical.ToString());
    }

    private static void AppendRecordMetadata(
        StringBuilder canonical,
        StoredReplayRecord record)
    {
        Append(canonical, record.StorageContractVersion);
        Append(canonical, record.ProcessId);
        Append(canonical, record.Sequence);
        Append(canonical, record.PreviousRecordSha256);
        Append(canonical, record.LogicalEntryId);
        Append(canonical, (int)record.Kind);
        Append(canonical, record.EncryptionKeyVersion);
        Append(canonical, record.Algorithm);
        Append(canonical, record.PlaintextSha256);
        Append(canonical, record.FlushedAtUtc);
    }

    private static string ComputeCheckpointProofSha256(
        StoredReplayCheckpoint checkpoint,
        byte[] masterKey)
    {
        StringBuilder canonical = new();
        Append(canonical, CheckpointDomain);
        Append(canonical, checkpoint.StorageContractVersion);
        Append(canonical, checkpoint.ProcessId);
        Append(canonical, checkpoint.Sequence);
        Append(canonical, checkpoint.RecordSha256);
        Append(canonical, checkpoint.IntegrityKeyVersion);
        Append(canonical, checkpoint.FlushedAtUtc);
        byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        byte[] checkpointKey = Derive(masterKey, CheckpointKeyDomain);
        try
        {
            return Convert.ToHexStringLower(
                HMACSHA256.HashData(checkpointKey, bytes));
        }
        finally
        {
            Zero(bytes, checkpointKey);
        }
    }

    private static TenantTerminationReplayAppendReceipt CreateReceipt(
        StoredReplayRecord record,
        StoredReplayCheckpoint checkpoint) =>
        new(
            TenantTerminationReplayAppendReceipt.CurrentContractVersion,
            record.LogicalEntryId,
            record.Kind,
            new(record.Sequence, record.RecordSha256),
            checkpoint.FlushedAtUtc,
            checkpoint.CheckpointProofSha256);

    private static TenantTerminationReplayCheckpoint ToPublicCheckpoint(
        StoredReplayCheckpoint checkpoint) =>
        new(
            TenantTerminationReplayCheckpoint.CurrentContractVersion,
            checkpoint.ProcessId,
            new(checkpoint.Sequence, checkpoint.RecordSha256),
            checkpoint.IntegrityKeyVersion,
            checkpoint.CheckpointProofSha256,
            checkpoint.FlushedAtUtc);

    private static bool CursorMatches(
        IReadOnlyList<LoadedReplayRecord> records,
        TenantTerminationReplayCursor cursor)
    {
        if (cursor.Sequence == 0)
        {
            return TenantTerminationReplayProof.FixedTimeSha256Equals(
                cursor.RecordSha256,
                TenantTerminationReplayProof.GenesisSha256);
        }

        return cursor.Sequence <= records.Count &&
            TenantTerminationReplayProof.FixedTimeSha256Equals(
                cursor.RecordSha256,
                records[checked((int)cursor.Sequence - 1)]
                    .Storage.RecordSha256);
    }

    private static async Task<T> ReadJsonAsync<T>(
        string path,
        int maximumBytes,
        CancellationToken cancellationToken)
        where T : class
    {
        FileInfo info = new(path);
        if (!info.Exists || info.Length is <= 0 || info.Length > maximumBytes)
        {
            throw Integrity("A replay storage object has an invalid size.");
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(
                path,
                cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(bytes, SerializerOptions) ??
                throw Integrity("A replay storage object is empty.");
        }
        catch (JsonException exception)
        {
            throw new TenantTerminationReplayStoreException(
                IntegrityCode,
                "A replay storage object is malformed.",
                exception);
        }
    }

    private static async Task WriteNewFileAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
        SetSecureFileMode(path);
    }

    private static async Task WriteAtomicReplaceFileAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        string temporaryPath =
            $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await WriteNewFileAsync(
                temporaryPath,
                bytes,
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
            SetSecureFileMode(path);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static string RecordPath(string directoryPath, long sequence) =>
        Path.Combine(
            directoryPath,
            sequence.ToString("D20", CultureInfo.InvariantCulture) +
                RecordSuffix);

    private static bool TryParseRecordSequence(
        string path,
        out long sequence)
    {
        sequence = 0;
        string fileName = Path.GetFileName(path);
        string prefix = fileName.EndsWith(
                RecordSuffix,
                StringComparison.Ordinal)
            ? fileName[..^RecordSuffix.Length]
            : string.Empty;
        return prefix.Length == 20 &&
            long.TryParse(
                prefix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out sequence) &&
            sequence > 0;
    }

    private static string NormalizeTenant(string tenantId) =>
        TenantIds.TryNormalize(tenantId, out string? normalized)
            ? normalized
            : throw Invalid("The replay tenant id is invalid.");

    private static void RequireProcessId(Guid processId)
    {
        if (processId == Guid.Empty)
        {
            throw Invalid("The replay process id is invalid.");
        }
    }

    private static bool TryDecode(
        string? value,
        int minimumBytes,
        int maximumBytes)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            byte[] decoded = Convert.FromBase64String(value);
            try
            {
                return decoded.Length >= minimumBytes &&
                    decoded.Length <= maximumBytes;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(decoded);
            }
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(byte[] masterKey, string domain) =>
        HMACSHA256.HashData(masterKey, Encoding.ASCII.GetBytes(domain));

    private static string Hash(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string Hash(StringBuilder canonical)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        try
        {
            return Hash(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static void Append(StringBuilder target, Guid value) =>
        Append(target, value.ToString("N"));

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, long value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(
        StringBuilder target,
        DateTimeOffset value) =>
        Append(
            target,
            value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static void EnsureSecureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }
    }

    private static void SetSecureFileMode(string path)
    {
        if (!OperatingSystem.IsWindows() && File.Exists(path))
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Zero(params byte[][] buffers)
    {
        foreach (byte[] buffer in buffers)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static TenantTerminationReplayStoreException Invalid(
        string message) =>
        new(InvalidCode, message);

    private static TenantTerminationReplayStoreException Conflict(
        string message) =>
        new(ConflictCode, message);

    private static TenantTerminationReplayStoreException Integrity(
        string message) =>
        new(IntegrityCode, message);

    private static TenantTerminationReplayStoreException Unavailable(
        string message) =>
        new(UnavailableCode, message);

    private sealed record StoredReplayRecord(
        int StorageContractVersion,
        Guid ProcessId,
        long Sequence,
        string PreviousRecordSha256,
        string LogicalEntryId,
        TenantTerminationReplayEntryKind Kind,
        int EncryptionKeyVersion,
        string Algorithm,
        string NonceBase64,
        string CiphertextBase64,
        string AuthenticationTagBase64,
        string PlaintextSha256,
        string RecordSha256,
        DateTimeOffset FlushedAtUtc);

    private sealed record StoredReplayCheckpoint(
        int StorageContractVersion,
        Guid ProcessId,
        long Sequence,
        string RecordSha256,
        int IntegrityKeyVersion,
        string CheckpointProofSha256,
        DateTimeOffset FlushedAtUtc);

    private sealed record LoadedReplayRecord(
        StoredReplayRecord Storage,
        TenantTerminationReplayJournalEntry Entry);

    private sealed record TrustedJournalState(
        StoredReplayCheckpoint Checkpoint,
        IReadOnlyList<LoadedReplayRecord> Records);

    private sealed class ProcessLease(
        string directoryPath,
        FileStream lockStream,
        SemaphoreSlim gate) : IAsyncDisposable
    {
        public string DirectoryPath { get; } = directoryPath;

        public async ValueTask DisposeAsync()
        {
            await lockStream.DisposeAsync().ConfigureAwait(false);
            gate.Release();
        }
    }
}

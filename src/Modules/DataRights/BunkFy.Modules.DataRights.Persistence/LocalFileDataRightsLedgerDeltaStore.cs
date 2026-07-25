namespace BunkFy.Modules.DataRights.Persistence;

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using Gma.Framework.Naming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed class LocalFileDataRightsLedgerDeltaStore
    : IDataRightsLedgerDeltaStore, IDataRightsRestoreScopeSource
{
    internal const string ProviderName = "local-file";
    internal const string InvalidCode = "data-rights.ledger-delta.invalid";
    internal const string ConflictCode = "data-rights.ledger-delta.conflict";
    internal const string IntegrityCode =
        "data-rights.ledger-delta.integrity-failed";
    internal const string UnavailableCode =
        "data-rights.ledger-delta.unavailable";
    private const int StorageContractVersion = 1;
    private const int MaximumRecordBytes = 64 * 1024;
    private const int MaximumCheckpointBytes = 8 * 1024;
    private const string RecordSuffix = ".delta.json";
    private const string CheckpointFileName = "checkpoint.json";
    private const string LockFileName = ".append.lock";
    private const string StorageMacDomain =
        "bunkfy.data-rights.local-ledger-delta.record.v1";
    private const string CheckpointMacDomain =
        "bunkfy.data-rights.local-ledger-delta.checkpoint.v1";
    private const string RestoreScopeSnapshotDomain =
        "bunkfy.data-rights.local-ledger-delta.restore-scopes.v1";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim>
        TenantGates = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly DataRightsLedgerDeltaOptions options;
    private readonly string rootPath;
    private readonly TimeProvider timeProvider;

    public LocalFileDataRightsLedgerDeltaStore(
        IOptions<DataRightsLedgerDeltaOptions> options,
        IHostEnvironment environment,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.options = options.Value;
        string configuredPath = this.options.LocalFilePath?.Trim() ?? string.Empty;
        this.rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));
        this.timeProvider = timeProvider;
    }

    public async Task<DataRightsLedgerDeltaStoreReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            EnsureSecureDirectory(this.rootPath);
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
            DataRightsLedgerDeltaStoreException)
        {
            return new(
                ProviderName,
                IsReady: false,
                IsProductionGrade: false,
                UnavailableCode);
        }
    }

    async Task<DataRightsRestoreScopeSourceReadiness>
        IDataRightsRestoreScopeSource.CheckReadinessAsync(
            CancellationToken cancellationToken)
    {
        DataRightsLedgerDeltaStoreReadiness readiness =
            await this.CheckReadinessAsync(cancellationToken)
                .ConfigureAwait(false);
        return new(
            readiness.Provider,
            readiness.IsReady,
            readiness.IsProductionGrade,
            readiness.FailureCode);
    }

    public async Task<DataRightsRestoreScopeSnapshot> OpenSnapshotAsync(
        CancellationToken cancellationToken)
    {
        RestoreScopeManifest manifest =
            await this.BuildRestoreScopeManifestAsync(cancellationToken)
                .ConfigureAwait(false);
        return new(
            DataRightsRestoreScopeSnapshot.CurrentContractVersion,
            manifest.SnapshotSha256,
            this.timeProvider.GetUtcNow());
    }

    public async Task<DataRightsRestoreScopePage> ReadScopesAsync(
        DataRightsRestoreScopeSnapshot snapshot,
        string? afterScopeId,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!HasValidRestoreScopeSnapshot(snapshot) ||
            pageSize <= 0 ||
            pageSize > this.options.MaximumPageSize)
        {
            throw Invalid(
                "The restore-scope snapshot or page size is invalid.");
        }

        RestoreScopeManifest manifest =
            await this.BuildRestoreScopeManifestAsync(cancellationToken)
                .ConfigureAwait(false);
        if (!FixedTimeHexEquals(
                manifest.SnapshotSha256,
                snapshot.SnapshotSha256))
        {
            throw new DataRightsRestoreScopeSourceException(
                DataRightsRestoreScopeSourceException.SnapshotChangedCode,
                "The restore-scope snapshot changed during reconciliation.");
        }

        int start = 0;
        if (!string.IsNullOrWhiteSpace(afterScopeId))
        {
            string cursor = NormalizeScope(afterScopeId);
            while (start < manifest.Scopes.Count &&
                   string.CompareOrdinal(
                       manifest.Scopes[start].ScopeId,
                       cursor) <= 0)
            {
                start++;
            }
        }

        DataRightsRestoreScope[] page = manifest.Scopes
            .Skip(start)
            .Take(pageSize)
            .ToArray();
        int nextIndex = start + page.Length;
        bool hasMore = nextIndex < manifest.Scopes.Count;
        return new(
            DataRightsRestoreScopePage.CurrentContractVersion,
            page,
            hasMore ? page[^1].ScopeId : null,
            hasMore);
    }

    public async Task<bool> IsCurrentAsync(
        DataRightsRestoreScopeSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (!HasValidRestoreScopeSnapshot(snapshot))
        {
            return false;
        }

        RestoreScopeManifest manifest =
            await this.BuildRestoreScopeManifestAsync(cancellationToken)
                .ConfigureAwait(false);
        return FixedTimeHexEquals(
            manifest.SnapshotSha256,
            snapshot.SnapshotSha256);
    }

    public async Task<DataRightsLedgerDeltaAppendReceipt> AppendAsync(
        DataRightsLedgerDelta delta,
        CancellationToken cancellationToken)
    {
        if (delta is null || !delta.HasValidProof())
        {
            throw Invalid("The ledger delta proof is invalid.");
        }

        string scopeId = NormalizeScope(delta.Ledger.ScopeId);
        await using TenantLease lease = await this.AcquireTenantLeaseAsync(
            scopeId,
            cancellationToken).ConfigureAwait(false);
        LocalLedgerCheckpointRecord checkpoint =
            await this.LoadOrRecoverCheckpointAsync(
                scopeId,
                lease.DirectoryPath,
                cancellationToken).ConfigureAwait(false);

        long sequence = delta.Ledger.TenantSequence;
        if (sequence <= checkpoint.TenantSequence)
        {
            LocalLedgerDeltaRecord existing = await this.ReadRecordAsync(
                scopeId,
                lease.DirectoryPath,
                sequence,
                cancellationToken).ConfigureAwait(false);
            if (!DeltaEquals(existing.Delta, delta))
            {
                throw Conflict(
                    "The ledger sequence is already bound to another delta.");
            }

            return CreateReceipt(existing, checkpoint.CheckpointMacSha256);
        }

        if (sequence != checkpoint.TenantSequence + 1 ||
            !string.Equals(
                delta.Ledger.PreviousEntrySha256,
                checkpoint.EntrySha256,
                StringComparison.Ordinal))
        {
            throw Conflict(
                "The ledger delta does not continue the trusted tenant checkpoint.");
        }

        LocalLedgerDeltaRecord record = this.CreateRecord(
            delta,
            checkpoint.StorageMacSha256);
        string recordPath = RecordPath(lease.DirectoryPath, sequence);
        if (File.Exists(recordPath))
        {
            LocalLedgerDeltaRecord existing = await this.ReadRecordAsync(
                scopeId,
                lease.DirectoryPath,
                sequence,
                cancellationToken).ConfigureAwait(false);
            if (!DeltaEquals(existing.Delta, delta))
            {
                throw Conflict(
                    "The ledger entry id is already bound to another delta.");
            }

            record = existing;
        }
        else
        {
            byte[] recordBytes = JsonSerializer.SerializeToUtf8Bytes(
                record,
                SerializerOptions);
            await WriteAtomicNewFileAsync(
                recordPath,
                recordBytes,
                cancellationToken).ConfigureAwait(false);
        }

        LocalLedgerCheckpointRecord advanced =
            this.CreateCheckpoint(scopeId, record);
        await WriteAtomicReplaceFileAsync(
            Path.Combine(lease.DirectoryPath, CheckpointFileName),
            JsonSerializer.SerializeToUtf8Bytes(
                advanced,
                SerializerOptions),
            cancellationToken).ConfigureAwait(false);
        return CreateReceipt(record, advanced.CheckpointMacSha256);
    }

    public async Task<DataRightsLedgerDeltaCheckpoint>
        ReadTrustedCheckpointAsync(
            string tenantId,
            CancellationToken cancellationToken)
    {
        string scopeId = NormalizeScope(tenantId);
        await using TenantLease lease = await this.AcquireTenantLeaseAsync(
            scopeId,
            cancellationToken).ConfigureAwait(false);
        LocalLedgerCheckpointRecord checkpoint =
            await this.LoadOrRecoverCheckpointAsync(
                scopeId,
                lease.DirectoryPath,
                cancellationToken).ConfigureAwait(false);
        return checkpoint.ToContract();
    }

    public async Task<DataRightsLedgerDeltaPage> ReadAfterAsync(
        string tenantId,
        DataRightsLedgerDeltaCursor cursor,
        int pageSize,
        CancellationToken cancellationToken)
    {
        string scopeId = NormalizeScope(tenantId);
        if (!HasValidCursor(cursor) ||
            pageSize <= 0 ||
            pageSize > this.options.MaximumPageSize)
        {
            throw Invalid("The ledger delta cursor or page size is invalid.");
        }

        await using TenantLease lease = await this.AcquireTenantLeaseAsync(
            scopeId,
            cancellationToken).ConfigureAwait(false);
        LocalLedgerCheckpointRecord checkpoint =
            await this.LoadOrRecoverCheckpointAsync(
                scopeId,
                lease.DirectoryPath,
                cancellationToken).ConfigureAwait(false);
        if (cursor.TenantSequence > checkpoint.TenantSequence)
        {
            throw Integrity(
                "The ledger delta cursor is ahead of the trusted checkpoint.");
        }

        if (cursor.TenantSequence > 0)
        {
            LocalLedgerDeltaRecord cursorRecord = await this.ReadRecordAsync(
                scopeId,
                lease.DirectoryPath,
                cursor.TenantSequence,
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(
                    cursorRecord.Delta.Ledger.EntrySha256,
                    cursor.EntrySha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    cursorRecord.StorageMacSha256,
                    cursor.StorageMacSha256,
                    StringComparison.Ordinal))
            {
                throw Integrity(
                    "The ledger delta cursor does not match durable proof.");
            }
        }

        List<DataRightsLedgerDelta> deltas = [];
        DataRightsLedgerDeltaCursor next = cursor;
        long finalSequence = Math.Min(
            checkpoint.TenantSequence,
            cursor.TenantSequence + pageSize);
        for (long sequence = cursor.TenantSequence + 1;
             sequence <= finalSequence;
             sequence++)
        {
            LocalLedgerDeltaRecord record = await this.ReadRecordAsync(
                scopeId,
                lease.DirectoryPath,
                sequence,
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(
                    record.Delta.Ledger.PreviousEntrySha256,
                    next.EntrySha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.PreviousStorageMacSha256,
                    next.StorageMacSha256,
                    StringComparison.Ordinal))
            {
                throw Integrity(
                    "The ledger delta page does not continue its cursor.");
            }

            deltas.Add(record.Delta);
            next = new(
                sequence,
                record.Delta.Ledger.EntrySha256,
                record.StorageMacSha256);
        }

        return new(
            DataRightsLedgerDeltaPage.CurrentContractVersion,
            deltas,
            next,
            HasMore: next.TenantSequence < checkpoint.TenantSequence);
    }

    private async Task<TenantLease> AcquireTenantLeaseAsync(
        string scopeId,
        CancellationToken cancellationToken)
    {
        EnsureSecureDirectory(this.rootPath);
        string directoryPath = Path.Combine(
            this.rootPath,
            ScopeDigest(scopeId));
        EnsureSecureDirectory(directoryPath);
        SemaphoreSlim gate = TenantGates.GetOrAdd(
            directoryPath,
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            string lockPath = Path.Combine(directoryPath, LockFileName);
            RejectReparsePointIfPresent(lockPath, "ledger delta lock");
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    FileStream stream = new(lockPath, new FileStreamOptions
                    {
                        Mode = FileMode.OpenOrCreate,
                        Access = FileAccess.ReadWrite,
                        Share = FileShare.None,
                        Options = FileOptions.Asynchronous |
                            FileOptions.WriteThrough
                    });
                    RestrictFilePermissions(lockPath);
                    return new TenantLease(directoryPath, stream, gate);
                }
                catch (IOException)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(25),
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

    private async Task<RestoreScopeManifest> BuildRestoreScopeManifestAsync(
        CancellationToken cancellationToken)
    {
        EnsureSecureDirectory(this.rootPath);
        List<DataRightsRestoreScope> scopes = [];
        HashSet<string> seenScopes = new(StringComparer.Ordinal);
        foreach (string directoryPath in Directory.EnumerateDirectories(
                     this.rootPath,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePointIfPresent(
                directoryPath,
                "ledger delta tenant directory");
            string directoryName = Path.GetFileName(directoryPath);
            if (!IsSha256(directoryName))
            {
                throw Integrity(
                    "A local ledger delta tenant directory is invalid.");
            }

            SortedSet<long> sequences =
                EnumerateRecordSequences(directoryPath);
            if (sequences.Count == 0)
            {
                continue;
            }

            if (sequences.Min != 1)
            {
                throw Integrity(
                    "A local ledger delta tenant sequence is invalid.");
            }

            LocalLedgerDeltaRecord first = await ReadJsonAsync<
                LocalLedgerDeltaRecord>(
                    RecordPath(directoryPath, sequence: 1),
                    MaximumRecordBytes,
                    cancellationToken).ConfigureAwait(false);
            string scopeId = NormalizeScope(first.Delta?.Ledger.ScopeId);
            if (!string.Equals(
                    ScopeDigest(scopeId),
                    directoryName,
                    StringComparison.Ordinal) ||
                !seenScopes.Add(scopeId))
            {
                throw Integrity(
                    "A local ledger delta tenant directory conflicts with its proof.");
            }

            DataRightsLedgerDeltaCheckpoint checkpoint =
                await this.ReadTrustedCheckpointAsync(
                    scopeId,
                    cancellationToken).ConfigureAwait(false);
            scopes.Add(new(
                DataRightsRestoreScope.CurrentContractVersion,
                scopeId,
                checkpoint));
        }

        scopes.Sort(static (left, right) =>
            string.CompareOrdinal(left.ScopeId, right.ScopeId));
        return new(
            scopes,
            ComputeRestoreScopeSnapshotSha256(scopes));
    }

    private async Task<LocalLedgerCheckpointRecord>
        LoadOrRecoverCheckpointAsync(
            string scopeId,
            string directoryPath,
            CancellationToken cancellationToken)
    {
        string checkpointPath = Path.Combine(
            directoryPath,
            CheckpointFileName);
        LocalLedgerCheckpointRecord checkpoint = File.Exists(checkpointPath)
            ? await this.ReadCheckpointAsync(
                scopeId,
                checkpointPath,
                cancellationToken).ConfigureAwait(false)
            : this.CreateGenesisCheckpoint(scopeId);
        SortedSet<long> sequences = EnumerateRecordSequences(directoryPath);
        if (sequences.Count == 0)
        {
            if (checkpoint.TenantSequence != 0)
            {
                throw Integrity(
                    "The trusted checkpoint references missing ledger deltas.");
            }

            return checkpoint;
        }

        long maximumSequence = sequences.Max;
        if (maximumSequence < checkpoint.TenantSequence ||
            sequences.Min != 1 ||
            sequences.Count != maximumSequence)
        {
            throw Integrity(
                "The local ledger delta sequence contains a gap.");
        }

        if (checkpoint.TenantSequence > 0)
        {
            LocalLedgerDeltaRecord anchored = await this.ReadRecordAsync(
                scopeId,
                directoryPath,
                checkpoint.TenantSequence,
                cancellationToken).ConfigureAwait(false);
            if (!CheckpointMatchesRecord(checkpoint, anchored))
            {
                throw Integrity(
                    "The trusted checkpoint does not match its ledger delta.");
            }
        }

        bool recovered = false;
        for (long sequence = checkpoint.TenantSequence + 1;
             sequence <= maximumSequence;
             sequence++)
        {
            LocalLedgerDeltaRecord record = await this.ReadRecordAsync(
                scopeId,
                directoryPath,
                sequence,
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(
                    record.Delta.Ledger.PreviousEntrySha256,
                    checkpoint.EntrySha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    record.PreviousStorageMacSha256,
                    checkpoint.StorageMacSha256,
                    StringComparison.Ordinal))
            {
                throw Integrity(
                    "The local ledger delta integrity chain is invalid.");
            }

            checkpoint = this.CreateCheckpoint(scopeId, record);
            recovered = true;
        }

        if (recovered)
        {
            await WriteAtomicReplaceFileAsync(
                checkpointPath,
                JsonSerializer.SerializeToUtf8Bytes(
                    checkpoint,
                    SerializerOptions),
                cancellationToken).ConfigureAwait(false);
        }

        return checkpoint;
    }

    private async Task<LocalLedgerDeltaRecord> ReadRecordAsync(
        string scopeId,
        string directoryPath,
        long sequence,
        CancellationToken cancellationToken)
    {
        string path = RecordPath(directoryPath, sequence);
        LocalLedgerDeltaRecord record = await ReadJsonAsync<
            LocalLedgerDeltaRecord>(
                path,
                MaximumRecordBytes,
                cancellationToken).ConfigureAwait(false);
        if (record.StorageContractVersion != StorageContractVersion ||
            record.Delta is null ||
            !record.Delta.HasValidProof() ||
            record.Delta.Ledger.TenantSequence != sequence ||
            !string.Equals(
                record.Delta.Ledger.ScopeId,
                scopeId,
                StringComparison.Ordinal) ||
            !IsSha256(record.PreviousStorageMacSha256) ||
            !IsSha256(record.StorageMacSha256))
        {
            throw Integrity("A local ledger delta record is invalid.");
        }

        string expectedMac = this.ComputeStorageMac(record);
        if (!FixedTimeHexEquals(expectedMac, record.StorageMacSha256))
        {
            throw Integrity(
                "A local ledger delta record failed integrity verification.");
        }

        return record;
    }

    private async Task<LocalLedgerCheckpointRecord> ReadCheckpointAsync(
        string scopeId,
        string path,
        CancellationToken cancellationToken)
    {
        LocalLedgerCheckpointRecord checkpoint = await ReadJsonAsync<
            LocalLedgerCheckpointRecord>(
                path,
                MaximumCheckpointBytes,
                cancellationToken).ConfigureAwait(false);
        if (checkpoint.StorageContractVersion != StorageContractVersion ||
            !string.Equals(
                checkpoint.ScopeSha256,
                ScopeDigest(scopeId),
                StringComparison.Ordinal) ||
            checkpoint.TenantSequence < 0 ||
            checkpoint.IntegrityKeyVersion <= 0 ||
            !IsSha256(checkpoint.EntrySha256) ||
            !IsSha256(checkpoint.StorageMacSha256) ||
            !IsSha256(checkpoint.CheckpointMacSha256))
        {
            throw Integrity("The local ledger delta checkpoint is invalid.");
        }

        if (!FixedTimeHexEquals(
                this.ComputeCheckpointMac(checkpoint),
                checkpoint.CheckpointMacSha256))
        {
            throw Integrity(
                "The local ledger delta checkpoint failed integrity verification.");
        }

        return checkpoint;
    }

    private LocalLedgerDeltaRecord CreateRecord(
        DataRightsLedgerDelta delta,
        string previousStorageMacSha256)
    {
        LocalLedgerDeltaRecord unsigned = new(
            StorageContractVersion,
            this.options.ActiveIntegrityKeyVersion,
            previousStorageMacSha256,
            this.timeProvider.GetUtcNow(),
            delta,
            StorageMacSha256:
                DataRightsProcessingLedgerEntry.GenesisEntrySha256);
        return unsigned with
        {
            StorageMacSha256 = this.ComputeStorageMac(unsigned)
        };
    }

    private LocalLedgerCheckpointRecord CreateGenesisCheckpoint(
        string scopeId)
    {
        LocalLedgerCheckpointRecord unsigned = new(
            StorageContractVersion,
            ScopeDigest(scopeId),
            TenantSequence: 0,
            DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            this.options.ActiveIntegrityKeyVersion,
            CheckpointMacSha256:
                DataRightsProcessingLedgerEntry.GenesisEntrySha256);
        return unsigned with
        {
            CheckpointMacSha256 = this.ComputeCheckpointMac(unsigned)
        };
    }

    private LocalLedgerCheckpointRecord CreateCheckpoint(
        string scopeId,
        LocalLedgerDeltaRecord record)
    {
        LocalLedgerCheckpointRecord unsigned = new(
            StorageContractVersion,
            ScopeDigest(scopeId),
            record.Delta.Ledger.TenantSequence,
            record.Delta.Ledger.EntrySha256,
            record.StorageMacSha256,
            this.options.ActiveIntegrityKeyVersion,
            CheckpointMacSha256:
                DataRightsProcessingLedgerEntry.GenesisEntrySha256);
        return unsigned with
        {
            CheckpointMacSha256 = this.ComputeCheckpointMac(unsigned)
        };
    }

    private string ComputeStorageMac(LocalLedgerDeltaRecord record)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            record.Delta,
            SerializerOptions);
        string canonical = string.Join(
            '\n',
            StorageMacDomain,
            record.StorageContractVersion.ToString(
                CultureInfo.InvariantCulture),
            record.IntegrityKeyVersion.ToString(
                CultureInfo.InvariantCulture),
            record.PreviousStorageMacSha256,
            record.FlushedAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            Convert.ToBase64String(payload));
        CryptographicOperations.ZeroMemory(payload);
        return this.ComputeMac(record.IntegrityKeyVersion, canonical);
    }

    private string ComputeCheckpointMac(
        LocalLedgerCheckpointRecord checkpoint)
    {
        string canonical = string.Join(
            '\n',
            CheckpointMacDomain,
            checkpoint.StorageContractVersion.ToString(
                CultureInfo.InvariantCulture),
            checkpoint.ScopeSha256,
            checkpoint.TenantSequence.ToString(
                CultureInfo.InvariantCulture),
            checkpoint.EntrySha256,
            checkpoint.StorageMacSha256,
            checkpoint.IntegrityKeyVersion.ToString(
                CultureInfo.InvariantCulture));
        return this.ComputeMac(checkpoint.IntegrityKeyVersion, canonical);
    }

    private string ComputeMac(int keyVersion, string canonical)
    {
        if (!this.options.IntegrityKeys.TryGetValue(
                keyVersion,
                out string? encodedKey) ||
            !DataRightsPseudonymisationOptionsValidator.TryDecode(
                encodedKey,
                out byte[] key))
        {
            throw Integrity(
                "A required local ledger delta integrity key is unavailable.");
        }

        byte[] input = Encoding.UTF8.GetBytes(canonical);
        try
        {
            return Convert.ToHexStringLower(
                HMACSHA256.HashData(key, input));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static void EnsureSecureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        RejectReparsePath(path, "ledger delta directory");
        RestrictDirectoryPermissions(path);
    }

    private static async Task<T> ReadJsonAsync<T>(
        string path,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        RejectReparsePointIfPresent(path, "ledger delta file");
        FileInfo file = new(path);
        if (!file.Exists || file.Length <= 0 || file.Length > maximumBytes)
        {
            throw Integrity("A local ledger delta file has an invalid size.");
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(
                path,
                cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(
                    bytes,
                    SerializerOptions)
                ?? throw Integrity(
                    "A local ledger delta file is empty.");
        }
        catch (JsonException exception)
        {
            throw new DataRightsLedgerDeltaStoreException(
                IntegrityCode,
                "A local ledger delta file is malformed.",
                exception);
        }
    }

    private static async Task WriteAtomicNewFileAsync(
        string destinationPath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        string temporaryPath = destinationPath +
            $".{Guid.NewGuid():N}.tmp";
        try
        {
            await WriteNewFileAsync(
                temporaryPath,
                bytes,
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, destinationPath, overwrite: false);
            RestrictFilePermissions(destinationPath);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static async Task WriteAtomicReplaceFileAsync(
        string destinationPath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        string temporaryPath = destinationPath +
            $".{Guid.NewGuid():N}.tmp";
        try
        {
            await WriteNewFileAsync(
                temporaryPath,
                bytes,
                cancellationToken).ConfigureAwait(false);
            RejectReparsePointIfPresent(
                destinationPath,
                "ledger delta checkpoint");
            File.Move(temporaryPath, destinationPath, overwrite: true);
            RestrictFilePermissions(destinationPath);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static async Task WriteNewFileAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough
        });
        RestrictFilePermissions(path);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static SortedSet<long> EnumerateRecordSequences(
        string directoryPath)
    {
        SortedSet<long> sequences = [];
        foreach (string path in Directory.EnumerateFiles(
                     directoryPath,
                     $"*{RecordSuffix}",
                     SearchOption.TopDirectoryOnly))
        {
            RejectReparsePointIfPresent(path, "ledger delta record");
            string name = Path.GetFileName(path);
            string sequenceText = name[..^RecordSuffix.Length];
            if (sequenceText.Length != 20 ||
                !long.TryParse(
                    sequenceText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long sequence) ||
                sequence <= 0 ||
                !sequences.Add(sequence))
            {
                throw Integrity(
                    "The local ledger delta sequence filename is invalid.");
            }
        }

        return sequences;
    }

    private static bool CheckpointMatchesRecord(
        LocalLedgerCheckpointRecord checkpoint,
        LocalLedgerDeltaRecord record) =>
        checkpoint.TenantSequence == record.Delta.Ledger.TenantSequence &&
        string.Equals(
            checkpoint.EntrySha256,
            record.Delta.Ledger.EntrySha256,
            StringComparison.Ordinal) &&
        string.Equals(
            checkpoint.StorageMacSha256,
            record.StorageMacSha256,
            StringComparison.Ordinal);

    private static DataRightsLedgerDeltaAppendReceipt CreateReceipt(
        LocalLedgerDeltaRecord record,
        string durabilityProofSha256) =>
        new(
            DataRightsLedgerDeltaAppendReceipt.CurrentContractVersion,
            record.Delta.Ledger.EntryId,
            new(
                record.Delta.Ledger.TenantSequence,
                record.Delta.Ledger.EntrySha256,
                record.StorageMacSha256),
            record.FlushedAtUtc,
            durabilityProofSha256);

    private static bool DeltaEquals(
        DataRightsLedgerDelta left,
        DataRightsLedgerDelta right)
    {
        byte[] leftBytes = JsonSerializer.SerializeToUtf8Bytes(
            left,
            SerializerOptions);
        byte[] rightBytes = JsonSerializer.SerializeToUtf8Bytes(
            right,
            SerializerOptions);
        try
        {
            return leftBytes.AsSpan().SequenceEqual(rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    private static bool HasValidCursor(
        DataRightsLedgerDeltaCursor? cursor)
    {
        if (cursor is null ||
            cursor.TenantSequence < 0 ||
            !IsSha256(cursor.EntrySha256) ||
            !IsSha256(cursor.StorageMacSha256))
        {
            return false;
        }

        return cursor.TenantSequence != 0 ||
            (string.Equals(
                cursor.EntrySha256,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
                StringComparison.Ordinal) &&
             string.Equals(
                cursor.StorageMacSha256,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
                StringComparison.Ordinal));
    }

    private static bool HasValidRestoreScopeSnapshot(
        DataRightsRestoreScopeSnapshot? snapshot) =>
        snapshot is not null &&
        snapshot.ContractVersion ==
            DataRightsRestoreScopeSnapshot.CurrentContractVersion &&
        IsSha256(snapshot.SnapshotSha256) &&
        snapshot.OpenedAtUtc != default &&
        snapshot.OpenedAtUtc.Offset == TimeSpan.Zero;

    private static string ComputeRestoreScopeSnapshotSha256(
        List<DataRightsRestoreScope> scopes)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendHash(hash, RestoreScopeSnapshotDomain);
        AppendHash(
            hash,
            scopes.Count.ToString(CultureInfo.InvariantCulture));
        foreach (DataRightsRestoreScope scope in scopes)
        {
            DataRightsLedgerDeltaCheckpoint checkpoint =
                scope.TrustedCheckpoint;
            AppendHash(hash, scope.ScopeId);
            AppendHash(
                hash,
                checkpoint.Cursor.TenantSequence.ToString(
                    CultureInfo.InvariantCulture));
            AppendHash(hash, checkpoint.Cursor.EntrySha256);
            AppendHash(hash, checkpoint.Cursor.StorageMacSha256);
            AppendHash(
                hash,
                checkpoint.IntegrityKeyVersion.ToString(
                    CultureInfo.InvariantCulture));
            AppendHash(hash, checkpoint.CheckpointMacSha256);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendHash(
        IncrementalHash hash,
        string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        try
        {
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string NormalizeScope(string? tenantId)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            throw Invalid("The ledger delta tenant coordinate is invalid.");
        }

        return scopeId;
    }

    private static string ScopeDigest(string scopeId) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(scopeId)));

    private static string RecordPath(
        string directoryPath,
        long sequence) =>
        Path.Combine(
            directoryPath,
            sequence.ToString("D20", CultureInfo.InvariantCulture) +
            RecordSuffix);

    private static bool IsSha256(string? value) =>
        value is { Length: DataRightsProcessingLedgerEntry.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool FixedTimeHexEquals(
        string expected,
        string actual)
    {
        if (!IsSha256(expected) || !IsSha256(actual))
        {
            return false;
        }

        byte[] expectedBytes = Convert.FromHexString(expected);
        byte[] actualBytes = Convert.FromHexString(actual);
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                actualBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes);
            CryptographicOperations.ZeroMemory(actualBytes);
        }
    }

    private static void RejectReparsePath(
        string path,
        string description)
    {
        DirectoryInfo? current = new(path);
        while (current is not null)
        {
            if (current.Exists &&
                current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw Invalid(
                    $"The {description} must not contain a reparse point.");
            }

            current = current.Parent;
        }
    }

    private static void RejectReparsePointIfPresent(
        string path,
        string description)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            throw Invalid(
                $"The {description} must not be a reparse point.");
        }
    }

    private static void RestrictDirectoryPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }
    }

    private static void RestrictFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static DataRightsLedgerDeltaStoreException Invalid(
        string message) =>
        new(InvalidCode, message);

    private static DataRightsLedgerDeltaStoreException Conflict(
        string message) =>
        new(ConflictCode, message);

    private static DataRightsLedgerDeltaStoreException Integrity(
        string message) =>
        new(IntegrityCode, message);

    private sealed class TenantLease(
        string directoryPath,
        FileStream stream,
        SemaphoreSlim gate) : IAsyncDisposable
    {
        public string DirectoryPath { get; } = directoryPath;

        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            gate.Release();
        }
    }

    private sealed record RestoreScopeManifest(
        IReadOnlyList<DataRightsRestoreScope> Scopes,
        string SnapshotSha256);
}

internal sealed record LocalLedgerDeltaRecord(
    int StorageContractVersion,
    int IntegrityKeyVersion,
    string PreviousStorageMacSha256,
    DateTimeOffset FlushedAtUtc,
    DataRightsLedgerDelta Delta,
    string StorageMacSha256);

internal sealed record LocalLedgerCheckpointRecord(
    int StorageContractVersion,
    string ScopeSha256,
    long TenantSequence,
    string EntrySha256,
    string StorageMacSha256,
    int IntegrityKeyVersion,
    string CheckpointMacSha256)
{
    public DataRightsLedgerDeltaCheckpoint ToContract() =>
        new(
            DataRightsLedgerDeltaCheckpoint.CurrentContractVersion,
            new(
                this.TenantSequence,
                this.EntrySha256,
                this.StorageMacSha256),
            this.IntegrityKeyVersion,
            this.CheckpointMacSha256);
}

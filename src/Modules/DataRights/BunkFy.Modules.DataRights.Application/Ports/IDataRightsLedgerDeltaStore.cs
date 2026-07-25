namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;

public interface IDataRightsLedgerDeltaStore
{
    Task<DataRightsLedgerDeltaStoreReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken);

    Task<DataRightsLedgerDeltaAppendReceipt> AppendAsync(
        DataRightsLedgerDelta delta,
        CancellationToken cancellationToken);

    Task<DataRightsLedgerDeltaCheckpoint> ReadTrustedCheckpointAsync(
        string tenantId,
        CancellationToken cancellationToken);

    Task<DataRightsLedgerDeltaPage> ReadAfterAsync(
        string tenantId,
        DataRightsLedgerDeltaCursor cursor,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record DataRightsLedgerDelta(
    int ContractVersion,
    DataRightsProcessingLedgerSnapshot Ledger,
    DataRightsProtectedReplayEnvelope ReplayEnvelope)
{
    public const int CurrentContractVersion = 1;

    public static DataRightsLedgerDelta Create(
        DataRightsProcessingLedgerEntry ledgerEntry,
        DataRightsProtectedReplayEnvelope replayEnvelope)
    {
        ArgumentNullException.ThrowIfNull(ledgerEntry);
        ArgumentNullException.ThrowIfNull(replayEnvelope);
        return new(
            CurrentContractVersion,
            ledgerEntry.Freeze(),
            replayEnvelope);
    }

    public bool HasValidProof() =>
        this.ContractVersion == CurrentContractVersion &&
        this.Ledger is not null &&
        this.ReplayEnvelope is not null &&
        DataRightsProcessingLedgerEntry.Restore(this.Ledger).IsSuccess &&
        this.ReplayEnvelope.HasValidShape();
}

public sealed record DataRightsProtectedReplayEnvelope(
    int ContractVersion,
    int KeyVersion,
    string Algorithm,
    string NonceBase64,
    string CiphertextBase64,
    string AuthenticationTagBase64)
{
    public const int CurrentContractVersion = 1;
    public const string Aes256GcmAlgorithm = "A256GCM";
    public const int NonceSizeBytes = 12;
    public const int AuthenticationTagSizeBytes = 16;
    public const int MaximumCiphertextSizeBytes = 256;

    public bool HasValidShape() =>
        this.ContractVersion == CurrentContractVersion &&
        this.KeyVersion > 0 &&
        string.Equals(
            this.Algorithm,
            Aes256GcmAlgorithm,
            StringComparison.Ordinal) &&
        TryDecodeExact(this.NonceBase64, NonceSizeBytes) &&
        TryDecodeBounded(
            this.CiphertextBase64,
            minimumSize: 1,
            MaximumCiphertextSizeBytes) &&
        TryDecodeExact(
            this.AuthenticationTagBase64,
            AuthenticationTagSizeBytes);

    private static bool TryDecodeExact(string? value, int expectedSize) =>
        TryDecodeBounded(value, expectedSize, expectedSize);

    private static bool TryDecodeBounded(
        string? value,
        int minimumSize,
        int maximumSize)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(value);
            return bytes.Length >= minimumSize && bytes.Length <= maximumSize;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record DataRightsLedgerDeltaCursor(
    long TenantSequence,
    string EntrySha256,
    string StorageMacSha256)
{
    public static readonly DataRightsLedgerDeltaCursor Genesis = new(
        TenantSequence: 0,
        DataRightsProcessingLedgerEntry.GenesisEntrySha256,
        DataRightsProcessingLedgerEntry.GenesisEntrySha256);
}

public sealed record DataRightsLedgerDeltaCheckpoint(
    int ContractVersion,
    DataRightsLedgerDeltaCursor Cursor,
    int IntegrityKeyVersion,
    string CheckpointMacSha256)
{
    public const int CurrentContractVersion = 1;
}

public sealed record DataRightsLedgerDeltaAppendReceipt(
    int ContractVersion,
    Guid LedgerEntryId,
    DataRightsLedgerDeltaCursor Cursor,
    DateTimeOffset FlushedAtUtc,
    string DurabilityProofSha256)
{
    public const int CurrentContractVersion = 1;
}

public sealed record DataRightsLedgerDeltaPage(
    int ContractVersion,
    IReadOnlyList<DataRightsLedgerDelta> Deltas,
    DataRightsLedgerDeltaCursor NextCursor,
    bool HasMore)
{
    public const int CurrentContractVersion = 1;
}

public sealed record DataRightsLedgerDeltaStoreReadiness(
    string Provider,
    bool IsReady,
    bool IsProductionGrade,
    string? FailureCode);

public sealed class DataRightsLedgerDeltaStoreException : Exception
{
    public DataRightsLedgerDeltaStoreException(string code, string message)
        : base(message)
        => this.Code = code;

    public DataRightsLedgerDeltaStoreException(
        string code,
        string message,
        Exception innerException)
        : base(message, innerException)
        => this.Code = code;

    public string Code { get; }
}

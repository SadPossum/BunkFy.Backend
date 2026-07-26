namespace BunkFy.Modules.Ingestion.Domain.DataRights;

using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class IngestionAnonymisationTombstone : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;

    private IngestionAnonymisationTombstone() { }

    private IngestionAnonymisationTombstone(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public IngestionAnonymisationTombstoneState State { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public long SelectedSourceLinkVersion { get; private set; }
    public long ResultingSourceLinkVersion { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } = string.Empty;
    public DateTimeOffset OriginallyCompletedAtUtc { get; private set; }
    public Guid LedgerEntryId { get; private set; }
    public long TenantSequence { get; private set; }
    public string LedgerEntrySha256 { get; private set; } = string.Empty;
    public int GraphRecordCount { get; private set; }
    public int FingerprintCount { get; private set; }
    public int RawPayloadCount { get; private set; }
    public DateTimeOffset ReplayStartedAtUtc { get; private set; }
    public DateTimeOffset? LastReplayedAtUtc { get; private set; }

    public static Result<IngestionAnonymisationTombstone> BeginRestore(
        string tenantId,
        Guid sourceLinkId,
        Guid propertyId,
        Guid connectionId,
        long selectedSourceLinkVersion,
        long resultingSourceLinkVersion,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId,
        long tenantSequence,
        string ledgerEntrySha256,
        int graphRecordCount,
        int fingerprintCount,
        int rawPayloadCount,
        DateTimeOffset replayStartedAtUtc)
    {
        string receiptDigest = NormalizeSha256(ownerReceiptSha256);
        string ledgerDigest = NormalizeSha256(ledgerEntrySha256);
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            sourceLinkId == Guid.Empty ||
            propertyId == Guid.Empty ||
            connectionId == Guid.Empty ||
            selectedSourceLinkVersion <= 0 ||
            resultingSourceLinkVersion != selectedSourceLinkVersion + 1 ||
            ownerReceiptContractVersion <= 0 ||
            ownerReceiptId == Guid.Empty ||
            !IsSha256(receiptDigest) ||
            originallyCompletedAtUtc == default ||
            ledgerEntryId == Guid.Empty ||
            tenantSequence <= 0 ||
            !IsSha256(ledgerDigest) ||
            graphRecordCount <= 0 ||
            fingerprintCount <= 0 ||
            rawPayloadCount < 0 ||
            replayStartedAtUtc == default)
        {
            return Invalid();
        }

        return Result.Success(new IngestionAnonymisationTombstone(
            sourceLinkId,
            scopeId)
        {
            ContractVersion = CurrentContractVersion,
            Revision = 1,
            State = IngestionAnonymisationTombstoneState.Reducing,
            PropertyId = propertyId,
            ConnectionId = connectionId,
            SelectedSourceLinkVersion = selectedSourceLinkVersion,
            ResultingSourceLinkVersion = resultingSourceLinkVersion,
            OwnerReceiptContractVersion = ownerReceiptContractVersion,
            OwnerReceiptId = ownerReceiptId,
            OwnerReceiptSha256 = receiptDigest,
            OriginallyCompletedAtUtc =
                originallyCompletedAtUtc.ToUniversalTime(),
            LedgerEntryId = ledgerEntryId,
            TenantSequence = tenantSequence,
            LedgerEntrySha256 = ledgerDigest,
            GraphRecordCount = graphRecordCount,
            FingerprintCount = fingerprintCount,
            RawPayloadCount = rawPayloadCount,
            ReplayStartedAtUtc = replayStartedAtUtc.ToUniversalTime()
        });
    }

    public Result CompleteRestore(DateTimeOffset replayedAtUtc)
    {
        DateTimeOffset completed = replayedAtUtc.ToUniversalTime();
        if (completed == default || completed < this.ReplayStartedAtUtc)
        {
            return InvalidResult();
        }

        if (this.State == IngestionAnonymisationTombstoneState.Completed)
        {
            return this.LastReplayedAtUtc == completed
                ? Result.Success()
                : InvalidResult();
        }

        if (this.State != IngestionAnonymisationTombstoneState.Reducing)
        {
            return InvalidResult();
        }

        this.State = IngestionAnonymisationTombstoneState.Completed;
        this.LastReplayedAtUtc = completed;
        this.Revision = checked(this.Revision + 1);
        return Result.Success();
    }

    public bool MatchesRestore(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId,
        long tenantSequence,
        string ledgerEntrySha256) =>
        this.ContractVersion == CurrentContractVersion &&
        this.Revision >= 1 &&
        this.PropertyId == propertyId &&
        this.OwnerReceiptContractVersion == ownerReceiptContractVersion &&
        this.OwnerReceiptId == ownerReceiptId &&
        string.Equals(
            this.OwnerReceiptSha256,
            NormalizeSha256(ownerReceiptSha256),
            StringComparison.Ordinal) &&
        this.OriginallyCompletedAtUtc ==
            originallyCompletedAtUtc.ToUniversalTime() &&
        this.LedgerEntryId == ledgerEntryId &&
        this.TenantSequence == tenantSequence &&
        string.Equals(
            this.LedgerEntrySha256,
            NormalizeSha256(ledgerEntrySha256),
            StringComparison.Ordinal);

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<IngestionAnonymisationTombstone> Invalid() =>
        Result.Failure<IngestionAnonymisationTombstone>(
            IngestionDomainErrors.AnonymisationTombstoneInvalid);

    private static Result InvalidResult() =>
        Result.Failure(
            IngestionDomainErrors.AnonymisationTombstoneInvalid);
}

public enum IngestionAnonymisationTombstoneState
{
    Unknown = 0,
    Reducing = 1,
    Completed = 2
}

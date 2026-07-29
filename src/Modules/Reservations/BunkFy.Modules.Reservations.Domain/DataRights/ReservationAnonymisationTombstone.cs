namespace BunkFy.Modules.Reservations.Domain.DataRights;

using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Models;
using BunkFy.Modules.Reservations.Domain.Retention;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationAnonymisationTombstone
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 2;

    private ReservationAnonymisationTombstone() { }

    private ReservationAnonymisationTombstone(
        Guid reservationId,
        string scopeId)
        : base(reservationId, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public ReservationAnonymisationAuthority Authority { get; private set; }
    public Guid PropertyId { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } = string.Empty;
    public long ResultingReservationVersion { get; private set; }
    public long? ResultingDetailsRevision { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public DateTimeOffset? LastReplayedAtUtc { get; private set; }

    public static Result<ReservationAnonymisationTombstone> Create(
        ReservationAnonymisationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!receipt.MatchesOwnerProof(
                receipt.ContractVersion,
                receipt.Id,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.ResultingReservationVersion,
                receipt.CanonicalSha256,
                receipt.CompletedAtUtc))
        {
            return Invalid();
        }

        return Result.Success(new ReservationAnonymisationTombstone(
            receipt.ReservationId,
            receipt.ScopeId)
        {
            ContractVersion = CurrentContractVersion,
            Revision = 1,
            Authority = ReservationAnonymisationAuthority.DataRights,
            PropertyId = receipt.PropertyId,
            OwnerReceiptContractVersion = receipt.ContractVersion,
            OwnerReceiptId = receipt.Id,
            OwnerReceiptSha256 = receipt.CanonicalSha256,
            ResultingReservationVersion =
                receipt.ResultingReservationVersion,
            ResultingDetailsRevision = receipt.ResultingDetailsRevision,
            CompletedAtUtc = receipt.CompletedAtUtc.ToUniversalTime()
        });
    }

    public static Result<ReservationAnonymisationTombstone>
        CreateForRetention(
            ReservationRetentionAnonymisationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!receipt.MatchesOwnerProof(
                receipt.ContractVersion,
                receipt.Id,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.ResultingReservationVersion,
                receipt.ResultingDetailsRevision,
                receipt.CanonicalSha256,
                receipt.CompletedAtUtc))
        {
            return Invalid();
        }

        return Result.Success(new ReservationAnonymisationTombstone(
            receipt.ReservationId,
            receipt.ScopeId)
        {
            ContractVersion = CurrentContractVersion,
            Revision = 1,
            Authority = ReservationAnonymisationAuthority.Retention,
            PropertyId = receipt.PropertyId,
            OwnerReceiptContractVersion = receipt.ContractVersion,
            OwnerReceiptId = receipt.Id,
            OwnerReceiptSha256 = receipt.CanonicalSha256,
            ResultingReservationVersion =
                receipt.ResultingReservationVersion,
            ResultingDetailsRevision = receipt.ResultingDetailsRevision,
            CompletedAtUtc = receipt.CompletedAtUtc.ToUniversalTime()
        });
    }

    public static Result<ReservationAnonymisationTombstone> Restore(
        string tenantId,
        Guid reservationId,
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingReservationVersion,
        long? resultingDetailsRevision,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId,
        DateTimeOffset replayedAtUtc)
    {
        string receiptSha256 = NormalizeSha256(ownerReceiptSha256);
        DateTimeOffset completedAtUtc =
            originallyCompletedAtUtc.ToUniversalTime();
        DateTimeOffset restoredAtUtc = replayedAtUtc.ToUniversalTime();
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            reservationId == Guid.Empty ||
            propertyId == Guid.Empty ||
            ownerReceiptContractVersion <= 0 ||
            ownerReceiptId == Guid.Empty ||
            !IsSha256(receiptSha256) ||
            resultingReservationVersion <= 0 ||
            resultingDetailsRevision is <= 0 ||
            completedAtUtc == default ||
            ledgerEntryId == Guid.Empty ||
            restoredAtUtc == default ||
            restoredAtUtc < completedAtUtc)
        {
            return Invalid();
        }

        return Result.Success(new ReservationAnonymisationTombstone(
            reservationId,
            scopeId)
        {
            ContractVersion = CurrentContractVersion,
            Revision = 1,
            Authority = ReservationAnonymisationAuthority.DataRights,
            PropertyId = propertyId,
            OwnerReceiptContractVersion = ownerReceiptContractVersion,
            OwnerReceiptId = ownerReceiptId,
            OwnerReceiptSha256 = receiptSha256,
            ResultingReservationVersion = resultingReservationVersion,
            ResultingDetailsRevision = resultingDetailsRevision,
            CompletedAtUtc = completedAtUtc,
            LedgerEntryId = ledgerEntryId,
            LastReplayedAtUtc = restoredAtUtc
        });
    }

    public Result AttachRestoreProof(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingReservationVersion,
        long? resultingDetailsRevision,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId,
        DateTimeOffset replayedAtUtc)
    {
        DateTimeOffset restoredAtUtc = replayedAtUtc.ToUniversalTime();
        if (this.Authority !=
                ReservationAnonymisationAuthority.DataRights ||
            !this.MatchesOwnerProof(
                propertyId,
                ownerReceiptContractVersion,
                ownerReceiptId,
                ownerReceiptSha256,
                resultingReservationVersion,
                resultingDetailsRevision,
                originallyCompletedAtUtc) ||
            ledgerEntryId == Guid.Empty ||
            restoredAtUtc == default ||
            restoredAtUtc < this.CompletedAtUtc)
        {
            return Result.Failure(
                ReservationsDomainErrors
                    .ReservationAnonymisationTombstoneInvalid);
        }

        if (this.LedgerEntryId.HasValue)
        {
            return this.LedgerEntryId == ledgerEntryId &&
                this.LastReplayedAtUtc.HasValue
                ? Result.Success()
                : Result.Failure(
                    ReservationsDomainErrors
                        .ReservationAnonymisationTombstoneInvalid);
        }

        this.LedgerEntryId = ledgerEntryId;
        this.LastReplayedAtUtc = restoredAtUtc;
        this.Revision++;
        return Result.Success();
    }

    public bool Matches(
        ReservationAnonymisationReceipt receipt) =>
        receipt is not null &&
        this.Authority == ReservationAnonymisationAuthority.DataRights &&
        this.Id == receipt.ReservationId &&
        this.MatchesOwnerProof(
            receipt.PropertyId,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingReservationVersion,
            receipt.ResultingDetailsRevision,
            receipt.CompletedAtUtc);

    public bool MatchesRetention(
        ReservationRetentionAnonymisationReceipt receipt) =>
        receipt is not null &&
        this.Authority == ReservationAnonymisationAuthority.Retention &&
        this.Id == receipt.ReservationId &&
        this.MatchesOwnerProof(
            receipt.PropertyId,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingReservationVersion,
            receipt.ResultingDetailsRevision,
            receipt.CompletedAtUtc);

    public bool MatchesRestore(
        Guid reservationId,
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingReservationVersion,
        long? resultingDetailsRevision,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId) =>
        this.Authority == ReservationAnonymisationAuthority.DataRights &&
        this.Id == reservationId &&
        this.LedgerEntryId == ledgerEntryId &&
        this.LastReplayedAtUtc.HasValue &&
        this.MatchesOwnerProof(
            propertyId,
            ownerReceiptContractVersion,
            ownerReceiptId,
            ownerReceiptSha256,
            resultingReservationVersion,
            resultingDetailsRevision,
            originallyCompletedAtUtc);

    private bool MatchesOwnerProof(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingReservationVersion,
        long? resultingDetailsRevision,
        DateTimeOffset originallyCompletedAtUtc) =>
        this.ContractVersion == CurrentContractVersion &&
        this.Revision >= 1 &&
        this.PropertyId == propertyId &&
        this.OwnerReceiptContractVersion ==
            ownerReceiptContractVersion &&
        this.OwnerReceiptId == ownerReceiptId &&
        string.Equals(
            this.OwnerReceiptSha256,
            NormalizeSha256(ownerReceiptSha256),
            StringComparison.Ordinal) &&
        this.ResultingReservationVersion ==
            resultingReservationVersion &&
        this.ResultingDetailsRevision ==
            resultingDetailsRevision &&
        this.CompletedAtUtc ==
            originallyCompletedAtUtc.ToUniversalTime();

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == ReservationAnonymisationReceipt.Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<ReservationAnonymisationTombstone> Invalid() =>
        Result.Failure<ReservationAnonymisationTombstone>(
            ReservationsDomainErrors
                .ReservationAnonymisationTombstoneInvalid);
}

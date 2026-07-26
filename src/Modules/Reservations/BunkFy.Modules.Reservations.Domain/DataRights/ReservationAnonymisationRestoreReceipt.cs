namespace BunkFy.Modules.Reservations.Domain.DataRights;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Reservations.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationAnonymisationRestoreReceipt
    : ScopedEntity<Guid>
{
    public const int CurrentContractVersion = 1;

    private ReservationAnonymisationRestoreReceipt() { }

    private ReservationAnonymisationRestoreReceipt(
        Guid ledgerEntryId,
        string scopeId)
        : base(ledgerEntryId, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid LedgerEntryId { get; private set; }
    public long TenantSequence { get; private set; }
    public string LedgerEntrySha256 { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public Guid ReservationId { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } = string.Empty;
    public long ResultingReservationVersion { get; private set; }
    public long? ResultingDetailsRevision { get; private set; }
    public DateTimeOffset OriginallyCompletedAtUtc { get; private set; }
    public long TombstoneRevision { get; private set; }
    public DateTimeOffset ReplayedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<ReservationAnonymisationRestoreReceipt> Create(
        string tenantId,
        Guid ledgerEntryId,
        long tenantSequence,
        string ledgerEntrySha256,
        Guid propertyId,
        Guid reservationId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingReservationVersion,
        long? resultingDetailsRevision,
        DateTimeOffset originallyCompletedAtUtc,
        long tombstoneRevision,
        DateTimeOffset replayedAtUtc)
    {
        string ledgerSha256 = NormalizeSha256(ledgerEntrySha256);
        string receiptSha256 = NormalizeSha256(ownerReceiptSha256);
        DateTimeOffset completedAtUtc =
            originallyCompletedAtUtc.ToUniversalTime();
        DateTimeOffset restoredAtUtc = replayedAtUtc.ToUniversalTime();
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            ledgerEntryId == Guid.Empty ||
            tenantSequence <= 0 ||
            !IsSha256(ledgerSha256) ||
            propertyId == Guid.Empty ||
            reservationId == Guid.Empty ||
            ownerReceiptContractVersion <= 0 ||
            ownerReceiptId == Guid.Empty ||
            !IsSha256(receiptSha256) ||
            resultingReservationVersion <= 0 ||
            resultingDetailsRevision is <= 0 ||
            completedAtUtc == default ||
            tombstoneRevision <= 0 ||
            restoredAtUtc == default ||
            restoredAtUtc < completedAtUtc)
        {
            return Invalid();
        }

        ReservationAnonymisationRestoreReceipt receipt =
            new(ledgerEntryId, scopeId)
            {
                ContractVersion = CurrentContractVersion,
                LedgerEntryId = ledgerEntryId,
                TenantSequence = tenantSequence,
                LedgerEntrySha256 = ledgerSha256,
                PropertyId = propertyId,
                ReservationId = reservationId,
                OwnerReceiptContractVersion =
                    ownerReceiptContractVersion,
                OwnerReceiptId = ownerReceiptId,
                OwnerReceiptSha256 = receiptSha256,
                ResultingReservationVersion =
                    resultingReservationVersion,
                ResultingDetailsRevision = resultingDetailsRevision,
                OriginallyCompletedAtUtc = completedAtUtc,
                TombstoneRevision = tombstoneRevision,
                ReplayedAtUtc = restoredAtUtc
            };
        receipt.CanonicalSha256 = receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool Matches(
        string tenantId,
        Guid ledgerEntryId,
        long tenantSequence,
        string ledgerEntrySha256,
        Guid propertyId,
        Guid reservationId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingReservationVersion,
        DateTimeOffset originallyCompletedAtUtc) =>
        string.Equals(this.ScopeId, tenantId, StringComparison.Ordinal) &&
        this.ContractVersion == CurrentContractVersion &&
        this.Id == ledgerEntryId &&
        this.LedgerEntryId == ledgerEntryId &&
        this.TenantSequence == tenantSequence &&
        string.Equals(
            this.LedgerEntrySha256,
            NormalizeSha256(ledgerEntrySha256),
            StringComparison.Ordinal) &&
        this.PropertyId == propertyId &&
        this.ReservationId == reservationId &&
        this.OwnerReceiptContractVersion ==
            ownerReceiptContractVersion &&
        this.OwnerReceiptId == ownerReceiptId &&
        string.Equals(
            this.OwnerReceiptSha256,
            NormalizeSha256(ownerReceiptSha256),
            StringComparison.Ordinal) &&
        this.ResultingReservationVersion ==
            resultingReservationVersion &&
        this.OriginallyCompletedAtUtc ==
            originallyCompletedAtUtc.ToUniversalTime() &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(
            canonical,
            this.ContractVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.Id.ToString("N"));
        Append(canonical, this.ScopeId);
        Append(canonical, this.LedgerEntryId.ToString("N"));
        Append(
            canonical,
            this.TenantSequence.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.LedgerEntrySha256);
        Append(canonical, this.PropertyId.ToString("N"));
        Append(canonical, this.ReservationId.ToString("N"));
        Append(
            canonical,
            this.OwnerReceiptContractVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(canonical, this.OwnerReceiptId.ToString("N"));
        Append(canonical, this.OwnerReceiptSha256);
        Append(
            canonical,
            this.ResultingReservationVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(
            canonical,
            this.ResultingDetailsRevision?.ToString(
                CultureInfo.InvariantCulture) ?? "-");
        Append(
            canonical,
            this.OriginallyCompletedAtUtc.ToString(
                "O",
                CultureInfo.InvariantCulture));
        Append(
            canonical,
            this.TombstoneRevision.ToString(
                CultureInfo.InvariantCulture));
        Append(
            canonical,
            this.ReplayedAtUtc.ToString(
                "O",
                CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(
            value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == ReservationAnonymisationReceipt.Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<ReservationAnonymisationRestoreReceipt>
        Invalid() =>
        Result.Failure<ReservationAnonymisationRestoreReceipt>(
            ReservationsDomainErrors
                .ReservationAnonymisationRestoreReceiptInvalid);
}

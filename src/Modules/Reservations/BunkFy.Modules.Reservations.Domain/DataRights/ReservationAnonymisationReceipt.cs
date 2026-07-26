namespace BunkFy.Modules.Reservations.Domain.DataRights;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationAnonymisationReceipt : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;

    private ReservationAnonymisationReceipt() { }

    private ReservationAnonymisationReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public Guid ReservationId { get; private set; }
    public long SelectedReservationVersion { get; private set; }
    public long ResultingReservationVersion { get; private set; }
    public long SelectedDetailsRevision { get; private set; }
    public long ResultingDetailsRevision { get; private set; }
    public ReservationAnonymisationDisposition Disposition { get; private set; }
    public ReservationAnonymisationReason Reason { get; private set; }
    public int RedactedHistoryCount { get; private set; }
    public int RemovedGuestLinkCount { get; private set; }
    public int ReducedExternalOperationCount { get; private set; }
    public int SuppressedReminderCount { get; private set; }
    public string ApprovalEvidenceSha256 { get; private set; } = string.Empty;
    public string PolicyEvidenceSha256 { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<ReservationAnonymisationReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid reservationId,
        ReservationAnonymisationOutcome outcome,
        int redactedHistoryCount,
        int reducedExternalOperationCount,
        int suppressedReminderCount,
        string approvalEvidenceSha256,
        string policyEvidenceSha256)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            propertyId == Guid.Empty ||
            caseId == Guid.Empty ||
            reservationId == Guid.Empty ||
            outcome.EventId == Guid.Empty ||
            outcome.CompletedAtUtc == default ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        string actor = outcome.ActorId.Trim();
        string approvalDigest = NormalizeSha256(approvalEvidenceSha256);
        string policyDigest = NormalizeSha256(policyEvidenceSha256);
        if (approvalRevision < 1 ||
            operationRevision <= approvalRevision ||
            outcome.PreviousVersion < 1 ||
            outcome.CurrentVersion != outcome.PreviousVersion + 1 ||
            outcome.PreviousDetailsRevision < 1 ||
            outcome.CurrentDetailsRevision !=
                outcome.PreviousDetailsRevision + 1 ||
            outcome.RemovedGuestLinkCount < 0 ||
            redactedHistoryCount < 1 ||
            reducedExternalOperationCount < 0 ||
            suppressedReminderCount < 0 ||
            actor.Length is 0 or > Reservation.ActorIdMaxLength ||
            !IsSha256(approvalDigest) ||
            !IsSha256(policyDigest))
        {
            return Invalid();
        }

        ReservationAnonymisationReceipt receipt = new(receiptId, scopeId)
        {
            ContractVersion = CurrentContractVersion,
            IdempotencyKey = idempotencyKey,
            PropertyId = propertyId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            OperationRevision = operationRevision,
            ReservationId = reservationId,
            SelectedReservationVersion = outcome.PreviousVersion,
            ResultingReservationVersion = outcome.CurrentVersion,
            SelectedDetailsRevision = outcome.PreviousDetailsRevision,
            ResultingDetailsRevision = outcome.CurrentDetailsRevision,
            Disposition = ReservationAnonymisationDisposition.Completed,
            Reason =
                ReservationAnonymisationReason.ReservationOwnerDataRedacted,
            RedactedHistoryCount = redactedHistoryCount,
            RemovedGuestLinkCount = outcome.RemovedGuestLinkCount,
            ReducedExternalOperationCount = reducedExternalOperationCount,
            SuppressedReminderCount = suppressedReminderCount,
            ApprovalEvidenceSha256 = approvalDigest,
            PolicyEvidenceSha256 = policyDigest,
            EventId = outcome.EventId,
            ActorId = actor,
            CompletedAtUtc = outcome.CompletedAtUtc
        };
        receipt.CanonicalSha256 = receipt.ComputeCanonicalSha256();
        receipt.RaiseDomainEvent(new ReservationAnonymisedDomainEvent(
            receipt.EventId,
            receipt.CompletedAtUtc,
            scopeId,
            receipt.Id,
            receipt.PropertyId,
            receipt.ReservationId,
            receipt.ResultingReservationVersion,
            receipt.ResultingDetailsRevision));
        return Result.Success(receipt);
    }

    public bool Matches(
        Guid idempotencyKey,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid reservationId,
        long selectedReservationVersion,
        long selectedDetailsRevision,
        string approvalEvidenceSha256,
        string actorId) =>
        this.ContractVersion == CurrentContractVersion &&
        this.IdempotencyKey == idempotencyKey &&
        this.PropertyId == propertyId &&
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.OperationRevision == operationRevision &&
        this.ReservationId == reservationId &&
        this.SelectedReservationVersion == selectedReservationVersion &&
        this.SelectedDetailsRevision == selectedDetailsRevision &&
        string.Equals(
            this.ApprovalEvidenceSha256,
            NormalizeSha256(approvalEvidenceSha256),
            StringComparison.Ordinal) &&
        string.Equals(this.ActorId, actorId?.Trim(), StringComparison.Ordinal) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, this.ContractVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.Id.ToString("N"));
        Append(canonical, this.ScopeId);
        Append(canonical, this.IdempotencyKey.ToString("N"));
        Append(canonical, this.PropertyId.ToString("N"));
        Append(canonical, this.CaseId.ToString("N"));
        Append(canonical, this.ApprovalRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.OperationRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.ReservationId.ToString("N"));
        Append(canonical, this.SelectedReservationVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.ResultingReservationVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.SelectedDetailsRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.ResultingDetailsRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)this.Disposition).ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)this.Reason).ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.RedactedHistoryCount.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.RemovedGuestLinkCount.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.ReducedExternalOperationCount.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.SuppressedReminderCount.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.ApprovalEvidenceSha256);
        Append(canonical, this.PolicyEvidenceSha256);
        Append(canonical, this.EventId.ToString("N"));
        Append(canonical, this.ActorId);
        Append(
            canonical,
            this.CompletedAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<ReservationAnonymisationReceipt> Invalid() =>
        Result.Failure<ReservationAnonymisationReceipt>(
            ReservationsDomainErrors.ReservationAnonymisationReceiptInvalid);
}

public enum ReservationAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum ReservationAnonymisationReason
{
    Unknown = 0,
    ReservationOwnerDataRedacted = 1
}

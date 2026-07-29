namespace BunkFy.Modules.Reservations.Domain.Retention;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationRetentionAnonymisationReceipt
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;
    public const string SystemActorId = "system:retention";

    private ReservationRetentionAnonymisationReceipt() { }

    private ReservationRetentionAnonymisationReceipt(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid ExecutionId { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid ReservationId { get; private set; }
    public long SelectedReservationVersion { get; private set; }
    public long ResultingReservationVersion { get; private set; }
    public long SelectedDetailsRevision { get; private set; }
    public long ResultingDetailsRevision { get; private set; }
    public DateTimeOffset TerminalAtUtc { get; private set; }
    public DateTimeOffset RetentionDeadlineUtc { get; private set; }
    public string PolicyEvidenceSha256 { get; private set; } = string.Empty;
    public int RedactedHistoryCount { get; private set; }
    public int RemovedGuestLinkCount { get; private set; }
    public int ReducedExternalOperationCount { get; private set; }
    public int SuppressedReminderCount { get; private set; }
    public Guid EventId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<ReservationRetentionAnonymisationReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid executionId,
        Guid propertyId,
        Guid reservationId,
        ReservationAnonymisationOutcome outcome,
        DateTimeOffset terminalAtUtc,
        DateTimeOffset retentionDeadlineUtc,
        string policyEvidenceSha256,
        int redactedHistoryCount,
        int reducedExternalOperationCount,
        int suppressedReminderCount)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        string digest = NormalizeSha256(policyEvidenceSha256);
        string actor = outcome.ActorId?.Trim() ?? string.Empty;
        if (receiptId == Guid.Empty ||
            executionId == Guid.Empty ||
            propertyId == Guid.Empty ||
            reservationId == Guid.Empty ||
            outcome.EventId == Guid.Empty ||
            outcome.PreviousVersion < 1 ||
            outcome.CurrentVersion != outcome.PreviousVersion + 1 ||
            outcome.PreviousDetailsRevision < 1 ||
            outcome.CurrentDetailsRevision !=
                outcome.PreviousDetailsRevision + 1 ||
            terminalAtUtc == default ||
            retentionDeadlineUtc < terminalAtUtc ||
            outcome.CompletedAtUtc < retentionDeadlineUtc ||
            redactedHistoryCount < 1 ||
            outcome.RemovedGuestLinkCount < 0 ||
            reducedExternalOperationCount < 0 ||
            suppressedReminderCount < 0 ||
            !string.Equals(
                actor,
                SystemActorId,
                StringComparison.Ordinal) ||
            !IsSha256(digest) ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        ReservationRetentionAnonymisationReceipt receipt =
            new(receiptId, scopeId)
            {
                ContractVersion = CurrentContractVersion,
                ExecutionId = executionId,
                PropertyId = propertyId,
                ReservationId = reservationId,
                SelectedReservationVersion = outcome.PreviousVersion,
                ResultingReservationVersion = outcome.CurrentVersion,
                SelectedDetailsRevision =
                    outcome.PreviousDetailsRevision,
                ResultingDetailsRevision =
                    outcome.CurrentDetailsRevision,
                TerminalAtUtc = terminalAtUtc.ToUniversalTime(),
                RetentionDeadlineUtc =
                    retentionDeadlineUtc.ToUniversalTime(),
                PolicyEvidenceSha256 = digest,
                RedactedHistoryCount = redactedHistoryCount,
                RemovedGuestLinkCount = outcome.RemovedGuestLinkCount,
                ReducedExternalOperationCount =
                    reducedExternalOperationCount,
                SuppressedReminderCount = suppressedReminderCount,
                EventId = outcome.EventId,
                ActorId = actor,
                CompletedAtUtc =
                    outcome.CompletedAtUtc.ToUniversalTime()
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
        Guid executionId,
        Guid reservationId,
        long selectedReservationVersion,
        long selectedDetailsRevision) =>
        this.ContractVersion == CurrentContractVersion &&
        this.ExecutionId == executionId &&
        this.ReservationId == reservationId &&
        this.SelectedReservationVersion == selectedReservationVersion &&
        this.SelectedDetailsRevision == selectedDetailsRevision &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    public bool MatchesOwnerProof(
        int contractVersion,
        Guid receiptId,
        Guid propertyId,
        Guid reservationId,
        long resultingReservationVersion,
        long resultingDetailsRevision,
        string canonicalSha256,
        DateTimeOffset completedAtUtc) =>
        this.ContractVersion == contractVersion &&
        this.Id == receiptId &&
        this.PropertyId == propertyId &&
        this.ReservationId == reservationId &&
        this.ResultingReservationVersion == resultingReservationVersion &&
        this.ResultingDetailsRevision == resultingDetailsRevision &&
        this.CompletedAtUtc == completedAtUtc.ToUniversalTime() &&
        string.Equals(
            this.CanonicalSha256,
            NormalizeSha256(canonicalSha256),
            StringComparison.Ordinal) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, this.ContractVersion);
        Append(canonical, this.Id);
        Append(canonical, this.ScopeId);
        Append(canonical, this.ExecutionId);
        Append(canonical, this.PropertyId);
        Append(canonical, this.ReservationId);
        Append(canonical, this.SelectedReservationVersion);
        Append(canonical, this.ResultingReservationVersion);
        Append(canonical, this.SelectedDetailsRevision);
        Append(canonical, this.ResultingDetailsRevision);
        Append(canonical, this.TerminalAtUtc);
        Append(canonical, this.RetentionDeadlineUtc);
        Append(canonical, this.PolicyEvidenceSha256);
        Append(canonical, this.RedactedHistoryCount);
        Append(canonical, this.RemovedGuestLinkCount);
        Append(canonical, this.ReducedExternalOperationCount);
        Append(canonical, this.SuppressedReminderCount);
        Append(canonical, this.EventId);
        Append(canonical, this.ActorId);
        Append(canonical, this.CompletedAtUtc);
        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder target, object value)
    {
        string text = value switch
        {
            DateTimeOffset timestamp =>
                timestamp.ToUniversalTime().ToString(
                    "O",
                    CultureInfo.InvariantCulture),
            Guid id => id.ToString("N"),
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        target.Append(
            text.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(text);
    }

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<ReservationRetentionAnonymisationReceipt>
        Invalid() =>
        Result.Failure<ReservationRetentionAnonymisationReceipt>(
            ReservationsDomainErrors.RetentionReceiptInvalid);
}

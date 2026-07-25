namespace BunkFy.Modules.Guests.Domain.DataRights;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.Errors;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class GuestAnonymisationReceipt : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;

    private GuestAnonymisationReceipt() { }

    private GuestAnonymisationReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid RoutingPropertyId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public Guid GuestId { get; private set; }
    public long SelectedGuestVersion { get; private set; }
    public long ResultingGuestVersion { get; private set; }
    public GuestAnonymisationDisposition Disposition { get; private set; }
    public GuestAnonymisationReason Reason { get; private set; }
    public int AffectedPropertyCount { get; private set; }
    public string ApprovalEvidenceSha256 { get; private set; } = string.Empty;
    public string PolicySetSha256 { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<GuestAnonymisationReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        Guid routingPropertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid guestId,
        long selectedGuestVersion,
        long resultingGuestVersion,
        int affectedPropertyCount,
        string approvalEvidenceSha256,
        string policySetSha256,
        Guid eventId,
        string actorId,
        DateTimeOffset completedAtUtc)
    {
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            routingPropertyId == Guid.Empty ||
            caseId == Guid.Empty ||
            guestId == Guid.Empty ||
            eventId == Guid.Empty ||
            completedAtUtc == default ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        string actor = actorId?.Trim() ?? string.Empty;
        string approvalDigest = NormalizeSha256(approvalEvidenceSha256);
        string policyDigest = NormalizeSha256(policySetSha256);
        if (approvalRevision < 1 ||
            operationRevision <= approvalRevision ||
            selectedGuestVersion < 1 ||
            resultingGuestVersion != selectedGuestVersion + 1 ||
            affectedPropertyCount < 1 ||
            actor.Length is 0 or > GuestProfile.ActorIdMaxLength ||
            !IsSha256(approvalDigest) ||
            !IsSha256(policyDigest))
        {
            return Invalid();
        }

        GuestAnonymisationReceipt receipt = new(receiptId, scopeId)
        {
            ContractVersion = CurrentContractVersion,
            IdempotencyKey = idempotencyKey,
            RoutingPropertyId = routingPropertyId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            OperationRevision = operationRevision,
            GuestId = guestId,
            SelectedGuestVersion = selectedGuestVersion,
            ResultingGuestVersion = resultingGuestVersion,
            Disposition = GuestAnonymisationDisposition.Completed,
            Reason = GuestAnonymisationReason.ProfileAnonymised,
            AffectedPropertyCount = affectedPropertyCount,
            ApprovalEvidenceSha256 = approvalDigest,
            PolicySetSha256 = policyDigest,
            EventId = eventId,
            ActorId = actor,
            CompletedAtUtc = completedAtUtc
        };
        receipt.CanonicalSha256 = receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool Matches(
        Guid idempotencyKey,
        Guid routingPropertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid guestId,
        long selectedGuestVersion,
        string approvalEvidenceSha256,
        string actorId) =>
        this.ContractVersion == CurrentContractVersion &&
        this.IdempotencyKey == idempotencyKey &&
        this.RoutingPropertyId == routingPropertyId &&
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.OperationRevision == operationRevision &&
        this.GuestId == guestId &&
        this.SelectedGuestVersion == selectedGuestVersion &&
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
        Append(canonical, this.RoutingPropertyId.ToString("N"));
        Append(canonical, this.CaseId.ToString("N"));
        Append(canonical, this.ApprovalRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.OperationRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.GuestId.ToString("N"));
        Append(canonical, this.SelectedGuestVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.ResultingGuestVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)this.Disposition).ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)this.Reason).ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.AffectedPropertyCount.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.ApprovalEvidenceSha256);
        Append(canonical, this.PolicySetSha256);
        Append(canonical, this.EventId.ToString("N"));
        Append(canonical, this.ActorId);
        Append(canonical, this.CompletedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
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

    private static Result<GuestAnonymisationReceipt> Invalid() =>
        Result.Failure<GuestAnonymisationReceipt>(
            GuestsDomainErrors.AnonymisationReceiptInvalid);
}

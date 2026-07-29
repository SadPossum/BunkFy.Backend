namespace BunkFy.Modules.Staff.Domain.DataRights;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffAnonymisationReceipt : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;

    private StaffAnonymisationReceipt() { }

    private StaffAnonymisationReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public long SelectedStaffVersion { get; private set; }
    public long ResultingStaffVersion { get; private set; }
    public long SelectedOperationLockRevision { get; private set; }
    public long ResultingOperationLockRevision { get; private set; }
    public StaffAnonymisationDisposition Disposition { get; private set; }
    public StaffAnonymisationReason Reason { get; private set; }
    public string ApprovalEvidenceSha256 { get; private set; } =
        string.Empty;
    public string StateBindingsSha256 { get; private set; } =
        string.Empty;
    public Guid EventId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<StaffAnonymisationReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid staffMemberId,
        long selectedStaffVersion,
        long resultingStaffVersion,
        long selectedOperationLockRevision,
        long resultingOperationLockRevision,
        string approvalEvidenceSha256,
        string stateBindingsSha256,
        Guid eventId,
        string actorId,
        DateTimeOffset completedAtUtc)
    {
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            caseId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            eventId == Guid.Empty ||
            completedAtUtc == default ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        string actor = actorId?.Trim() ?? string.Empty;
        string approvalDigest = NormalizeSha256(
            approvalEvidenceSha256);
        string stateDigest = NormalizeSha256(stateBindingsSha256);
        if (approvalRevision < 1 ||
            operationRevision <= approvalRevision ||
            selectedStaffVersion < 1 ||
            resultingStaffVersion != selectedStaffVersion + 1 ||
            selectedOperationLockRevision < 1 ||
            resultingOperationLockRevision !=
                selectedOperationLockRevision + 1 ||
            actor.Length is 0 or > StaffMember.ActorIdMaxLength ||
            !IsSha256(approvalDigest) ||
            !IsSha256(stateDigest))
        {
            return Invalid();
        }

        StaffAnonymisationReceipt receipt = new(receiptId, scopeId)
        {
            ContractVersion = CurrentContractVersion,
            IdempotencyKey = idempotencyKey,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            OperationRevision = operationRevision,
            StaffMemberId = staffMemberId,
            SelectedStaffVersion = selectedStaffVersion,
            ResultingStaffVersion = resultingStaffVersion,
            SelectedOperationLockRevision =
                selectedOperationLockRevision,
            ResultingOperationLockRevision =
                resultingOperationLockRevision,
            Disposition = StaffAnonymisationDisposition.Completed,
            Reason = StaffAnonymisationReason.ProfileAnonymised,
            ApprovalEvidenceSha256 = approvalDigest,
            StateBindingsSha256 = stateDigest,
            EventId = eventId,
            ActorId = actor,
            CompletedAtUtc = completedAtUtc.ToUniversalTime()
        };
        receipt.CanonicalSha256 = receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool Matches(
        Guid idempotencyKey,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid staffMemberId,
        long selectedStaffVersion,
        string approvalEvidenceSha256,
        string actorId) =>
        this.ContractVersion == CurrentContractVersion &&
        this.IdempotencyKey == idempotencyKey &&
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.OperationRevision == operationRevision &&
        this.StaffMemberId == staffMemberId &&
        this.SelectedStaffVersion == selectedStaffVersion &&
        string.Equals(
            this.ApprovalEvidenceSha256,
            NormalizeSha256(approvalEvidenceSha256),
            StringComparison.Ordinal) &&
        string.Equals(
            this.ActorId,
            actorId?.Trim(),
            StringComparison.Ordinal) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, this.ContractVersion);
        Append(canonical, this.Id.ToString("N"));
        Append(canonical, this.ScopeId);
        Append(canonical, this.IdempotencyKey.ToString("N"));
        Append(canonical, this.CaseId.ToString("N"));
        Append(canonical, this.ApprovalRevision);
        Append(canonical, this.OperationRevision);
        Append(canonical, this.StaffMemberId.ToString("N"));
        Append(canonical, this.SelectedStaffVersion);
        Append(canonical, this.ResultingStaffVersion);
        Append(canonical, this.SelectedOperationLockRevision);
        Append(canonical, this.ResultingOperationLockRevision);
        Append(canonical, (int)this.Disposition);
        Append(canonical, (int)this.Reason);
        Append(canonical, this.ApprovalEvidenceSha256);
        Append(canonical, this.StateBindingsSha256);
        Append(canonical, this.EventId.ToString("N"));
        Append(canonical, this.ActorId);
        Append(
            canonical,
            this.CompletedAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));
        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(
        StringBuilder target,
        long value) =>
        Append(
            target,
            value.ToString(CultureInfo.InvariantCulture));

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
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<StaffAnonymisationReceipt> Invalid() =>
        Result.Failure<StaffAnonymisationReceipt>(
            StaffDomainErrors.AnonymisationReceiptInvalid);
}

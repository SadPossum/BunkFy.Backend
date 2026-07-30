namespace BunkFy.Modules.Staff.Domain.Retention;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffRetentionAnonymisationReceipt
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;
    public const string SystemActorId = "system:retention";

    private StaffRetentionAnonymisationReceipt() { }

    private StaffRetentionAnonymisationReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid ExecutionId { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public long SelectedStaffVersion { get; private set; }
    public long ResultingStaffVersion { get; private set; }
    public long SelectedOperationLockRevision { get; private set; }
    public long ResultingOperationLockRevision { get; private set; }
    public DateTimeOffset DepartedAtUtc { get; private set; }
    public DateTimeOffset RetentionDeadlineUtc { get; private set; }
    public string PolicyEvidenceSha256 { get; private set; } =
        string.Empty;
    public Guid EventId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<StaffRetentionAnonymisationReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid executionId,
        Guid staffMemberId,
        StaffMemberAnonymisationOutcome outcome,
        long selectedOperationLockRevision,
        long resultingOperationLockRevision,
        DateTimeOffset departedAtUtc,
        DateTimeOffset retentionDeadlineUtc,
        string policyEvidenceSha256)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        string digest =
            policyEvidenceSha256?.Trim().ToLowerInvariant() ??
            string.Empty;
        if (receiptId == Guid.Empty ||
            executionId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            outcome.EventId == Guid.Empty ||
            outcome.PreviousVersion < 1 ||
            outcome.CurrentVersion != outcome.PreviousVersion + 1 ||
            selectedOperationLockRevision < 1 ||
            resultingOperationLockRevision !=
                selectedOperationLockRevision + 1 ||
            departedAtUtc == default ||
            retentionDeadlineUtc < departedAtUtc ||
            outcome.OccurredAtUtc < retentionDeadlineUtc ||
            !IsSha256(digest) ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        StaffRetentionAnonymisationReceipt receipt =
            new(receiptId, scopeId)
            {
                ContractVersion = CurrentContractVersion,
                ExecutionId = executionId,
                StaffMemberId = staffMemberId,
                SelectedStaffVersion = outcome.PreviousVersion,
                ResultingStaffVersion = outcome.CurrentVersion,
                SelectedOperationLockRevision =
                    selectedOperationLockRevision,
                ResultingOperationLockRevision =
                    resultingOperationLockRevision,
                DepartedAtUtc = departedAtUtc.ToUniversalTime(),
                RetentionDeadlineUtc =
                    retentionDeadlineUtc.ToUniversalTime(),
                PolicyEvidenceSha256 = digest,
                EventId = outcome.EventId,
                ActorId = SystemActorId,
                CompletedAtUtc =
                    outcome.OccurredAtUtc.ToUniversalTime()
            };
        receipt.CanonicalSha256 = receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool Matches(
        Guid executionId,
        Guid staffMemberId,
        long selectedStaffVersion) =>
        this.HasValidCanonicalProof() &&
        this.ExecutionId == executionId &&
        this.StaffMemberId == staffMemberId &&
        this.SelectedStaffVersion == selectedStaffVersion;

    public bool HasValidCanonicalProof() =>
        this.ContractVersion == CurrentContractVersion &&
        IsSha256(this.CanonicalSha256) &&
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
        Append(canonical, this.StaffMemberId);
        Append(canonical, this.SelectedStaffVersion);
        Append(canonical, this.ResultingStaffVersion);
        Append(canonical, this.SelectedOperationLockRevision);
        Append(canonical, this.ResultingOperationLockRevision);
        Append(canonical, this.DepartedAtUtc);
        Append(canonical, this.RetentionDeadlineUtc);
        Append(canonical, this.PolicyEvidenceSha256);
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

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<StaffRetentionAnonymisationReceipt> Invalid() =>
        Result.Failure<StaffRetentionAnonymisationReceipt>(
            StaffDomainErrors.RetentionReceiptInvalid);
}

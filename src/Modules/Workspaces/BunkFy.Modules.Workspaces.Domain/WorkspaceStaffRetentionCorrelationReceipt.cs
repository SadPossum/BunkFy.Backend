namespace BunkFy.Modules.Workspaces.Domain;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffRetentionCorrelationReceipt
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;
    public const string PseudonymPrefix = "retained:";

    private WorkspaceStaffRetentionCorrelationReceipt() { }

    private WorkspaceStaffRetentionCorrelationReceipt(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid ExecutionId { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public long SelectedStaffVersion { get; private set; }
    public int OnboardingRecordsScrubbed { get; private set; }
    public int AccessProcessRecordsScrubbed { get; private set; }
    public int AccessPlanRecordsScrubbed { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<WorkspaceStaffRetentionCorrelationReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid executionId,
        Guid staffMemberId,
        long selectedStaffVersion,
        int onboardingRecordsScrubbed,
        int accessProcessRecordsScrubbed,
        int accessPlanRecordsScrubbed,
        DateTimeOffset completedAtUtc)
    {
        if (receiptId == Guid.Empty ||
            executionId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            selectedStaffVersion <= 0 ||
            onboardingRecordsScrubbed < 0 ||
            accessProcessRecordsScrubbed < 0 ||
            accessPlanRecordsScrubbed < 0 ||
            completedAtUtc == default ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        WorkspaceStaffRetentionCorrelationReceipt receipt =
            new(receiptId, scopeId)
            {
                ContractVersion = CurrentContractVersion,
                ExecutionId = executionId,
                StaffMemberId = staffMemberId,
                SelectedStaffVersion = selectedStaffVersion,
                OnboardingRecordsScrubbed =
                    onboardingRecordsScrubbed,
                AccessProcessRecordsScrubbed =
                    accessProcessRecordsScrubbed,
                AccessPlanRecordsScrubbed =
                    accessPlanRecordsScrubbed,
                CompletedAtUtc = completedAtUtc.ToUniversalTime()
            };
        receipt.CanonicalSha256 =
            receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool Matches(
        string tenantId,
        Guid staffMemberId,
        long selectedStaffVersion) =>
        this.HasValidCanonicalProof() &&
        string.Equals(
            this.ScopeId,
            tenantId?.Trim(),
            StringComparison.Ordinal) &&
        this.StaffMemberId == staffMemberId &&
        this.SelectedStaffVersion == selectedStaffVersion;

    public bool HasValidCanonicalProof() =>
        this.ContractVersion == CurrentContractVersion &&
        IsSha256(this.CanonicalSha256) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    public string CreateSubjectPseudonym() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{PseudonymPrefix}{this.Id:N}");

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, this.ContractVersion);
        Append(canonical, this.Id);
        Append(canonical, this.ScopeId);
        Append(canonical, this.ExecutionId);
        Append(canonical, this.StaffMemberId);
        Append(canonical, this.SelectedStaffVersion);
        Append(canonical, this.OnboardingRecordsScrubbed);
        Append(canonical, this.AccessProcessRecordsScrubbed);
        Append(canonical, this.AccessPlanRecordsScrubbed);
        Append(canonical, this.CompletedAtUtc);
        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(
        StringBuilder target,
        object value)
    {
        string text = value switch
        {
            DateTimeOffset timestamp =>
                timestamp.ToUniversalTime().ToString(
                    "O",
                    CultureInfo.InvariantCulture),
            Guid id => id.ToString("N"),
            IFormattable formattable =>
                formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        target.Append(
            text.Length.ToString(
                CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(text);
    }

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<
        WorkspaceStaffRetentionCorrelationReceipt> Invalid() =>
        Result.Failure<
            WorkspaceStaffRetentionCorrelationReceipt>(
                WorkspaceStaffRetentionErrors.ReceiptInvalid);
}

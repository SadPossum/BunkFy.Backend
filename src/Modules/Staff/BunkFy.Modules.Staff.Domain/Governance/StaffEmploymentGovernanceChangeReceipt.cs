namespace BunkFy.Modules.Staff.Domain.Governance;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffEmploymentGovernanceChangeReceipt
    : ScopedAggregateRoot<Guid>
{
    private StaffEmploymentGovernanceChangeReceipt() { }

    private StaffEmploymentGovernanceChangeReceipt(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid IdempotencyKey { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public int GovernanceContractVersion { get; private set; }
    public long SelectedStaffVersion { get; private set; }
    public long PreviousGovernanceVersion { get; private set; }
    public long ResultingGovernanceVersion { get; private set; }
    public string PolicyContentSha256 { get; private set; } = string.Empty;
    public string AcknowledgementsSha256 { get; private set; } =
        string.Empty;
    public string RequestSha256 { get; private set; } = string.Empty;
    public string ReceiptSha256 { get; private set; } = string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static Result<StaffEmploymentGovernanceChangeReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        StaffEmploymentGovernance governance,
        long previousGovernanceVersion,
        string requestSha256,
        string actorId,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(governance);

        string requestDigest =
            requestSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            !string.Equals(
                scopeId,
                governance.ScopeId,
                StringComparison.Ordinal) ||
            governance.StaffMemberId == Guid.Empty ||
            governance.GovernanceContractVersion !=
                StaffEmploymentGovernance.ContractVersion ||
            governance.SelectedStaffVersion < 1 ||
            previousGovernanceVersion < 0 ||
            governance.Version != previousGovernanceVersion + 1 ||
            !IsSha256(requestDigest) ||
            normalizedActor.Length is 0 or > StaffMember.ActorIdMaxLength ||
            !string.Equals(
                normalizedActor,
                governance.ConfiguredBy,
                StringComparison.Ordinal) ||
            completedAtUtc == default ||
            completedAtUtc != governance.ConfiguredAtUtc)
        {
            return Result.Failure<StaffEmploymentGovernanceChangeReceipt>(
                StaffDomainErrors.EmploymentGovernanceReceiptInvalid);
        }

        string acknowledgementDigest = HashAcknowledgements(
            governance.AcceptedAcknowledgements);
        string receiptDigest = HashReceipt(
            idempotencyKey,
            governance,
            previousGovernanceVersion,
            requestDigest,
            acknowledgementDigest,
            normalizedActor,
            completedAtUtc);

        return Result.Success(
            new StaffEmploymentGovernanceChangeReceipt(
                receiptId,
                scopeId)
            {
                IdempotencyKey = idempotencyKey,
                StaffMemberId = governance.StaffMemberId,
                GovernanceContractVersion =
                    governance.GovernanceContractVersion,
                SelectedStaffVersion = governance.SelectedStaffVersion,
                PreviousGovernanceVersion = previousGovernanceVersion,
                ResultingGovernanceVersion = governance.Version,
                PolicyContentSha256 = governance.Binding.ContentSha256,
                AcknowledgementsSha256 = acknowledgementDigest,
                RequestSha256 = requestDigest,
                ReceiptSha256 = receiptDigest,
                ActorId = normalizedActor,
                CompletedAtUtc = completedAtUtc
            });
    }

    public bool MatchesReplay(
        Guid staffMemberId,
        long expectedStaffVersion,
        long expectedGovernanceVersion,
        string requestSha256) =>
        this.StaffMemberId == staffMemberId &&
        this.SelectedStaffVersion == expectedStaffVersion &&
        this.PreviousGovernanceVersion == expectedGovernanceVersion &&
        string.Equals(
            this.RequestSha256,
            requestSha256,
            StringComparison.Ordinal);

    private static string HashAcknowledgements(
        IReadOnlyCollection<StaffEmploymentGovernanceAcknowledgement>
            acknowledgements)
    {
        string canonical = string.Join(
            '\n',
            acknowledgements
                .OrderBy(
                    item => item.AcknowledgementId,
                    StringComparer.Ordinal)
                .ThenBy(item => item.AcknowledgementVersion)
                .Select(item =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{item.AcknowledgementId}:{item.AcknowledgementVersion}")));
        return Hash(canonical);
    }

    private static string HashReceipt(
        Guid idempotencyKey,
        StaffEmploymentGovernance governance,
        long previousGovernanceVersion,
        string requestSha256,
        string acknowledgementsSha256,
        string actorId,
        DateTimeOffset completedAtUtc)
    {
        string canonical = string.Join(
            '\n',
            idempotencyKey.ToString("D"),
            governance.ScopeId,
            governance.StaffMemberId.ToString("D"),
            governance.GovernanceContractVersion.ToString(
                CultureInfo.InvariantCulture),
            governance.SelectedStaffVersion.ToString(
                CultureInfo.InvariantCulture),
            previousGovernanceVersion.ToString(
                CultureInfo.InvariantCulture),
            governance.Version.ToString(CultureInfo.InvariantCulture),
            governance.Binding.ContentSha256,
            acknowledgementsSha256,
            requestSha256,
            actorId,
            completedAtUtc
                .ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture));
        return Hash(canonical);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsSha256(string value) =>
        value.Length ==
            StaffEmploymentGovernanceBinding.ContentSha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}

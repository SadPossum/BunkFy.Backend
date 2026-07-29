namespace BunkFy.Modules.Staff.Domain.DataRights;

using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffAnonymisationTombstone
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;

    private StaffAnonymisationTombstone() { }

    private StaffAnonymisationTombstone(
        Guid staffMemberId,
        string scopeId)
        : base(staffMemberId, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public StaffAnonymisationTombstoneState State { get; private set; }
    public StaffAnonymisationAuthority Authority { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } =
        string.Empty;
    public DateTimeOffset? LastReplayedAtUtc { get; private set; }

    public static Result<StaffAnonymisationTombstone> Create(
        string tenantId,
        Guid staffMemberId,
        DateTimeOffset completedAtUtc,
        string ownerReceiptSha256)
    {
        string receiptDigest =
            ownerReceiptSha256?.Trim().ToLowerInvariant() ??
            string.Empty;
        if (staffMemberId == Guid.Empty ||
            completedAtUtc == default ||
            !IsSha256(receiptDigest) ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        return Result.Success(
            new StaffAnonymisationTombstone(
                staffMemberId,
                scopeId)
            {
                ContractVersion = CurrentContractVersion,
                Revision = 1,
                State = StaffAnonymisationTombstoneState.Anonymised,
                Authority = StaffAnonymisationAuthority.DataRights,
                CompletedAtUtc = completedAtUtc.ToUniversalTime(),
                OwnerReceiptSha256 = receiptDigest
            });
    }

    public bool Matches(StaffAnonymisationReceipt receipt) =>
        receipt is not null &&
        this.ContractVersion == CurrentContractVersion &&
        this.Revision >= 1 &&
        this.State == StaffAnonymisationTombstoneState.Anonymised &&
        this.Authority == StaffAnonymisationAuthority.DataRights &&
        this.Id == receipt.StaffMemberId &&
        this.CompletedAtUtc == receipt.CompletedAtUtc &&
        string.Equals(
            this.OwnerReceiptSha256,
            receipt.CanonicalSha256,
            StringComparison.Ordinal);

    private static bool IsSha256(string value) =>
        value.Length == StaffAnonymisationReceipt.Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<StaffAnonymisationTombstone> Invalid() =>
        Result.Failure<StaffAnonymisationTombstone>(
            StaffDomainErrors.AnonymisationTombstoneInvalid);
}

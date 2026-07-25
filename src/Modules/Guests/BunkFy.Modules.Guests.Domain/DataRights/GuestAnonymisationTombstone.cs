namespace BunkFy.Modules.Guests.Domain.DataRights;

using BunkFy.Modules.Guests.Domain.Errors;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class GuestAnonymisationTombstone : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;

    private GuestAnonymisationTombstone() { }

    private GuestAnonymisationTombstone(Guid guestId, string scopeId)
        : base(guestId, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public GuestAnonymisationTombstoneState State { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } = string.Empty;
    public DateTimeOffset? LastReplayedAtUtc { get; private set; }

    public static Result<GuestAnonymisationTombstone> Create(
        string tenantId,
        Guid guestId,
        DateTimeOffset completedAtUtc,
        string ownerReceiptSha256)
    {
        string receiptDigest = ownerReceiptSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        if (guestId == Guid.Empty ||
            completedAtUtc == default ||
            receiptDigest.Length != GuestAnonymisationReceipt.Sha256Length ||
            receiptDigest.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<GuestAnonymisationTombstone>(
                GuestsDomainErrors.AnonymisationTombstoneInvalid);
        }

        return Result.Success(new GuestAnonymisationTombstone(guestId, scopeId)
        {
            ContractVersion = CurrentContractVersion,
            Revision = 1,
            State = GuestAnonymisationTombstoneState.Anonymised,
            CompletedAtUtc = completedAtUtc,
            OwnerReceiptSha256 = receiptDigest
        });
    }

    public bool Matches(GuestAnonymisationReceipt receipt) =>
        receipt is not null &&
        this.ContractVersion == CurrentContractVersion &&
        this.Revision >= 1 &&
        this.State == GuestAnonymisationTombstoneState.Anonymised &&
        this.Id == receipt.GuestId &&
        this.CompletedAtUtc == receipt.CompletedAtUtc &&
        string.Equals(
            this.OwnerReceiptSha256,
            receipt.CanonicalSha256,
            StringComparison.Ordinal);
}

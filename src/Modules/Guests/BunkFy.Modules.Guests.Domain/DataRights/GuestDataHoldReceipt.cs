namespace BunkFy.Modules.Guests.Domain.DataRights;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.Errors;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class GuestDataHoldReceipt : ScopedAggregateRoot<Guid>
{
    private GuestDataHoldReceipt() { }

    private GuestDataHoldReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid IdempotencyKey { get; private set; }
    public Guid HoldId { get; private set; }
    public GuestDataHoldAction Action { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid GuestId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public long SelectedGuestVersion { get; private set; }
    public long ResultingHoldVersion { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static Result<GuestDataHoldReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        GuestDataHold hold,
        GuestDataHoldAction action,
        long selectedGuestVersion,
        string actorId,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(hold);

        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            !string.Equals(scopeId, hold.ScopeId, StringComparison.Ordinal) ||
            hold.PropertyId == Guid.Empty ||
            hold.GuestId == Guid.Empty)
        {
            return Result.Failure<GuestDataHoldReceipt>(
                GuestsDomainErrors.DataHoldReceiptIdentityInvalid);
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        bool transitionValid = action switch
        {
            GuestDataHoldAction.Place =>
                hold.State == GuestDataHoldState.Active &&
                hold.Version == 1 &&
                completedAtUtc == hold.PlacedAtUtc &&
                string.Equals(normalizedActor, hold.PlacedBy, StringComparison.Ordinal),
            GuestDataHoldAction.Release =>
                hold.State == GuestDataHoldState.Released &&
                hold.Version >= 2 &&
                completedAtUtc == hold.ReleasedAtUtc &&
                string.Equals(normalizedActor, hold.ReleasedBy, StringComparison.Ordinal),
            _ => false
        };
        if (!transitionValid ||
            selectedGuestVersion < 1 ||
            normalizedActor.Length is 0 or > GuestProfile.ActorIdMaxLength ||
            completedAtUtc == default)
        {
            return Result.Failure<GuestDataHoldReceipt>(
                GuestsDomainErrors.DataHoldReceiptTransitionInvalid);
        }

        return Result.Success(new GuestDataHoldReceipt(receiptId, scopeId)
        {
            IdempotencyKey = idempotencyKey,
            HoldId = hold.Id,
            Action = action,
            PropertyId = hold.PropertyId,
            GuestId = hold.GuestId,
            ReasonCode = hold.ReasonCode,
            SelectedGuestVersion = selectedGuestVersion,
            ResultingHoldVersion = hold.Version,
            ActorId = normalizedActor,
            CompletedAtUtc = completedAtUtc
        });
    }
}

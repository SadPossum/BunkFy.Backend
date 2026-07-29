namespace BunkFy.Modules.Staff.Domain.DataRights;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffDataHoldReceipt : ScopedAggregateRoot<Guid>
{
    private StaffDataHoldReceipt() { }

    private StaffDataHoldReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid IdempotencyKey { get; private set; }
    public Guid HoldId { get; private set; }
    public StaffDataHoldAction Action { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public long SelectedStaffVersion { get; private set; }
    public long ResultingHoldVersion { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static Result<StaffDataHoldReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        StaffDataHold hold,
        StaffDataHoldAction action,
        long selectedStaffVersion,
        string actorId,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(hold);

        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            !string.Equals(
                scopeId,
                hold.ScopeId,
                StringComparison.Ordinal) ||
            hold.StaffMemberId == Guid.Empty)
        {
            return Result.Failure<StaffDataHoldReceipt>(
                StaffDomainErrors.DataHoldReceiptIdentityInvalid);
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        bool transitionValid = action switch
        {
            StaffDataHoldAction.Place =>
                hold.State == StaffDataHoldState.Active &&
                hold.Version == 1 &&
                completedAtUtc == hold.PlacedAtUtc &&
                string.Equals(
                    normalizedActor,
                    hold.PlacedBy,
                    StringComparison.Ordinal),
            StaffDataHoldAction.Release =>
                hold.State == StaffDataHoldState.Released &&
                hold.Version == 2 &&
                completedAtUtc == hold.ReleasedAtUtc &&
                string.Equals(
                    normalizedActor,
                    hold.ReleasedBy,
                    StringComparison.Ordinal),
            _ => false
        };
        if (!transitionValid ||
            selectedStaffVersion < 1 ||
            normalizedActor.Length is 0 or > StaffMember.ActorIdMaxLength ||
            completedAtUtc == default)
        {
            return Result.Failure<StaffDataHoldReceipt>(
                StaffDomainErrors.DataHoldReceiptTransitionInvalid);
        }

        return Result.Success(
            new StaffDataHoldReceipt(receiptId, scopeId)
            {
                IdempotencyKey = idempotencyKey,
                HoldId = hold.Id,
                Action = action,
                StaffMemberId = hold.StaffMemberId,
                ReasonCode = hold.ReasonCode,
                SelectedStaffVersion = selectedStaffVersion,
                ResultingHoldVersion = hold.Version,
                ActorId = normalizedActor,
                CompletedAtUtc = completedAtUtc
            });
    }
}

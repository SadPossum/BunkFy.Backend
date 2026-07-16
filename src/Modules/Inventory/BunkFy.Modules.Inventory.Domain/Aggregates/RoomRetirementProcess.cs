namespace BunkFy.Modules.Inventory.Domain.Aggregates;

using BunkFy.Modules.Inventory.Domain.Errors;
using BunkFy.Modules.Inventory.Domain.Events;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed class RoomRetirementProcess : ScopedAggregateRoot<Guid>
{
    public const int ReasonMaxLength = 500;
    public const int ActorIdMaxLength = 200;

    private RoomRetirementProcess() { }

    private RoomRetirementProcess(
        Guid id,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        string reason,
        string requestedBy,
        DateTimeOffset nowUtc)
        : base(id, scopeId)
    {
        this.PropertyId = propertyId;
        this.RoomId = roomId;
        this.Reason = reason;
        this.RequestedBy = requestedBy;
        this.CreatedAtUtc = nowUtc;
    }

    public Guid PropertyId { get; private set; }
    public Guid RoomId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string RequestedBy { get; private set; } = string.Empty;
    public InventoryRetirementProcessState State { get; private set; } = InventoryRetirementProcessState.Draining;
    public int? RejectionReasonCode { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static Result<RoomRetirementProcess> Create(
        Guid id,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        string reason,
        string requestedBy,
        DateTimeOffset nowUtc)
    {
        string normalizedReason = reason?.Trim() ?? string.Empty;
        string normalizedActor = requestedBy?.Trim() ?? string.Empty;
        if (id == Guid.Empty || propertyId == Guid.Empty || roomId == Guid.Empty)
        {
            return Result.Failure<RoomRetirementProcess>(InventoryDomainErrors.RoomRetirementIdentityInvalid);
        }

        if (string.IsNullOrWhiteSpace(scopeId) ||
            normalizedReason.Length is 0 or > ReasonMaxLength ||
            normalizedActor.Length is 0 or > ActorIdMaxLength)
        {
            return Result.Failure<RoomRetirementProcess>(InventoryDomainErrors.RoomRetirementRequestInvalid);
        }

        return Result.Success(new RoomRetirementProcess(
            id,
            scopeId.Trim(),
            propertyId,
            roomId,
            normalizedReason,
            normalizedActor,
            nowUtc));
    }

    public Result RequestFinalization(Guid eventId, DateTimeOffset nowUtc)
    {
        if (this.State == InventoryRetirementProcessState.FinalizationRequested)
        {
            return Result.Success();
        }

        if (this.State is not (InventoryRetirementProcessState.Draining or InventoryRetirementProcessState.Rejected) ||
            eventId == Guid.Empty)
        {
            return Result.Failure(InventoryDomainErrors.RoomRetirementTransitionInvalid);
        }

        this.State = InventoryRetirementProcessState.FinalizationRequested;
        this.RejectionReasonCode = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new RoomRetirementFinalizationRequestedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            this.RoomId));
        return Result.Success();
    }

    public Result MarkFinalized(DateTimeOffset nowUtc)
    {
        if (this.State is InventoryRetirementProcessState.FinalizedAwaitingTopology or InventoryRetirementProcessState.Completed)
        {
            return Result.Success();
        }

        if (this.State != InventoryRetirementProcessState.FinalizationRequested)
        {
            return Result.Failure(InventoryDomainErrors.RoomRetirementTransitionInvalid);
        }

        this.State = InventoryRetirementProcessState.FinalizedAwaitingTopology;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Complete(DateTimeOffset nowUtc)
    {
        if (this.State == InventoryRetirementProcessState.Completed)
        {
            return Result.Success();
        }

        if (this.State is not (InventoryRetirementProcessState.FinalizationRequested or
            InventoryRetirementProcessState.FinalizedAwaitingTopology))
        {
            return Result.Failure(InventoryDomainErrors.RoomRetirementTransitionInvalid);
        }

        this.State = InventoryRetirementProcessState.Completed;
        this.RejectionReasonCode = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.CompletedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Reject(int reasonCode, DateTimeOffset nowUtc)
    {
        if ((this.State == InventoryRetirementProcessState.Rejected && this.RejectionReasonCode == reasonCode) ||
            this.State is InventoryRetirementProcessState.FinalizedAwaitingTopology or InventoryRetirementProcessState.Completed)
        {
            return Result.Success();
        }

        if (this.State != InventoryRetirementProcessState.FinalizationRequested || reasonCode <= 0)
        {
            return Result.Failure(InventoryDomainErrors.RoomRetirementTransitionInvalid);
        }

        this.State = InventoryRetirementProcessState.Rejected;
        this.RejectionReasonCode = reasonCode;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    public static bool IsDrainActive(InventoryRetirementProcessState state) =>
        state is InventoryRetirementProcessState.Draining or
            InventoryRetirementProcessState.FinalizationRequested or
            InventoryRetirementProcessState.FinalizedAwaitingTopology or
            InventoryRetirementProcessState.Rejected;
}

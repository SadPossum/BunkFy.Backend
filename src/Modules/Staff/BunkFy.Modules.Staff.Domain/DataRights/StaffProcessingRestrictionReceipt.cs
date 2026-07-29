namespace BunkFy.Modules.Staff.Domain.DataRights;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Events;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffProcessingRestrictionReceipt : ScopedAggregateRoot<Guid>
{
    private StaffProcessingRestrictionReceipt() { }

    private StaffProcessingRestrictionReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid IdempotencyKey { get; private set; }
    public Guid RestrictionId { get; private set; }
    public StaffProcessingRestrictionAction Action { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long SelectedStaffVersion { get; private set; }
    public long ResultingRestrictionVersion { get; private set; }
    public long ResultingProjectionRevision { get; private set; }
    public bool EffectiveRestricted { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static Result<StaffProcessingRestrictionReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        Guid restrictionId,
        StaffProcessingRestrictionAction action,
        Guid staffMemberId,
        Guid caseId,
        long approvalRevision,
        long selectedStaffVersion,
        int restrictionContractVersion,
        long resultingRestrictionVersion,
        long resultingProjectionRevision,
        bool effectiveRestricted,
        string actorId,
        Guid eventId,
        DateTimeOffset completedAtUtc)
    {
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            restrictionId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            caseId == Guid.Empty ||
            eventId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<StaffProcessingRestrictionReceipt>(
                StaffDomainErrors.RestrictionReceiptIdentityInvalid);
        }

        bool restrictionVersionValid = action switch
        {
            StaffProcessingRestrictionAction.Apply =>
                resultingRestrictionVersion == 1,
            StaffProcessingRestrictionAction.Release =>
                resultingRestrictionVersion >= 2,
            _ => false
        };
        if (approvalRevision < 1 ||
            selectedStaffVersion < 1 ||
            restrictionContractVersion < 1 ||
            resultingProjectionRevision < 1 ||
            !restrictionVersionValid)
        {
            return Result.Failure<StaffProcessingRestrictionReceipt>(
                StaffDomainErrors.RestrictionReceiptVersionInvalid);
        }

        string? normalizedActorId = actorId?.Trim();
        if (normalizedActorId is not { Length: > 0 } ||
            normalizedActorId.Length > StaffMember.ActorIdMaxLength ||
            completedAtUtc == default ||
            (action == StaffProcessingRestrictionAction.Apply &&
                !effectiveRestricted))
        {
            return Result.Failure<StaffProcessingRestrictionReceipt>(
                StaffDomainErrors.RestrictionReceiptTransitionInvalid);
        }

        StaffProcessingRestrictionReceipt receipt = new(receiptId, scopeId)
        {
            IdempotencyKey = idempotencyKey,
            RestrictionId = restrictionId,
            Action = action,
            StaffMemberId = staffMemberId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            SelectedStaffVersion = selectedStaffVersion,
            ResultingRestrictionVersion = resultingRestrictionVersion,
            ResultingProjectionRevision = resultingProjectionRevision,
            EffectiveRestricted = effectiveRestricted,
            ActorId = normalizedActorId,
            EventId = eventId,
            CompletedAtUtc = completedAtUtc
        };
        receipt.RaiseDomainEvent(
            new StaffProcessingRestrictionChangedDomainEvent(
                eventId,
                completedAtUtc,
                scopeId,
                staffMemberId,
                restrictionContractVersion,
                resultingProjectionRevision,
                effectiveRestricted));
        return Result.Success(receipt);
    }
}

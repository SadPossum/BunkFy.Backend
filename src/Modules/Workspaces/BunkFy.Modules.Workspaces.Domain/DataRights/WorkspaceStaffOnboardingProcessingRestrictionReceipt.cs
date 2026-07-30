namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using BunkFy.Modules.Workspaces.Domain.Events;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffOnboardingProcessingRestrictionReceipt
    : ScopedAggregateRoot<Guid>
{
    private WorkspaceStaffOnboardingProcessingRestrictionReceipt() { }

    private WorkspaceStaffOnboardingProcessingRestrictionReceipt(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid IdempotencyKey { get; private set; }
    public Guid RestrictionId { get; private set; }
    public WorkspaceStaffOnboardingProcessingRestrictionAction Action
    {
        get;
        private set;
    }
    public Guid ApplicationId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long SelectedOnboardingVersion { get; private set; }
    public long ResultingRestrictionVersion { get; private set; }
    public long ResultingProjectionRevision { get; private set; }
    public bool EffectiveRestricted { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static Result<
        WorkspaceStaffOnboardingProcessingRestrictionReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        Guid restrictionId,
        WorkspaceStaffOnboardingProcessingRestrictionAction action,
        Guid applicationId,
        Guid caseId,
        long approvalRevision,
        long selectedOnboardingVersion,
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
            applicationId == Guid.Empty ||
            caseId == Guid.Empty ||
            eventId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<
                WorkspaceStaffOnboardingProcessingRestrictionReceipt>(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ReceiptIdentityInvalid);
        }

        bool restrictionVersionValid = action switch
        {
            WorkspaceStaffOnboardingProcessingRestrictionAction.Apply =>
                resultingRestrictionVersion == 1,
            WorkspaceStaffOnboardingProcessingRestrictionAction.Release =>
                resultingRestrictionVersion >= 2,
            _ => false
        };
        if (approvalRevision < 1 ||
            selectedOnboardingVersion < 1 ||
            restrictionContractVersion < 1 ||
            resultingProjectionRevision < 1 ||
            !restrictionVersionValid)
        {
            return Result.Failure<
                WorkspaceStaffOnboardingProcessingRestrictionReceipt>(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ReceiptVersionInvalid);
        }

        string actor = actorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or >
                WorkspaceStaffOnboardingRules.ActorIdMaxLength ||
            completedAtUtc == default ||
            (action ==
                WorkspaceStaffOnboardingProcessingRestrictionAction.Apply &&
                !effectiveRestricted))
        {
            return Result.Failure<
                WorkspaceStaffOnboardingProcessingRestrictionReceipt>(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ReceiptTransitionInvalid);
        }

        WorkspaceStaffOnboardingProcessingRestrictionReceipt receipt = new(
            receiptId,
            scopeId)
        {
            IdempotencyKey = idempotencyKey,
            RestrictionId = restrictionId,
            Action = action,
            ApplicationId = applicationId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            SelectedOnboardingVersion = selectedOnboardingVersion,
            ResultingRestrictionVersion = resultingRestrictionVersion,
            ResultingProjectionRevision = resultingProjectionRevision,
            EffectiveRestricted = effectiveRestricted,
            ActorId = actor,
            EventId = eventId,
            CompletedAtUtc = completedAtUtc
        };
        receipt.RaiseDomainEvent(
            new WorkspaceStaffOnboardingProcessingRestrictionChangedDomainEvent(
                eventId,
                completedAtUtc,
                scopeId,
                applicationId,
                restrictionContractVersion,
                resultingProjectionRevision,
                effectiveRestricted));
        return Result.Success(receipt);
    }
}

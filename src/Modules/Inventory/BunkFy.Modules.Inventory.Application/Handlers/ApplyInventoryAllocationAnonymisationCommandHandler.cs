namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Mapping;
using BunkFy.Modules.Inventory.Application.Policies;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class
    ApplyInventoryAllocationAnonymisationCommandHandler(
        IInventoryAllocationAnonymisationRepository anonymisation,
        IInventoryAllocationOperationLock operationLock,
        IDataRightsOperationApprovalGate approvalGate,
        IScopeContext scopeContext,
        ISystemClock clock,
        IIdGenerator ids)
    : ICommandHandler<
        ApplyInventoryAllocationAnonymisationCommand,
        InventoryAllocationAnonymisationReceiptDto>
{
    internal const string ActiveBlocker = "AllocationActive";
    internal const string VersionBlocker = "AllocationVersionChanged";
    internal const string AnonymisedBlocker =
        "AllocationAlreadyAnonymised";

    public async Task<Result<
        InventoryAllocationAnonymisationReceiptDto>> HandleAsync(
            ApplyInventoryAllocationAnonymisationCommand command,
            CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationContributionRequest request =
            command.Request;
        string? actorId = NormalizeActor(request.ExecutingActorId);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    InventoryApplicationErrors.TenantRequired);
        }

        if (!IsValid(
                request,
                tenantId,
                actorId,
                clock.UtcNow))
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    InventoryApplicationErrors
                        .AnonymisationRequestInvalid);
        }

        string approvalEvidenceSha256 =
            InventoryAnonymisationPolicyEvidence.ComputeSha256(
                request.RoutingPolicy);
        InventoryAllocationAnonymisationReceipt? existing =
            await anonymisation.FindReceiptByIdempotencyKeyAsync(
                request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ReplayAsync(
                existing,
                request,
                approvalEvidenceSha256,
                actorId!,
                cancellationToken).ConfigureAwait(false);
        }

        DataRightsOperationApprovalResult approval =
            await approvalGate.EvaluateAsync(
                new(
                    tenantId,
                    request.RoutingPropertyId,
                    request.CaseId,
                    request.ApprovalRevision,
                    DataRightsOperation.Anonymisation,
                    InventoryDataRightsCoordinates.Owner,
                    InventoryDataRightsCoordinates
                        .AllocationRecordType,
                    request.Coordinate.RecordId,
                    request.Coordinate.RecordVersion,
                    ExecutingActorId: actorId),
                cancellationToken).ConfigureAwait(false);
        if (!approval.IsApproved ||
            approval.ApprovalEvidence is null ||
            !InventoryAnonymisationPolicyEvidence.MatchesApproval(
                request.RoutingPolicy,
                approval.ApprovalEvidence))
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    InventoryApplicationErrors
                        .DataRightsApprovalRequired);
        }

        await operationLock.AcquireAsync(
            tenantId,
            request.Coordinate.RecordId,
            cancellationToken).ConfigureAwait(false);

        existing =
            await anonymisation.FindReceiptByIdempotencyKeyAsync(
                request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ReplayAsync(
                existing,
                request,
                approvalEvidenceSha256,
                actorId!,
                cancellationToken).ConfigureAwait(false);
        }

        InventoryAllocation? allocation =
            await anonymisation.GetAllocationAsync(
                request.RoutingPropertyId,
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (allocation is null)
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    InventoryApplicationErrors.AllocationNotFound);
        }

        if (allocation.Version != request.Coordinate.RecordVersion)
        {
            return Blocked(VersionBlocker);
        }

        if (allocation.IsAnonymised)
        {
            return Blocked(AnonymisedBlocker);
        }

        if (allocation.Status is not (
                InventoryAllocationState.Released or
                InventoryAllocationState.Rejected))
        {
            return Blocked(ActiveBlocker);
        }

        Guid receiptId = ids.NewId();
        Guid reservationPseudonym =
            InventoryAnonymisationIdentity
                .CreateReservationPseudonym(receiptId);
        Result<InventoryAllocationAnonymisationOutcome> mutated =
            allocation.Anonymise(
                request.Coordinate.RecordVersion,
                reservationPseudonym,
                ToPersistencePrecision(clock.UtcNow));
        if (mutated.IsFailure)
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    mutated.Error);
        }

        int removedAmendmentDecisions =
            await anonymisation.RemoveAmendmentDecisionsAsync(
                request.RoutingPropertyId,
                allocation.Id,
                cancellationToken).ConfigureAwait(false);
        Result<InventoryAllocationAnonymisationReceipt> receipt =
            InventoryAllocationAnonymisationReceipt.Create(
                receiptId,
                tenantId,
                request.WorkItemId,
                request.IdempotencyKey,
                request.RoutingPropertyId,
                request.CaseId,
                request.ApprovalRevision,
                request.OperationRevision,
                allocation.Id,
                mutated.Value,
                removedAmendmentDecisions,
                approvalEvidenceSha256,
                actorId!);
        if (receipt.IsFailure)
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    receipt.Error);
        }

        Result<InventoryAllocationAnonymisationTombstone> tombstone =
            InventoryAllocationAnonymisationTombstone.Create(
                receipt.Value);
        if (tombstone.IsFailure)
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    tombstone.Error);
        }

        await anonymisation.AddOwnerProofAsync(
            receipt.Value,
            tombstone.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private async Task<Result<
        InventoryAllocationAnonymisationReceiptDto>> ReplayAsync(
            InventoryAllocationAnonymisationReceipt receipt,
            DataRightsAnonymisationContributionRequest request,
            string approvalEvidenceSha256,
            string actorId,
            CancellationToken cancellationToken)
    {
        if (!receipt.MatchesExecution(
                request.WorkItemId,
                request.IdempotencyKey,
                request.RoutingPropertyId,
                request.CaseId,
                request.ApprovalRevision,
                request.OperationRevision,
                request.Coordinate.RecordId,
                request.Coordinate.RecordVersion,
                approvalEvidenceSha256,
                actorId))
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    InventoryApplicationErrors
                        .AnonymisationIdempotencyConflict);
        }

        InventoryAllocation? allocation =
            await anonymisation.GetAllocationAsync(
                request.RoutingPropertyId,
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (allocation is null ||
            !allocation.MatchesAnonymisedState(
                receipt.ResultingAllocationVersion,
                receipt.ResultingReservationPseudonym,
                receipt.CompletedAtUtc) ||
            !await anonymisation.VerifyOwnerStateAsync(
                receipt,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<
                InventoryAllocationAnonymisationReceiptDto>(
                    InventoryApplicationErrors
                        .AnonymisationProofUnavailable);
        }

        return Result.Success(receipt.ToDto());
    }

    private static bool IsValid(
        DataRightsAnonymisationContributionRequest? request,
        string tenantId,
        string? actorId,
        DateTimeOffset nowUtc) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationContract.CurrentVersion &&
        string.Equals(
            request.TenantId,
            tenantId,
            StringComparison.Ordinal) &&
        request.WorkItemId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        request.RoutingPropertyId != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        request.Coordinate is not null &&
        string.Equals(
            request.Coordinate.OwnerKey,
            InventoryDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            InventoryDataRightsCoordinates.AllocationRecordType,
            StringComparison.Ordinal) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        request.RoutingPolicy is not null &&
        request.RoutingPolicy.PropertyId ==
            request.RoutingPropertyId &&
        InventoryAnonymisationPolicyEvidence.IsValid(
            request.RoutingPolicy) &&
        actorId is not null &&
        request.DeadlineUtc > nowUtc;

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <=
                InventoryAllocationAnonymisationReceipt
                    .ActorIdMaxLength
            ? normalized
            : null;
    }

    private static Result<
        InventoryAllocationAnonymisationReceiptDto> Blocked(
            string reasonCode) =>
        Result.Failure<
            InventoryAllocationAnonymisationReceiptDto>(
                InventoryApplicationErrors.AnonymisationBlocked(
                    reasonCode));

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        DateTimeOffset utc = value.ToUniversalTime();
        return new(
            utc.Ticks - (utc.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }
}

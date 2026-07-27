namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Policies;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class
    RestoreInventoryAllocationAnonymisationCommandHandler(
        IInventoryAllocationAnonymisationRestoreRepository
            restoreRepository,
        IInventoryAllocationOperationLock operationLock,
        IScopeContext scopeContext,
        ISystemClock clock)
    : ICommandHandler<
        RestoreInventoryAllocationAnonymisationCommand,
        InventoryAllocationAnonymisationRestoreReceipt>
{
    public async Task<Result<
        InventoryAllocationAnonymisationRestoreReceipt>> HandleAsync(
            RestoreInventoryAllocationAnonymisationCommand command,
            CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationRestoreRequest request =
            command.Request;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<
                InventoryAllocationAnonymisationRestoreReceipt>(
                    InventoryApplicationErrors.TenantRequired);
        }

        if (!IsValid(request, tenantId))
        {
            return Result.Failure<
                InventoryAllocationAnonymisationRestoreReceipt>(
                    InventoryApplicationErrors
                        .AnonymisationRestoreRequestInvalid);
        }

        await operationLock.AcquireAsync(
            tenantId,
            request.RecordId,
            cancellationToken).ConfigureAwait(false);

        InventoryAllocationAnonymisationRestoreReceipt? existing =
            await restoreRepository.GetRestoreReceiptAsync(
                request.LedgerEntryId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ReplayAsync(
                existing,
                request,
                cancellationToken).ConfigureAwait(false);
        }

        Guid reservationPseudonym =
            InventoryAnonymisationIdentity
                .CreateReservationPseudonym(
                    request.OwnerReceiptId);
        InventoryAllocationAnonymisationReceipt? originalReceipt =
            await restoreRepository.GetOriginalReceiptAsync(
                request.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
        if (originalReceipt is not null &&
            (!MatchesOriginalReceipt(originalReceipt, request) ||
             originalReceipt.ResultingReservationPseudonym !=
                reservationPseudonym))
        {
            return Conflict();
        }

        InventoryAllocation? allocation =
            await restoreRepository.GetAllocationAsync(
                request.RoutingPropertyId,
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        bool allocationPresent = allocation is not null;
        if (allocation is null)
        {
            if (originalReceipt is not null)
            {
                return Conflict();
            }
        }
        else if (allocation.IsAnonymised)
        {
            if (!allocation.MatchesAnonymisedState(
                    request.ResultingRecordVersion!.Value,
                    reservationPseudonym,
                    request.OriginallyCompletedAtUtc))
            {
                return Conflict();
            }
        }
        else
        {
            Result<InventoryAllocationAnonymisationOutcome> restored =
                allocation.RestoreAnonymisation(
                    request.ResultingRecordVersion!.Value,
                    reservationPseudonym,
                    request.OriginallyCompletedAtUtc);
            if (restored.IsFailure)
            {
                return Conflict();
            }
        }

        await restoreRepository.RemoveAmendmentDecisionsAsync(
            request.RoutingPropertyId,
            request.RecordId,
            cancellationToken).ConfigureAwait(false);

        DateTimeOffset replayedAtUtc =
            ToPersistencePrecision(clock.UtcNow);
        InventoryAllocationAnonymisationTombstone? tombstone =
            await restoreRepository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        InventoryAllocationAnonymisationTombstone? newTombstone =
            null;
        if (tombstone is null)
        {
            Result<InventoryAllocationAnonymisationTombstone>
                restoredTombstone =
                    InventoryAllocationAnonymisationTombstone.Restore(
                        tenantId,
                        request.RecordId,
                        request.RoutingPropertyId,
                        request.OwnerReceiptContractVersion,
                        request.OwnerReceiptId,
                        request.OwnerReceiptSha256,
                        request.ResultingRecordVersion!.Value,
                        reservationPseudonym,
                        allocationPresent,
                        request.OriginallyCompletedAtUtc,
                        request.LedgerEntryId,
                        replayedAtUtc);
            if (restoredTombstone.IsFailure)
            {
                return Result.Failure<
                    InventoryAllocationAnonymisationRestoreReceipt>(
                        restoredTombstone.Error);
            }

            tombstone = restoredTombstone.Value;
            newTombstone = tombstone;
        }
        else
        {
            Result attached = tombstone.AttachRestoreProof(
                request.RoutingPropertyId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingRecordVersion!.Value,
                reservationPseudonym,
                allocationPresent,
                request.OriginallyCompletedAtUtc,
                request.LedgerEntryId,
                replayedAtUtc);
            if (attached.IsFailure)
            {
                return Result.Failure<
                    InventoryAllocationAnonymisationRestoreReceipt>(
                        attached.Error);
            }
        }

        Result<InventoryAllocationAnonymisationRestoreReceipt>
            receipt =
                InventoryAllocationAnonymisationRestoreReceipt.Create(
                    tenantId,
                    request.LedgerEntryId,
                    request.TenantSequence,
                    request.LedgerEntrySha256,
                    request.RoutingPropertyId,
                    request.RecordId,
                    request.OwnerReceiptContractVersion,
                    request.OwnerReceiptId,
                    request.OwnerReceiptSha256,
                    request.ResultingRecordVersion!.Value,
                    reservationPseudonym,
                    allocationPresent,
                    request.OriginallyCompletedAtUtc,
                    tombstone.Revision,
                    tombstone.LastReplayedAtUtc!.Value);
        if (receipt.IsFailure)
        {
            return receipt;
        }

        await restoreRepository.AddRestoreProofAsync(
            receipt.Value,
            newTombstone,
            cancellationToken).ConfigureAwait(false);
        return receipt;
    }

    private async Task<Result<
        InventoryAllocationAnonymisationRestoreReceipt>> ReplayAsync(
            InventoryAllocationAnonymisationRestoreReceipt receipt,
            DataRightsAnonymisationRestoreRequest request,
            CancellationToken cancellationToken)
    {
        if (!receipt.Matches(
                request.TenantId,
                request.LedgerEntryId,
                request.TenantSequence,
                request.LedgerEntrySha256,
                request.RoutingPropertyId,
                request.RecordId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingRecordVersion!.Value,
                request.OriginallyCompletedAtUtc))
        {
            return Conflict();
        }

        InventoryAllocationAnonymisationTombstone? tombstone =
            await restoreRepository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (tombstone is null ||
            tombstone.Revision != receipt.TombstoneRevision ||
            !tombstone.MatchesRestore(
                request.RecordId,
                request.RoutingPropertyId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingRecordVersion!.Value,
                receipt.ResultingReservationPseudonym,
                receipt.AllocationPresent,
                request.OriginallyCompletedAtUtc,
                request.LedgerEntryId))
        {
            return Conflict();
        }

        InventoryAllocationAnonymisationReceipt? originalReceipt =
            await restoreRepository.GetOriginalReceiptAsync(
                request.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
        if (originalReceipt is not null &&
            (!MatchesOriginalReceipt(originalReceipt, request) ||
             originalReceipt.ResultingReservationPseudonym !=
                receipt.ResultingReservationPseudonym))
        {
            return Conflict();
        }

        if (!await restoreRepository.VerifyRestoredOwnerStateAsync(
                request.RoutingPropertyId,
                request.RecordId,
                receipt.ResultingReservationPseudonym,
                receipt.AllocationPresent,
                receipt.ResultingAllocationVersion,
                request.OriginallyCompletedAtUtc,
                cancellationToken).ConfigureAwait(false))
        {
            return Conflict();
        }

        return Result.Success(receipt);
    }

    private static bool MatchesOriginalReceipt(
        InventoryAllocationAnonymisationReceipt receipt,
        DataRightsAnonymisationRestoreRequest request) =>
        receipt.MatchesOwnerProof(
            request.OwnerReceiptContractVersion,
            request.OwnerReceiptId,
            request.RoutingPropertyId,
            request.RecordId,
            request.ResultingRecordVersion!.Value,
            request.OwnerReceiptSha256,
            request.OriginallyCompletedAtUtc);

    private static bool IsValid(
        DataRightsAnonymisationRestoreRequest? request,
        string tenantId) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationRestoreContract.CurrentVersion &&
        string.Equals(
            request.TenantId,
            tenantId,
            StringComparison.Ordinal) &&
        request.LedgerEntryId != Guid.Empty &&
        request.TenantSequence > 0 &&
        IsSha256(request.LedgerEntrySha256) &&
        request.RoutingPropertyId != Guid.Empty &&
        string.Equals(
            request.OwnerKey,
            InventoryDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            InventoryDataRightsCoordinates.AllocationRecordType,
            StringComparison.Ordinal) &&
        request.RecordId != Guid.Empty &&
        request.OwnerReceiptContractVersion > 0 &&
        request.OwnerReceiptId != Guid.Empty &&
        IsSha256(request.OwnerReceiptSha256) &&
        request.ResultingRecordVersion > 0 &&
        request.OriginallyCompletedAtUtc != default &&
        request.OriginallyCompletedAtUtc.Offset == TimeSpan.Zero;

    private static bool IsSha256(string? value) =>
        value is
        {
            Length:
                InventoryAllocationAnonymisationReceipt.Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<
        InventoryAllocationAnonymisationRestoreReceipt> Conflict() =>
        Result.Failure<
            InventoryAllocationAnonymisationRestoreReceipt>(
                InventoryApplicationErrors
                    .AnonymisationRestoreProofConflict);

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

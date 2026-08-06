namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class RestoreReservationAnonymisationCommandHandler(
    IReservationAnonymisationRestoreRepository restoreRepository,
    IReservationOperationLock operationLock,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        RestoreReservationAnonymisationCommand,
        ReservationAnonymisationRestoreReceipt>
{
    internal const string RestoreActorId = "data-rights-restore";

    public async Task<Result<ReservationAnonymisationRestoreReceipt>>
        HandleAsync(
            RestoreReservationAnonymisationCommand command,
            CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationRestoreRequest? request =
            command.Request;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<
                ReservationAnonymisationRestoreReceipt>(
                    ReservationsApplicationErrors.TenantRequired);
        }

        if (!IsValid(request, tenantId))
        {
            return Result.Failure<
                ReservationAnonymisationRestoreReceipt>(
                    ReservationsApplicationErrors
                        .AnonymisationRestoreRequestInvalid);
        }

        await operationLock.AcquireCoordinateAsync(
            tenantId,
            request.RecordId,
            cancellationToken).ConfigureAwait(false);

        ReservationAnonymisationRestoreReceipt? existing =
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

        Reservation? reservation =
            await restoreRepository.GetReservationAsync(
                request.RoutingPropertyId,
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        ReservationAnonymisationReceipt? originalReceipt =
            await restoreRepository.GetOriginalReceiptAsync(
                request.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
        if (originalReceipt is not null &&
            !MatchesOriginalReceipt(originalReceipt, request))
        {
            return Conflict();
        }

        DateTimeOffset replayedAtUtc =
            ToPersistencePrecision(clock.UtcNow);
        long? resultingDetailsRevision;
        if (reservation is null)
        {
            if (originalReceipt is not null)
            {
                return Conflict();
            }

            resultingDetailsRevision = null;
        }
        else if (reservation.IsAnonymised)
        {
            if (!reservation.MatchesAnonymisedState(
                    request.ResultingRecordVersion!.Value,
                    request.OriginallyCompletedAtUtc))
            {
                return Conflict();
            }

            resultingDetailsRevision = reservation.DetailsRevision;
            await restoreRepository.RedactRestoredOwnedRecordsAsync(
                reservation,
                outcome: null,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Result<ReservationAnonymisationRestoreOutcome> restored =
                reservation.RestoreAnonymisation(
                    request.ResultingRecordVersion!.Value,
                    RestoreActorId,
                    ids.NewId(),
                    request.OriginallyCompletedAtUtc,
                    replayedAtUtc);
            if (restored.IsFailure)
            {
                return Conflict();
            }

            resultingDetailsRevision =
                restored.Value.CurrentDetailsRevision;
            await restoreRepository.RedactRestoredOwnedRecordsAsync(
                reservation,
                restored.Value,
                cancellationToken).ConfigureAwait(false);
        }

        if (originalReceipt is not null &&
            originalReceipt.ResultingDetailsRevision !=
                resultingDetailsRevision)
        {
            return Conflict();
        }

        ReservationAnonymisationTombstone? tombstone =
            await restoreRepository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        ReservationAnonymisationTombstone? newTombstone = null;
        if (tombstone is null)
        {
            Result<ReservationAnonymisationTombstone> restored =
                ReservationAnonymisationTombstone.Restore(
                    tenantId,
                    request.RecordId,
                    request.RoutingPropertyId,
                    request.OwnerReceiptContractVersion,
                    request.OwnerReceiptId,
                    request.OwnerReceiptSha256,
                    request.ResultingRecordVersion!.Value,
                    resultingDetailsRevision,
                    request.OriginallyCompletedAtUtc,
                    request.LedgerEntryId,
                    replayedAtUtc);
            if (restored.IsFailure)
            {
                return Result.Failure<
                    ReservationAnonymisationRestoreReceipt>(
                        restored.Error);
            }

            tombstone = restored.Value;
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
                resultingDetailsRevision,
                request.OriginallyCompletedAtUtc,
                request.LedgerEntryId,
                replayedAtUtc);
            if (attached.IsFailure)
            {
                return Result.Failure<
                    ReservationAnonymisationRestoreReceipt>(
                        attached.Error);
            }
        }

        Result<ReservationAnonymisationRestoreReceipt> receipt =
            ReservationAnonymisationRestoreReceipt.Create(
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
                resultingDetailsRevision,
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

    private async Task<Result<ReservationAnonymisationRestoreReceipt>>
        ReplayAsync(
            ReservationAnonymisationRestoreReceipt receipt,
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

        ReservationAnonymisationTombstone? tombstone =
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
                receipt.ResultingDetailsRevision,
                request.OriginallyCompletedAtUtc,
                request.LedgerEntryId))
        {
            return Conflict();
        }

        ReservationAnonymisationReceipt? originalReceipt =
            await restoreRepository.GetOriginalReceiptAsync(
                request.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
        if (originalReceipt is not null &&
            (!MatchesOriginalReceipt(originalReceipt, request) ||
             originalReceipt.ResultingDetailsRevision !=
                receipt.ResultingDetailsRevision))
        {
            return Conflict();
        }

        Reservation? reservation =
            await restoreRepository.GetReservationAsync(
                request.RoutingPropertyId,
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        bool reservationMatches = reservation is null
            ? receipt.ResultingDetailsRevision is null
            : receipt.ResultingDetailsRevision.HasValue &&
              reservation.MatchesAnonymisedState(
                  receipt.ResultingReservationVersion,
                  receipt.ResultingDetailsRevision.Value,
                  request.OriginallyCompletedAtUtc);
        if (!reservationMatches ||
            !await restoreRepository.VerifyRestoredOwnerStateAsync(
                request.RoutingPropertyId,
                request.RecordId,
                cancellationToken).ConfigureAwait(false))
        {
            return Conflict();
        }

        return Result.Success(receipt);
    }

    private static bool MatchesOriginalReceipt(
        ReservationAnonymisationReceipt receipt,
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
            ReservationsDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            ReservationsDataRightsCoordinates.ReservationRecordType,
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
            Length: ReservationAnonymisationReceipt.Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<ReservationAnonymisationRestoreReceipt>
        Conflict() =>
        Result.Failure<ReservationAnonymisationRestoreReceipt>(
            ReservationsApplicationErrors
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

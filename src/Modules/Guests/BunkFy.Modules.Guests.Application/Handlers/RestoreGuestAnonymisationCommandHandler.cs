namespace BunkFy.Modules.Guests.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class RestoreGuestAnonymisationCommandHandler(
    IGuestAnonymisationRestoreRepository restoreRepository,
    IGuestAnonymisationExecutionBoundary executionBoundary,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        RestoreGuestAnonymisationCommand,
        GuestAnonymisationRestoreReceipt>
{
    internal const string RestoreActorId = "data-rights-restore";

    public async Task<Result<GuestAnonymisationRestoreReceipt>> HandleAsync(
        RestoreGuestAnonymisationCommand command,
        CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationRestoreRequest? request =
            command.Request;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<GuestAnonymisationRestoreReceipt>(
                GuestsApplicationErrors.TenantRequired);
        }

        if (!IsValid(request, tenantId))
        {
            return Result.Failure<GuestAnonymisationRestoreReceipt>(
                GuestsApplicationErrors.AnonymisationRestoreRequestInvalid);
        }

        await executionBoundary.AcquireAsync(
            tenantId,
            request.RecordId,
            cancellationToken).ConfigureAwait(false);

        GuestAnonymisationRestoreReceipt? existing =
            await restoreRepository.GetReceiptAsync(
                request.LedgerEntryId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ReplayAsync(
                existing,
                request,
                cancellationToken).ConfigureAwait(false);
        }

        GuestProfile? profile =
            await restoreRepository.GetProfileAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        DateTimeOffset replayedAtUtc =
            ToPersistencePrecision(clock.UtcNow);
        GuestProfile? newProfile = null;
        if (profile is null)
        {
            Result<GuestProfile> restored =
                GuestProfile.RestoreMissingAnonymised(
                    request.RecordId,
                    tenantId,
                    request.RoutingPropertyId,
                    RestoreActorId,
                    ids.NewId(),
                    request.OriginallyCompletedAtUtc,
                    replayedAtUtc);
            if (restored.IsFailure)
            {
                return Result.Failure<GuestAnonymisationRestoreReceipt>(
                    restored.Error);
            }

            profile = restored.Value;
            newProfile = profile;
        }
        else if (profile.Status == GuestProfileState.Anonymised)
        {
            if (!profile.MatchesAnonymisedState(
                    request.ResultingRecordVersion!.Value,
                    request.OriginallyCompletedAtUtc))
            {
                return Result.Failure<GuestAnonymisationRestoreReceipt>(
                    GuestsApplicationErrors
                        .AnonymisationRestoreProofConflict);
            }
        }
        else
        {
            if (profile.Version !=
                request.ResultingRecordVersion!.Value - 1)
            {
                return Result.Failure<GuestAnonymisationRestoreReceipt>(
                    GuestsApplicationErrors
                        .AnonymisationRestoreProofConflict);
            }

            Result<GuestProfileAnonymisationOutcome> restored =
                profile.RestoreAnonymisation(
                    RestoreActorId,
                    ids.NewId(),
                    request.OriginallyCompletedAtUtc,
                    replayedAtUtc);
            if (restored.IsFailure)
            {
                return Result.Failure<GuestAnonymisationRestoreReceipt>(
                    restored.Error);
            }
        }

        if (profile.Version != request.ResultingRecordVersion!.Value)
        {
            return Result.Failure<GuestAnonymisationRestoreReceipt>(
                GuestsApplicationErrors.AnonymisationRestoreProofConflict);
        }

        GuestAnonymisationTombstone? tombstone =
            await restoreRepository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        GuestAnonymisationTombstone? newTombstone = null;
        if (tombstone is null)
        {
            Result<GuestAnonymisationTombstone> created =
                GuestAnonymisationTombstone.Restore(
                    tenantId,
                    request.RecordId,
                    request.OriginallyCompletedAtUtc,
                    request.OwnerReceiptSha256,
                    request.LedgerEntryId,
                    replayedAtUtc);
            if (created.IsFailure)
            {
                return Result.Failure<GuestAnonymisationRestoreReceipt>(
                    created.Error);
            }

            tombstone = created.Value;
            newTombstone = tombstone;
        }
        else
        {
            Result attached = tombstone.AttachRestoreProof(
                request.LedgerEntryId,
                request.OriginallyCompletedAtUtc,
                request.OwnerReceiptSha256,
                replayedAtUtc);
            if (attached.IsFailure)
            {
                return Result.Failure<GuestAnonymisationRestoreReceipt>(
                    attached.Error);
            }
        }

        Result<GuestAnonymisationRestoreReceipt> receipt =
            GuestAnonymisationRestoreReceipt.Create(
                tenantId,
                request.LedgerEntryId,
                request.RecordId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                profile.Version,
                tombstone.Revision,
                tombstone.LastReplayedAtUtc!.Value);
        if (receipt.IsFailure)
        {
            return receipt;
        }

        await restoreRepository.AddAsync(
            receipt.Value,
            newTombstone,
            newProfile,
            cancellationToken).ConfigureAwait(false);
        return receipt;
    }

    private async Task<Result<GuestAnonymisationRestoreReceipt>>
        ReplayAsync(
            GuestAnonymisationRestoreReceipt receipt,
            DataRightsAnonymisationRestoreRequest request,
            CancellationToken cancellationToken)
    {
        if (!receipt.Matches(
                request.LedgerEntryId,
                request.RecordId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256) ||
            receipt.ResultingGuestVersion !=
                request.ResultingRecordVersion!.Value)
        {
            return Result.Failure<GuestAnonymisationRestoreReceipt>(
                GuestsApplicationErrors.AnonymisationRestoreProofConflict);
        }

        GuestProfile? profile =
            await restoreRepository.GetProfileAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        GuestAnonymisationTombstone? tombstone =
            await restoreRepository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (profile is null ||
            profile.Version != receipt.ResultingGuestVersion ||
            !profile.MatchesAnonymisedState(
                request.OriginallyCompletedAtUtc) ||
            tombstone is null ||
            tombstone.Revision != receipt.TombstoneRevision ||
            !tombstone.MatchesRestore(
                request.RecordId,
                request.LedgerEntryId,
                request.OriginallyCompletedAtUtc,
                request.OwnerReceiptSha256))
        {
            return Result.Failure<GuestAnonymisationRestoreReceipt>(
                GuestsApplicationErrors.AnonymisationRestoreProofConflict);
        }

        return Result.Success(receipt);
    }

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
            GuestsDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            GuestsDataRightsCoordinates.GuestProfileRecordType,
            StringComparison.Ordinal) &&
        request.RecordId != Guid.Empty &&
        request.OwnerReceiptContractVersion > 0 &&
        request.OwnerReceiptId != Guid.Empty &&
        IsSha256(request.OwnerReceiptSha256) &&
        request.ResultingRecordVersion > 0 &&
        request.OriginallyCompletedAtUtc != default &&
        request.OriginallyCompletedAtUtc.Offset == TimeSpan.Zero;

    private static bool IsSha256(string? value) =>
        value is { Length: GuestAnonymisationReceipt.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

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

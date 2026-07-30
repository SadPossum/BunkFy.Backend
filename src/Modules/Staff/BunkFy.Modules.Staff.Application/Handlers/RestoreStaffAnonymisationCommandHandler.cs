namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class RestoreStaffAnonymisationCommandHandler(
    IStaffMemberRepository members,
    IStaffOperationLock operationLock,
    IStaffAnonymisationRestoreRepository restoreRepository,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        RestoreStaffAnonymisationCommand,
        StaffAnonymisationRestoreReceipt>
{
    internal const string RestoreActorId = "data-rights-restore";

    public async Task<Result<StaffAnonymisationRestoreReceipt>> HandleAsync(
        RestoreStaffAnonymisationCommand command,
        CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationRestoreRequestV3? request =
            command.Request;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<StaffAnonymisationRestoreReceipt>(
                StaffApplicationErrors.TenantRequired);
        }

        if (!IsValid(request, tenantId))
        {
            return Result.Failure<StaffAnonymisationRestoreReceipt>(
                StaffApplicationErrors.AnonymisationRestoreRequestInvalid);
        }

        bool lockAcquired =
            await operationLock.TryAcquireStaffMemberAsync(
                tenantId,
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (!lockAcquired)
        {
            return Result.Failure<StaffAnonymisationRestoreReceipt>(
                StaffApplicationErrors.AnonymisationRestoreProofConflict);
        }

        StaffAnonymisationRestoreReceipt? existing =
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

        StaffMember? member = await members.GetForDataRightsAsync(
            request.RecordId,
            cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffAnonymisationRestoreReceipt>(
                StaffApplicationErrors.AnonymisationRestoreProofConflict);
        }

        DateTimeOffset replayedAtUtc =
            ToPersistencePrecision(clock.UtcNow);
        if (member.Status == StaffMemberState.Anonymised)
        {
            if (!member.MatchesAnonymisedState(
                    request.ResultingRecordVersion,
                    request.OriginallyCompletedAtUtc))
            {
                return Result.Failure<StaffAnonymisationRestoreReceipt>(
                    StaffApplicationErrors
                        .AnonymisationRestoreProofConflict);
            }
        }
        else
        {
            if (member.Version != request.ResultingRecordVersion - 1)
            {
                return Result.Failure<StaffAnonymisationRestoreReceipt>(
                    StaffApplicationErrors
                        .AnonymisationRestoreProofConflict);
            }

            Result<StaffMemberAnonymisationOutcome> restored =
                member.RestoreAnonymisation(
                    RestoreActorId,
                    ids.NewId(),
                    request.OriginallyCompletedAtUtc,
                    replayedAtUtc);
            if (restored.IsFailure)
            {
                return Result.Failure<StaffAnonymisationRestoreReceipt>(
                    restored.Error);
            }
        }

        if (member.Version != request.ResultingRecordVersion)
        {
            return Result.Failure<StaffAnonymisationRestoreReceipt>(
                StaffApplicationErrors.AnonymisationRestoreProofConflict);
        }

        StaffAnonymisationTombstone? tombstone =
            await restoreRepository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        StaffAnonymisationTombstone? newTombstone = null;
        if (tombstone is null)
        {
            Result<StaffAnonymisationTombstone> created =
                StaffAnonymisationTombstone.Restore(
                    tenantId,
                    request.RecordId,
                    request.OriginallyCompletedAtUtc,
                    request.OwnerReceiptSha256,
                    request.LedgerEntryId,
                    replayedAtUtc);
            if (created.IsFailure)
            {
                return Result.Failure<
                    StaffAnonymisationRestoreReceipt>(created.Error);
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
                return Result.Failure<
                    StaffAnonymisationRestoreReceipt>(attached.Error);
            }
        }

        Result<StaffAnonymisationRestoreReceipt> receipt =
            StaffAnonymisationRestoreReceipt.Create(
                tenantId,
                request.LedgerEntryId,
                request.RecordId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                member.Version,
                tombstone.Revision,
                tombstone.LastReplayedAtUtc!.Value);
        if (receipt.IsFailure)
        {
            return receipt;
        }

        await restoreRepository.AddAsync(
            receipt.Value,
            newTombstone,
            cancellationToken).ConfigureAwait(false);
        return receipt;
    }

    private async Task<Result<StaffAnonymisationRestoreReceipt>>
        ReplayAsync(
            StaffAnonymisationRestoreReceipt receipt,
            DataRightsAnonymisationRestoreRequestV3 request,
            CancellationToken cancellationToken)
    {
        if (!receipt.Matches(
                request.LedgerEntryId,
                request.RecordId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256) ||
            receipt.ResultingStaffVersion !=
                request.ResultingRecordVersion)
        {
            return Result.Failure<StaffAnonymisationRestoreReceipt>(
                StaffApplicationErrors.AnonymisationRestoreProofConflict);
        }

        StaffMember? member = await members.GetForDataRightsAsync(
            request.RecordId,
            cancellationToken).ConfigureAwait(false);
        StaffAnonymisationTombstone? tombstone =
            await restoreRepository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (member is null ||
            !member.MatchesAnonymisedState(
                receipt.ResultingStaffVersion,
                request.OriginallyCompletedAtUtc) ||
            tombstone is null ||
            tombstone.Revision != receipt.TombstoneRevision ||
            tombstone.LastReplayedAtUtc != receipt.ReplayedAtUtc ||
            !tombstone.MatchesRestore(
                request.RecordId,
                request.LedgerEntryId,
                request.OriginallyCompletedAtUtc,
                request.OwnerReceiptSha256))
        {
            return Result.Failure<StaffAnonymisationRestoreReceipt>(
                StaffApplicationErrors.AnonymisationRestoreProofConflict);
        }

        return Result.Success(receipt);
    }

    private static bool IsValid(
        DataRightsAnonymisationRestoreRequestV3? request,
        string tenantId) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationRestoreContractV3.CurrentVersion &&
        string.Equals(
            request.TenantId,
            tenantId,
            StringComparison.Ordinal) &&
        request.LedgerEntryId != Guid.Empty &&
        request.TenantSequence > 0 &&
        IsSha256(request.LedgerEntrySha256) &&
        request.CaseType == DataRightsCaseType.StaffRights &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        request.RoutingPropertyId is null &&
        string.Equals(
            request.OwnerKey,
            StaffDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            StaffDataRightsCoordinates.StaffMemberRecordType,
            StringComparison.Ordinal) &&
        request.RecordId != Guid.Empty &&
        request.OwnerReceiptContractVersion > 0 &&
        request.OwnerReceiptId != Guid.Empty &&
        IsSha256(request.OwnerReceiptSha256) &&
        request.ResultingRecordVersion > 1 &&
        request.OriginallyCompletedAtUtc != default &&
        request.OriginallyCompletedAtUtc.Offset == TimeSpan.Zero;

    private static bool IsSha256(string? value) =>
        value is { Length: StaffAnonymisationReceipt.Sha256Length } &&
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

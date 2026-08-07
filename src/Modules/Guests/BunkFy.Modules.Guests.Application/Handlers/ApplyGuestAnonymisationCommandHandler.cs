namespace BunkFy.Modules.Guests.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Application.Policies;
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

internal sealed class ApplyGuestAnonymisationCommandHandler(
    IGuestProfileRepository profiles,
    IGuestAnonymisationRepository anonymisation,
    IGuestManagementOperationRepository managementOperations,
    IGuestAnonymisationExecutionBoundary executionBoundary,
    IGuestAnonymisationEligibilityEvaluator eligibility,
    IDataRightsOperationApprovalGate approvalGate,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ApplyGuestAnonymisationCommand, GuestAnonymisationReceiptDto>
{
    public async Task<Result<GuestAnonymisationReceiptDto>> HandleAsync(
        ApplyGuestAnonymisationCommand command,
        CancellationToken cancellationToken)
    {
        string? tenantId = scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        string? actorId = NormalizeActor(command.ActorId);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(
                GuestsApplicationErrors.TenantRequired);
        }

        if (!IsValid(command, actorId))
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(
                GuestsApplicationErrors.AnonymisationRequestInvalid);
        }

        string approvalEvidenceSha256 =
            GuestAnonymisationPolicyEvidence.ComputeSha256(command.RoutingPolicy);
        GuestAnonymisationReceipt? existing =
            await anonymisation.FindReceiptByIdempotencyKeyAsync(
                command.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ReplayAsync(
                existing,
                command,
                approvalEvidenceSha256,
                actorId!,
                cancellationToken).ConfigureAwait(false);
        }

        DataRightsOperationApprovalResult approval = await approvalGate.EvaluateAsync(
            new(
                tenantId,
                command.RoutingPropertyId,
                command.CaseId,
                command.ApprovalRevision,
                DataRightsOperation.Anonymisation,
                GuestsDataRightsCoordinates.Owner,
                GuestsDataRightsCoordinates.GuestProfileRecordType,
                command.GuestId,
                command.ExpectedGuestVersion,
                ExecutingActorId: actorId),
            cancellationToken).ConfigureAwait(false);
        if (!approval.IsApproved ||
            approval.ApprovalEvidence is null ||
            !GuestAnonymisationPolicyEvidence.MatchesApproval(
                command.RoutingPolicy,
                approval.ApprovalEvidence))
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(
                GuestsApplicationErrors.DataRightsApprovalRequired);
        }

        await executionBoundary.AcquireAsync(
            tenantId,
            command.GuestId,
            cancellationToken).ConfigureAwait(false);

        GuestAnonymisationEligibilityResult eligibilityResult = await eligibility.EvaluateAsync(
            new(
                GuestAnonymisationEligibilityContract.CurrentVersion,
                tenantId,
                command.CaseId,
                command.ApprovalRevision,
                command.OperationRevision,
                command.RoutingPropertyId,
                command.GuestId,
                command.ExpectedGuestVersion,
                command.RoutingPolicy),
            cancellationToken).ConfigureAwait(false);
        if (eligibilityResult.Status != GuestAnonymisationEligibilityStatus.Eligible ||
            eligibilityResult.BlockerCode != GuestAnonymisationBlockerCode.None ||
            eligibilityResult.GuestVersion != command.ExpectedGuestVersion ||
            eligibilityResult.AffectedPropertyCount < 1 ||
            string.IsNullOrWhiteSpace(eligibilityResult.PolicySetSha256))
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(
                GuestsApplicationErrors.AnonymisationBlocked(
                    eligibilityResult.BlockerCode));
        }

        GuestProfile? profile = await profiles.GetForDataRightsAsync(
            command.RoutingPropertyId,
            command.GuestId,
            cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(
                GuestsApplicationErrors.GuestNotFound);
        }

        DateTimeOffset nowUtc = ToPersistencePrecision(clock.UtcNow);
        Guid eventId = ids.NewId();
        Result<GuestProfileAnonymisationOutcome> mutated = profile.Anonymise(
            command.ExpectedGuestVersion,
            actorId!,
            eventId,
            nowUtc);
        if (mutated.IsFailure)
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(mutated.Error);
        }

        Result<GuestAnonymisationReceipt> receipt = GuestAnonymisationReceipt.Create(
            ids.NewId(),
            tenantId,
            command.IdempotencyKey,
            command.RoutingPropertyId,
            command.CaseId,
            command.ApprovalRevision,
            command.OperationRevision,
            command.GuestId,
            mutated.Value.PreviousVersion,
            mutated.Value.CurrentVersion,
            eligibilityResult.AffectedPropertyCount,
            approvalEvidenceSha256,
            eligibilityResult.PolicySetSha256,
            mutated.Value.EventId,
            actorId!,
            mutated.Value.OccurredAtUtc);
        if (receipt.IsFailure)
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(receipt.Error);
        }

        Result<GuestAnonymisationTombstone> tombstone =
            GuestAnonymisationTombstone.Create(
                tenantId,
                command.GuestId,
                receipt.Value.CompletedAtUtc,
                receipt.Value.CanonicalSha256);
        if (tombstone.IsFailure)
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(tombstone.Error);
        }

        await managementOperations.DeleteForGuestAsync(
            command.GuestId,
            cancellationToken).ConfigureAwait(false);
        await anonymisation.AddAsync(
            receipt.Value,
            tombstone.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private async Task<Result<GuestAnonymisationReceiptDto>> ReplayAsync(
        GuestAnonymisationReceipt receipt,
        ApplyGuestAnonymisationCommand command,
        string approvalEvidenceSha256,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (!receipt.Matches(
                command.IdempotencyKey,
                command.RoutingPropertyId,
                command.CaseId,
                command.ApprovalRevision,
                command.OperationRevision,
                command.GuestId,
                command.ExpectedGuestVersion,
                approvalEvidenceSha256,
                actorId))
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(
                GuestsApplicationErrors.AnonymisationIdempotencyConflict);
        }

        GuestProfile? profile = await profiles.GetForDataRightsAsync(
            command.RoutingPropertyId,
            command.GuestId,
            cancellationToken).ConfigureAwait(false);
        GuestAnonymisationTombstone? tombstone =
            await anonymisation.GetTombstoneAsync(
                command.GuestId,
                cancellationToken).ConfigureAwait(false);
        if (profile is null ||
            !profile.MatchesAnonymisedState(
                receipt.ResultingGuestVersion,
                receipt.CompletedAtUtc) ||
            tombstone is null ||
            !tombstone.Matches(receipt))
        {
            return Result.Failure<GuestAnonymisationReceiptDto>(
                GuestsApplicationErrors.AnonymisationProofUnavailable);
        }

        return Result.Success(receipt.ToDto());
    }

    private static bool IsValid(
        ApplyGuestAnonymisationCommand command,
        string? actorId) =>
        command.IdempotencyKey != Guid.Empty &&
        command.RoutingPropertyId != Guid.Empty &&
        command.CaseId != Guid.Empty &&
        command.ApprovalRevision > 0 &&
        command.OperationRevision > command.ApprovalRevision &&
        command.GuestId != Guid.Empty &&
        command.ExpectedGuestVersion > 0 &&
        actorId is not null &&
        GuestAnonymisationPolicyEvidence.IsValid(command.RoutingPolicy);

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <= GuestProfile.ActorIdMaxLength
            ? normalized
            : null;
    }

    private static DateTimeOffset ToPersistencePrecision(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(value.Ticks - (value.Ticks % ticksPerMicrosecond), value.Offset);
    }
}

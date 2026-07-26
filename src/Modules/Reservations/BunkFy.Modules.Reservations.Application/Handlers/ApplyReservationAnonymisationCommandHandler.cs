namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Mapping;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ApplyReservationAnonymisationCommandHandler(
    IReservationRepository reservations,
    IReservationAnonymisationRepository anonymisation,
    IReservationOperationLock operationLock,
    IReservationAnonymisationEligibilityEvaluator eligibility,
    IDataRightsOperationApprovalGate approvalGate,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        ApplyReservationAnonymisationCommand,
        ReservationAnonymisationReceiptDto>
{
    public async Task<Result<ReservationAnonymisationReceiptDto>> HandleAsync(
        ApplyReservationAnonymisationCommand command,
        CancellationToken cancellationToken)
    {
        string? tenantId = scopeContext.IsEnabled
            ? scopeContext.ScopeId
            : null;
        string? actorId = NormalizeActor(command.ActorId);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                ReservationsApplicationErrors.TenantRequired);
        }

        if (!IsValid(command, actorId))
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                ReservationsApplicationErrors.AnonymisationRequestInvalid);
        }

        string approvalEvidenceSha256 =
            ReservationAnonymisationPolicyEvidence.ComputeSha256(
                command.RoutingPolicy);
        ReservationAnonymisationReceipt? existing =
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

        DataRightsOperationApprovalResult approval =
            await approvalGate.EvaluateAsync(
                new(
                    tenantId,
                    command.PropertyId,
                    command.CaseId,
                    command.ApprovalRevision,
                    DataRightsOperation.Anonymisation,
                    ReservationsDataRightsCoordinates.Owner,
                    ReservationsDataRightsCoordinates.ReservationRecordType,
                    command.ReservationId,
                    command.ExpectedReservationVersion,
                    ExecutingActorId: actorId),
                cancellationToken).ConfigureAwait(false);
        if (!approval.IsApproved ||
            approval.ApprovalEvidence is null ||
            !ReservationAnonymisationPolicyEvidence.MatchesApproval(
                command.RoutingPolicy,
                approval.ApprovalEvidence))
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                ReservationsApplicationErrors.DataRightsApprovalRequired);
        }

        await operationLock.AcquireAsync(
            tenantId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);

        ReservationAnonymisationEligibilityResult eligibilityResult =
            await eligibility.EvaluateAsync(
                new(
                    ReservationAnonymisationEligibilityContract.CurrentVersion,
                    tenantId,
                    command.CaseId,
                    command.ApprovalRevision,
                    command.OperationRevision,
                    command.PropertyId,
                    command.ReservationId,
                    command.ExpectedReservationVersion,
                    command.ExpectedDetailsRevision,
                    command.RoutingPolicy),
                cancellationToken).ConfigureAwait(false);
        if (eligibilityResult.Status !=
                ReservationAnonymisationEligibilityStatus.Eligible ||
            eligibilityResult.BlockerCode !=
                ReservationAnonymisationBlockerCode.None ||
            eligibilityResult.ReservationVersion !=
                command.ExpectedReservationVersion ||
            eligibilityResult.DetailsRevision !=
                command.ExpectedDetailsRevision ||
            string.IsNullOrWhiteSpace(
                eligibilityResult.PolicyEvidenceSha256))
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                ReservationsApplicationErrors.AnonymisationBlocked(
                    eligibilityResult.BlockerCode));
        }

        Reservation? reservation = await reservations.GetForDataRightsAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                ReservationsApplicationErrors.ReservationNotFound);
        }

        DateTimeOffset nowUtc = ToPersistencePrecision(clock.UtcNow);
        Result<ReservationAnonymisationOutcome> mutated =
            reservation.Anonymise(
                command.ExpectedReservationVersion,
                command.ExpectedDetailsRevision,
                actorId!,
                ids.NewId(),
                nowUtc);
        if (mutated.IsFailure)
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                mutated.Error);
        }

        ReservationAnonymisationAffectedRecords affected =
            await anonymisation.RedactOwnedRecordsAsync(
                reservation,
                mutated.Value,
                cancellationToken).ConfigureAwait(false);
        Result<ReservationAnonymisationReceipt> receipt =
            ReservationAnonymisationReceipt.Create(
                ids.NewId(),
                tenantId,
                command.IdempotencyKey,
                command.PropertyId,
                command.CaseId,
                command.ApprovalRevision,
                command.OperationRevision,
                command.ReservationId,
                mutated.Value,
                affected.RedactedHistoryCount,
                affected.ReducedExternalOperationCount,
                affected.SuppressedReminderCount,
                approvalEvidenceSha256,
                eligibilityResult.PolicyEvidenceSha256!);
        if (receipt.IsFailure)
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                receipt.Error);
        }

        await anonymisation.AddReceiptAsync(
            receipt.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private async Task<Result<ReservationAnonymisationReceiptDto>> ReplayAsync(
        ReservationAnonymisationReceipt receipt,
        ApplyReservationAnonymisationCommand command,
        string approvalEvidenceSha256,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (!receipt.Matches(
                command.IdempotencyKey,
                command.PropertyId,
                command.CaseId,
                command.ApprovalRevision,
                command.OperationRevision,
                command.ReservationId,
                command.ExpectedReservationVersion,
                command.ExpectedDetailsRevision,
                approvalEvidenceSha256,
                actorId))
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                ReservationsApplicationErrors.AnonymisationIdempotencyConflict);
        }

        Reservation? reservation = await reservations.GetForDataRightsAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null ||
            !reservation.MatchesAnonymisedState(
                receipt.ResultingReservationVersion,
                receipt.ResultingDetailsRevision,
                receipt.CompletedAtUtc) ||
            !await anonymisation.VerifyOwnerStateAsync(
                receipt,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ReservationAnonymisationReceiptDto>(
                ReservationsApplicationErrors.AnonymisationProofUnavailable);
        }

        return Result.Success(receipt.ToDto());
    }

    private static bool IsValid(
        ApplyReservationAnonymisationCommand command,
        string? actorId) =>
        command.IdempotencyKey != Guid.Empty &&
        command.PropertyId != Guid.Empty &&
        command.CaseId != Guid.Empty &&
        command.ApprovalRevision > 0 &&
        command.OperationRevision > command.ApprovalRevision &&
        command.ReservationId != Guid.Empty &&
        command.ExpectedReservationVersion > 0 &&
        command.ExpectedDetailsRevision > 0 &&
        actorId is not null &&
        ReservationAnonymisationPolicyEvidence.IsValid(command.RoutingPolicy);

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <= Reservation.ActorIdMaxLength
            ? normalized
            : null;
    }

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }
}

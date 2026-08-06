namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Mapping;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ApplyReservationDataRightsCorrectionCommandHandler(
    ReservationMutationCoordinator mutations,
    IReservationDataRightsCorrectionReceiptRepository receipts,
    IDataRightsCorrectionExecutionGate executionGate,
    IReservationCountryPolicyAdmission countryPolicy,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        ApplyReservationDataRightsCorrectionCommand,
        ReservationDataRightsCorrectionReceiptDto>
{
    public async Task<Result<ReservationDataRightsCorrectionReceiptDto>> HandleAsync(
        ApplyReservationDataRightsCorrectionCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(
                ReservationsApplicationErrors.TenantRequired);
        }

        if (!IsValid(command))
        {
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(
                ReservationsApplicationErrors.CorrectionRequestInvalid);
        }

        ReservationDataRightsCorrectionReceipt? existing =
            await receipts.FindByIdempotencyKeyAsync(
                command.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ReplayAsync(existing, command, cancellationToken)
                .ConfigureAwait(false);
        }

        CountryPolicyDecision policyDecision = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            ReservationCountryPolicyAdmission.DataRightsCorrectionPurpose,
            CountryPolicySurface.ApiWrite,
            ReservationCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!policyDecision.IsAllowed)
        {
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(
                ReservationsApplicationErrors.CountryPolicyDenied(policyDecision.Reason));
        }

        DataRightsCorrectionExecutionGateResult execution =
            await executionGate.EvaluateAsync(
            new(
                scopeContext.ScopeId,
                DataRightsCaseType.GuestRights,
                command.PropertyId,
                command.CaseId,
                command.ApprovalRevision,
                command.IdempotencyKey,
                new DataRightsSubjectCoordinate(
                    ReservationsDataRightsCoordinates.Owner,
                    ReservationsDataRightsCoordinates.ReservationRecordType,
                    command.ReservationId,
                    command.ExpectedVersion),
                ReservationsDataRightsCoordinates.CorrectionFieldPolicyKey,
                command.ActorId),
            cancellationToken).ConfigureAwait(false);
        if (!execution.IsAllowed)
        {
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(
                ReservationsApplicationErrors.DataRightsApprovalRequired);
        }

        Reservation? reservation = await mutations.AcquireDataRightsAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(
                ReservationsApplicationErrors.ReservationNotFound);
        }

        DateTimeOffset nowUtc = ToPersistencePrecision(clock.UtcNow);
        Guid correlationId = ids.NewId();
        Result<ReservationDataRightsCorrectionOutcome> corrected = reservation.CorrectGuestDetails(
            command.PrimaryGuestName,
            command.Email,
            command.Phone,
            command.GuestCount,
            command.Notes,
            command.ExpectedVersion,
            command.ExpectedDetailsRevision,
            command.ActorId,
            correlationId,
            ids.NewId(),
            nowUtc,
            command.ExpectedArrivalTime,
            command.ExpectedDepartureTime);
        if (corrected.IsFailure)
        {
            Error error = corrected.Error == ReservationsDomainErrors.DataRightsCorrectionNoChanges
                ? ReservationsApplicationErrors.CorrectionNoChanges
                : corrected.Error;
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(error);
        }

        Result<ReservationDataRightsCorrectionReceipt> created =
            ReservationDataRightsCorrectionReceipt.Create(
                ids.NewId(),
                scopeContext.ScopeId,
                command.IdempotencyKey,
                command.PropertyId,
                command.CaseId,
                command.ApprovalRevision,
                command.ReservationId,
                corrected.Value,
                ids.NewId(),
                ids.NewId());
        if (created.IsFailure)
        {
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(created.Error);
        }

        await receipts.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToDto());
    }

    private async Task<Result<ReservationDataRightsCorrectionReceiptDto>> ReplayAsync(
        ReservationDataRightsCorrectionReceipt receipt,
        ApplyReservationDataRightsCorrectionCommand command,
        CancellationToken cancellationToken)
    {
        if (receipt.PropertyId != command.PropertyId ||
            receipt.CaseId != command.CaseId ||
            receipt.ApprovalRevision != command.ApprovalRevision ||
            receipt.ReservationId != command.ReservationId ||
            receipt.SelectedRecordVersion != command.ExpectedVersion ||
            receipt.SelectedDetailsRevision != command.ExpectedDetailsRevision)
        {
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(
                ReservationsApplicationErrors.CorrectionIdempotencyConflict);
        }

        Reservation? reservation = await mutations.AcquireDataRightsAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null ||
            reservation.Version != receipt.CurrentRecordVersion ||
            reservation.DetailsRevision != receipt.CurrentDetailsRevision ||
            !reservation.HasGuestDetails(
                command.PrimaryGuestName,
                command.Email,
                command.Phone,
                command.GuestCount,
                command.Notes,
                command.ExpectedArrivalTime,
                command.ExpectedDepartureTime))
        {
            return Result.Failure<ReservationDataRightsCorrectionReceiptDto>(
                ReservationsApplicationErrors.CorrectionIdempotencyConflict);
        }

        return Result.Success(receipt.ToDto());
    }

    private static bool IsValid(ApplyReservationDataRightsCorrectionCommand command) =>
        command.IdempotencyKey != Guid.Empty &&
        command.PropertyId != Guid.Empty &&
        command.CaseId != Guid.Empty &&
        command.ApprovalRevision >= 1 &&
        command.ReservationId != Guid.Empty &&
        command.ExpectedVersion >= 1 &&
        command.ExpectedDetailsRevision >= 0 &&
        !string.IsNullOrWhiteSpace(command.ActorId);

    private static DateTimeOffset ToPersistencePrecision(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }
}

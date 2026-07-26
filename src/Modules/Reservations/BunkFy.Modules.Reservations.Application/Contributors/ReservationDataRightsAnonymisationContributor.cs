namespace BunkFy.Modules.Reservations.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class ReservationDataRightsAnonymisationContributor(
    IRequestDispatcher dispatcher,
    IReservationAnonymisationEligibilityRepository eligibilityRepository,
    IReservationAnonymisationRepository anonymisationRepository,
    ISystemClock clock)
    : IDataRightsAnonymisationContributor
{
    private const string BlockedPrefix =
        "Reservations.AnonymisationBlocked.";
    private const string CompletedDispositionCode =
        "reservations.completed";
    private const string OwnerDataRedactedReasonCode =
        "reservations.owner-data-redacted";

    public string OwnerKey => ReservationsDataRightsCoordinates.Owner;

    public int ContractVersion =>
        DataRightsAnonymisationContract.CurrentVersion;

    public async Task<DataRightsAnonymisationContributionResult>
        ExecuteAsync(
            DataRightsAnonymisationContributionRequest request,
            CancellationToken cancellationToken)
    {
        if (!IsValid(request, clock.UtcNow))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                ReservationsApplicationErrors
                    .AnonymisationRequestInvalid.Code);
        }

        ReservationAnonymisationReceipt? existing =
            await anonymisationRepository
                .FindReceiptByIdempotencyKeyAsync(
                    request.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);
        long detailsRevision;
        if (existing is not null)
        {
            detailsRevision = existing.SelectedDetailsRevision;
        }
        else
        {
            ReservationAnonymisationEligibilitySnapshot? current =
                await eligibilityRepository.LoadAsync(
                    request.RoutingPropertyId,
                    request.Coordinate.RecordId,
                    cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                return DataRightsAnonymisationContributionResult.Failed(
                    ReservationsApplicationErrors
                        .ReservationNotFound.Code);
            }

            if (current.ReservationVersion !=
                request.Coordinate.RecordVersion)
            {
                return DataRightsAnonymisationContributionResult.Blocked(
                    ReservationsApplicationErrors.AnonymisationBlocked(
                        ReservationAnonymisationBlockerCode
                            .ReservationVersionChanged).Code);
            }

            detailsRevision = current.DetailsRevision;
        }

        DataRightsApprovalEvidence policy = request.RoutingPolicy;
        Result<ReservationAnonymisationReceiptDto> executed =
            await dispatcher.SendAsync(
                new ApplyReservationAnonymisationCommand(
                    request.IdempotencyKey,
                    request.RoutingPropertyId,
                    request.CaseId,
                    request.ApprovalRevision,
                    request.OperationRevision,
                    request.Coordinate.RecordId,
                    request.Coordinate.RecordVersion,
                    detailsRevision,
                    new ReservationAnonymisationRoutingPolicyEvidence(
                        policy.PropertyVersion,
                        policy.OperatingCountryCode,
                        policy.PolicyId,
                        policy.PolicyVersion,
                        policy.RetentionPolicyId,
                        policy.RetentionPolicyVersion,
                        policy.ContentSha256,
                        policy.PurposeCode,
                        policy.Surface,
                        policy.SourceProvenance,
                        policy.EvaluatedAtUtc),
                    request.ExecutingActorId),
                cancellationToken).ConfigureAwait(false);
        if (executed.IsFailure)
        {
            return executed.Error.Code.StartsWith(
                BlockedPrefix,
                StringComparison.Ordinal)
                ? DataRightsAnonymisationContributionResult.Blocked(
                    executed.Error.Code)
                : DataRightsAnonymisationContributionResult.Failed(
                    executed.Error.Code);
        }

        ReservationAnonymisationReceiptDto receipt = executed.Value;
        if (!Matches(request, detailsRevision, receipt))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                ReservationsApplicationErrors
                    .AnonymisationProofUnavailable.Code);
        }

        return DataRightsAnonymisationContributionResult.Completed(
            new DataRightsAnonymisationOwnerProof(
                receipt.ContractVersion,
                receipt.ReceiptId,
                receipt.ResultingReservationVersion,
                CompletedDispositionCode,
                OwnerDataRedactedReasonCode,
                receipt.CanonicalSha256,
                receipt.CompletedAtUtc));
    }

    private static bool IsValid(
        DataRightsAnonymisationContributionRequest? request,
        DateTimeOffset nowUtc) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationContract.CurrentVersion &&
        request.Coordinate is not null &&
        request.RoutingPolicy is not null &&
        string.Equals(
            request.Coordinate.OwnerKey,
            ReservationsDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            ReservationsDataRightsCoordinates.ReservationRecordType,
            StringComparison.Ordinal) &&
        request.WorkItemId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        request.RoutingPropertyId != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        request.RoutingPolicy.PropertyId ==
            request.RoutingPropertyId &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        request.DeadlineUtc > nowUtc;

    private static bool Matches(
        DataRightsAnonymisationContributionRequest request,
        long selectedDetailsRevision,
        ReservationAnonymisationReceiptDto receipt) =>
        receipt.ContractVersion > 0 &&
        receipt.ReceiptId != Guid.Empty &&
        receipt.IdempotencyKey == request.IdempotencyKey &&
        receipt.PropertyId == request.RoutingPropertyId &&
        receipt.CaseId == request.CaseId &&
        receipt.ApprovalRevision == request.ApprovalRevision &&
        receipt.OperationRevision == request.OperationRevision &&
        receipt.ReservationId == request.Coordinate.RecordId &&
        receipt.SelectedReservationVersion ==
            request.Coordinate.RecordVersion &&
        receipt.ResultingReservationVersion ==
            receipt.SelectedReservationVersion + 1 &&
        receipt.SelectedDetailsRevision == selectedDetailsRevision &&
        receipt.ResultingDetailsRevision ==
            receipt.SelectedDetailsRevision + 1 &&
        receipt.Disposition ==
            BunkFy.Modules.Reservations.Contracts
                .ReservationAnonymisationDisposition.Completed &&
        receipt.Reason ==
            BunkFy.Modules.Reservations.Contracts
                .ReservationAnonymisationReason
                .ReservationOwnerDataRedacted &&
        receipt.CompletedAtUtc != default &&
        IsSha256(receipt.CanonicalSha256);

    private static bool IsSha256(string? value) =>
        value is
        {
            Length: DataRightsAnonymisationContract.Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}

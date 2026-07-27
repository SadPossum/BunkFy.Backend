namespace BunkFy.Modules.Inventory.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class InventoryDataRightsAnonymisationContributor(
    IRequestDispatcher dispatcher,
    ISystemClock clock)
    : IDataRightsAnonymisationContributor
{
    private const string BlockedPrefix =
        "Inventory.AnonymisationBlocked.";
    private const string CompletedDispositionCode =
        "inventory.completed";
    private const string CorrelationPseudonymisedReasonCode =
        "inventory.reservation-correlation-pseudonymised";

    public string OwnerKey => InventoryDataRightsCoordinates.Owner;

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
                InventoryApplicationErrors
                    .AnonymisationRequestInvalid.Code);
        }

        Result<InventoryAllocationAnonymisationReceiptDto> executed =
            await dispatcher.SendAsync(
                new ApplyInventoryAllocationAnonymisationCommand(
                    request),
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

        InventoryAllocationAnonymisationReceiptDto receipt =
            executed.Value;
        if (!Matches(request, receipt))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                InventoryApplicationErrors
                    .AnonymisationProofUnavailable.Code);
        }

        return DataRightsAnonymisationContributionResult.Completed(
            new(
                receipt.ContractVersion,
                receipt.ReceiptId,
                receipt.ResultingAllocationVersion,
                CompletedDispositionCode,
                CorrelationPseudonymisedReasonCode,
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
        string.Equals(
            request.Coordinate.OwnerKey,
            InventoryDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            InventoryDataRightsCoordinates.AllocationRecordType,
            StringComparison.Ordinal) &&
        request.WorkItemId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        request.RoutingPropertyId != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        request.RoutingPolicy is not null &&
        request.RoutingPolicy.PropertyId ==
            request.RoutingPropertyId &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        request.DeadlineUtc > nowUtc;

    private static bool Matches(
        DataRightsAnonymisationContributionRequest request,
        InventoryAllocationAnonymisationReceiptDto receipt) =>
        receipt.ContractVersion > 0 &&
        receipt.ReceiptId != Guid.Empty &&
        receipt.WorkItemId == request.WorkItemId &&
        receipt.IdempotencyKey == request.IdempotencyKey &&
        receipt.PropertyId == request.RoutingPropertyId &&
        receipt.CaseId == request.CaseId &&
        receipt.ApprovalRevision == request.ApprovalRevision &&
        receipt.OperationRevision == request.OperationRevision &&
        receipt.AllocationId == request.Coordinate.RecordId &&
        receipt.SelectedAllocationVersion ==
            request.Coordinate.RecordVersion &&
        receipt.ResultingAllocationVersion ==
            receipt.SelectedAllocationVersion + 1 &&
        receipt.ResultingReservationPseudonym != Guid.Empty &&
        receipt.Disposition ==
            InventoryAllocationAnonymisationDisposition.Completed &&
        receipt.Reason ==
            InventoryAllocationAnonymisationReason
                .ReservationCorrelationPseudonymised &&
        receipt.RemovedAmendmentDecisionCount >= 0 &&
        receipt.CompletedAtUtc != default &&
        IsSha256(receipt.CanonicalSha256);

    private static bool IsSha256(string? value) =>
        value is
        {
            Length: DataRightsAnonymisationContract.Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));
}

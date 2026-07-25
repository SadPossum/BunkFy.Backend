namespace BunkFy.Modules.Guests.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class GuestDataRightsAnonymisationContributor(
    IRequestDispatcher dispatcher,
    ISystemClock clock)
    : IDataRightsAnonymisationContributor
{
    private const string BlockedPrefix = "Guests.AnonymisationBlocked.";
    private const string CompletedDispositionCode = "guests.completed";
    private const string ProfileAnonymisedReasonCode = "guests.profile-anonymised";

    public string OwnerKey => GuestsDataRightsCoordinates.Owner;

    public int ContractVersion => DataRightsAnonymisationContract.CurrentVersion;

    public async Task<DataRightsAnonymisationContributionResult> ExecuteAsync(
        DataRightsAnonymisationContributionRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValid(request, clock.UtcNow))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                GuestsApplicationErrors.AnonymisationRequestInvalid.Code);
        }

        DataRightsApprovalEvidence policy = request.RoutingPolicy;
        Result<GuestAnonymisationReceiptDto> executed = await dispatcher.SendAsync(
            new ApplyGuestAnonymisationCommand(
                request.IdempotencyKey,
                request.RoutingPropertyId,
                request.CaseId,
                request.ApprovalRevision,
                request.OperationRevision,
                request.Coordinate.RecordId,
                request.Coordinate.RecordVersion,
                new GuestAnonymisationRoutingPolicyEvidence(
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
            return executed.Error.Code.StartsWith(BlockedPrefix, StringComparison.Ordinal)
                ? DataRightsAnonymisationContributionResult.Blocked(executed.Error.Code)
                : DataRightsAnonymisationContributionResult.Failed(executed.Error.Code);
        }

        GuestAnonymisationReceiptDto receipt = executed.Value;
        if (!Matches(request, receipt))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                GuestsApplicationErrors.AnonymisationProofUnavailable.Code);
        }

        return DataRightsAnonymisationContributionResult.Completed(
            new DataRightsAnonymisationOwnerProof(
                receipt.ContractVersion,
                receipt.ReceiptId,
                receipt.ResultingGuestVersion,
                CompletedDispositionCode,
                ProfileAnonymisedReasonCode,
                receipt.CanonicalSha256,
                receipt.CompletedAtUtc));
    }

    private static bool IsValid(
        DataRightsAnonymisationContributionRequest? request,
        DateTimeOffset nowUtc) =>
        request is not null &&
        request.ContractVersion == DataRightsAnonymisationContract.CurrentVersion &&
        string.Equals(
            request.Coordinate.OwnerKey,
            GuestsDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            GuestsDataRightsCoordinates.GuestProfileRecordType,
            StringComparison.Ordinal) &&
        request.WorkItemId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        request.RoutingPropertyId != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        request.RoutingPolicy.PropertyId == request.RoutingPropertyId &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        request.DeadlineUtc > nowUtc;

    private static bool Matches(
        DataRightsAnonymisationContributionRequest request,
        GuestAnonymisationReceiptDto receipt) =>
        receipt.ContractVersion > 0 &&
        receipt.ReceiptId != Guid.Empty &&
        receipt.IdempotencyKey == request.IdempotencyKey &&
        receipt.RoutingPropertyId == request.RoutingPropertyId &&
        receipt.CaseId == request.CaseId &&
        receipt.ApprovalRevision == request.ApprovalRevision &&
        receipt.OperationRevision == request.OperationRevision &&
        receipt.GuestId == request.Coordinate.RecordId &&
        receipt.SelectedGuestVersion == request.Coordinate.RecordVersion &&
        receipt.ResultingGuestVersion > receipt.SelectedGuestVersion &&
        receipt.Disposition == GuestAnonymisationDisposition.Completed &&
        receipt.Reason == GuestAnonymisationReason.ProfileAnonymised &&
        receipt.CompletedAtUtc != default &&
        receipt.CanonicalSha256.Length == DataRightsAnonymisationContract.Sha256Length &&
        receipt.CanonicalSha256.All(Uri.IsHexDigit);
}

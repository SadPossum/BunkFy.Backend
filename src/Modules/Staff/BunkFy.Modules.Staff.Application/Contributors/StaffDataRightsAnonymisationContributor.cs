namespace BunkFy.Modules.Staff.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class StaffDataRightsAnonymisationContributor(
    IRequestDispatcher dispatcher,
    ISystemClock clock)
    : IDataRightsAnonymisationContributorV2
{
    private const string BlockedPrefix =
        "Staff.AnonymisationBlocked.";
    private const string CompletedDispositionCode =
        "staff.completed";
    private const string ProfileAnonymisedReasonCode =
        "staff.profile-anonymised";

    public string OwnerKey => StaffDataRightsCoordinates.Owner;

    public string RecordType =>
        StaffDataRightsCoordinates.StaffMemberRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public int ContractVersion =>
        DataRightsAnonymisationContractV2.CurrentVersion;

    public async Task<DataRightsAnonymisationContributionResult>
        ExecuteAsync(
            DataRightsAnonymisationContributionRequestV2 request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValid(request, clock.UtcNow))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                this.ContractVersion,
                StaffApplicationErrors
                    .AnonymisationRequestInvalid.Code);
        }

        Result<StaffAnonymisationReceiptDto> executed =
            await dispatcher.SendAsync(
                new ApplyStaffAnonymisationCommand(
                    request.IdempotencyKey,
                    request.CaseId,
                    request.ApprovalRevision,
                    request.OperationRevision,
                    request.Coordinate.RecordId,
                    request.Coordinate.RecordVersion,
                    request.ApprovalEvidence,
                    request.ExecutingActorId),
                cancellationToken).ConfigureAwait(false);
        if (executed.IsFailure)
        {
            return executed.Error.Code.StartsWith(
                    BlockedPrefix,
                    StringComparison.Ordinal)
                ? DataRightsAnonymisationContributionResult.Blocked(
                    this.ContractVersion,
                    executed.Error.Code)
                : DataRightsAnonymisationContributionResult.Failed(
                    this.ContractVersion,
                    executed.Error.Code);
        }

        StaffAnonymisationReceiptDto receipt = executed.Value;
        if (!Matches(request, receipt))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                this.ContractVersion,
                StaffApplicationErrors
                    .AnonymisationProofUnavailable.Code);
        }

        return DataRightsAnonymisationContributionResult.Completed(
            this.ContractVersion,
            new DataRightsAnonymisationOwnerProof(
                receipt.ContractVersion,
                receipt.ReceiptId,
                receipt.ResultingStaffVersion,
                CompletedDispositionCode,
                ProfileAnonymisedReasonCode,
                receipt.CanonicalSha256,
                receipt.CompletedAtUtc));
    }

    private bool IsValid(
        DataRightsAnonymisationContributionRequestV2? request,
        DateTimeOffset nowUtc) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.CaseType == this.CaseType &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        request.PropertyId is null &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        request.WorkItemId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        string.Equals(
            request.Coordinate.OwnerKey,
            this.OwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            this.RecordType,
            StringComparison.Ordinal) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        StaffAnonymisationPolicyEvidence.IsValidStaffApproval(
            request.ApprovalEvidence) &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        request.DeadlineUtc > nowUtc;

    private static bool Matches(
        DataRightsAnonymisationContributionRequestV2 request,
        StaffAnonymisationReceiptDto receipt) =>
        receipt.ContractVersion > 0 &&
        receipt.ReceiptId != Guid.Empty &&
        receipt.IdempotencyKey == request.IdempotencyKey &&
        receipt.CaseId == request.CaseId &&
        receipt.ApprovalRevision == request.ApprovalRevision &&
        receipt.OperationRevision == request.OperationRevision &&
        receipt.StaffMemberId == request.Coordinate.RecordId &&
        receipt.SelectedStaffVersion ==
            request.Coordinate.RecordVersion &&
        receipt.ResultingStaffVersion ==
            receipt.SelectedStaffVersion + 1 &&
        receipt.ResultingOperationLockRevision ==
            receipt.SelectedOperationLockRevision + 1 &&
        receipt.Disposition ==
            StaffAnonymisationDisposition.Completed &&
        receipt.Reason ==
            StaffAnonymisationReason.ProfileAnonymised &&
        receipt.CompletedAtUtc != default &&
        receipt.CanonicalSha256.Length ==
            DataRightsAnonymisationContract.Sha256Length &&
        receipt.CanonicalSha256.All(Uri.IsHexDigit);
}

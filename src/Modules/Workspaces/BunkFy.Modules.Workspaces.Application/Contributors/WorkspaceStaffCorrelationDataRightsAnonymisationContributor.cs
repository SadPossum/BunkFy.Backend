namespace BunkFy.Modules.Workspaces.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class
    WorkspaceStaffCorrelationDataRightsAnonymisationContributor(
        IRequestDispatcher dispatcher,
        ISystemClock clock)
    : IDataRightsAnonymisationContributorV2
{
    private const string BlockedPrefix =
        "Workspaces.StaffCorrelationAnonymisationBlocked.";
    private const string CompletedDispositionCode =
        "workspaces.completed";
    private const string PseudonymisedReasonCode =
        "workspaces.subject-correlations-pseudonymised";

    public string OwnerKey =>
        WorkspacesDataRightsCoordinates.Owner;

    public string RecordType =>
        WorkspacesDataRightsCoordinates
            .StaffAccessProcessRecordType;

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
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .RequestInvalid.Code);
        }

        Result<WorkspaceStaffCorrelationAnonymisationReceiptDto>
            executed = await dispatcher.SendAsync(
                new ApplyWorkspaceStaffCorrelationAnonymisationCommand(
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

        WorkspaceStaffCorrelationAnonymisationReceiptDto receipt =
            executed.Value;
        if (!Matches(request, receipt))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                this.ContractVersion,
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .ProofUnavailable.Code);
        }

        return DataRightsAnonymisationContributionResult.Completed(
            this.ContractVersion,
            new(
                receipt.ContractVersion,
                receipt.ReceiptId,
                receipt.ResultingAnchorVersion,
                CompletedDispositionCode,
                PseudonymisedReasonCode,
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
        WorkspaceStaffCorrelationAnonymisationPolicyEvidence
            .IsValid(request.ApprovalEvidence) &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        request.DeadlineUtc > nowUtc;

    private static bool Matches(
        DataRightsAnonymisationContributionRequestV2 request,
        WorkspaceStaffCorrelationAnonymisationReceiptDto receipt) =>
        receipt.ContractVersion > 0 &&
        receipt.ReceiptId != Guid.Empty &&
        receipt.IdempotencyKey == request.IdempotencyKey &&
        receipt.CaseId == request.CaseId &&
        receipt.ApprovalRevision == request.ApprovalRevision &&
        receipt.OperationRevision == request.OperationRevision &&
        receipt.AnchorProcessId == request.Coordinate.RecordId &&
        receipt.SelectedAnchorVersion ==
            request.Coordinate.RecordVersion &&
        receipt.ResultingAnchorVersion ==
            receipt.SelectedAnchorVersion + 1 &&
        receipt.AccessProcessRecordsScrubbed > 0 &&
        receipt.Disposition ==
            WorkspaceStaffCorrelationAnonymisationDisposition
                .Completed &&
        receipt.Reason ==
            WorkspaceStaffCorrelationAnonymisationReason
                .SubjectCorrelationsPseudonymised &&
        receipt.CompletedAtUtc != default &&
        receipt.CanonicalSha256.Length ==
            DataRightsAnonymisationContract.Sha256Length &&
        receipt.CanonicalSha256.All(Uri.IsHexDigit);
}

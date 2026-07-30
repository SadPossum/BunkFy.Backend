namespace BunkFy.Modules.Workspaces.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.Extensions.Logging;

internal sealed class
    WorkspaceStaffCorrelationAnonymisationPrerequisite(
        IWorkspaceStaffCorrelationAnonymisationRepository
            correlations,
        WorkspaceStaffAccessDenier accessDenier,
        ILogger<
            WorkspaceStaffCorrelationAnonymisationPrerequisite>
            logger)
    : IDataRightsAnonymisationExecutionPrerequisiteV2,
      IDataRightsAnonymisationRestorePrerequisiteV3
{
    private const string RequestInvalid =
        "Workspaces.StaffCorrelationAnonymisationPrerequisiteInvalid";
    private const string StateUnavailable =
        "Workspaces.StaffCorrelationAnonymisationStateUnavailable";
    private const string StateConflict =
        "Workspaces.StaffCorrelationAnonymisationStateConflict";
    private const string ActiveOnboarding =
        "Workspaces.StaffCorrelationAnonymisationActiveOnboarding";
    private const string ActiveAccessProcess =
        "Workspaces.StaffCorrelationAnonymisationActiveAccessProcess";
    private const string OwnerProtected =
        "Workspaces.StaffCorrelationAnonymisationOwnerProtected";
    private const string RetryRequired =
        "Workspaces.StaffCorrelationAnonymisationRetryRequired";

    public string OwnerKey =>
        WorkspacesDataRightsCoordinates.Owner;

    public string RecordType =>
        WorkspacesDataRightsCoordinates
            .StaffAccessProcessRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    int IDataRightsAnonymisationExecutionPrerequisiteV2
        .ContractVersion =>
        DataRightsAnonymisationExecutionPrerequisiteContractV2
            .CurrentVersion;

    int IDataRightsAnonymisationRestorePrerequisiteV3
        .ContractVersion =>
        DataRightsAnonymisationRestoreContractV3.CurrentVersion;

    public async Task<
        DataRightsAnonymisationExecutionPrerequisiteResult>
        ExecuteAsync(
            DataRightsAnonymisationContributionRequestV2 request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return ExecutionBlocked(RequestInvalid);
        }

        ClosureResult closure = await this.EnsureClosedAsync(
            request.TenantId,
            request.Coordinate.RecordId,
            request.Coordinate.RecordVersion,
            expectedOwnerReceiptContractVersion: null,
            expectedOwnerReceiptId: null,
            expectedOwnerReceiptSha256: null,
            cancellationToken).ConfigureAwait(false);
        return closure.Status switch
        {
            ClosureStatus.Completed =>
                DataRightsAnonymisationExecutionPrerequisiteResult
                    .Completed(
                        DataRightsAnonymisationExecutionPrerequisiteContractV2
                            .CurrentVersion),
            ClosureStatus.Blocked =>
                ExecutionBlocked(closure.Code),
            _ =>
                DataRightsAnonymisationExecutionPrerequisiteResult
                    .RetryRequired(
                        DataRightsAnonymisationExecutionPrerequisiteContractV2
                            .CurrentVersion,
                        closure.Code)
        };
    }

    public async Task<
        DataRightsAnonymisationRestorePrerequisiteResult>
        ExecuteAsync(
            DataRightsAnonymisationRestoreRequestV3 request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return RestoreBlocked(RequestInvalid);
        }

        ClosureResult closure = await this.EnsureClosedAsync(
            request.TenantId,
            request.RecordId,
            request.ResultingRecordVersion - 1,
            request.OwnerReceiptContractVersion,
            request.OwnerReceiptId,
            request.OwnerReceiptSha256,
            cancellationToken).ConfigureAwait(false);
        return closure.Status switch
        {
            ClosureStatus.Completed =>
                DataRightsAnonymisationRestorePrerequisiteResult
                    .Completed(
                        DataRightsAnonymisationRestoreContractV3
                            .CurrentVersion),
            ClosureStatus.Blocked =>
                RestoreBlocked(closure.Code),
            _ =>
                DataRightsAnonymisationRestorePrerequisiteResult
                    .RetryRequired(
                        DataRightsAnonymisationRestoreContractV3
                            .CurrentVersion,
                        closure.Code)
        };
    }

    private async Task<ClosureResult> EnsureClosedAsync(
        string tenantId,
        Guid anchorProcessId,
        long selectedAnchorVersion,
        int? expectedOwnerReceiptContractVersion,
        Guid? expectedOwnerReceiptId,
        string? expectedOwnerReceiptSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            WorkspaceStaffCorrelationAnonymisationSnapshot
                snapshot = await correlations.ReadAsync(
                    tenantId,
                    anchorProcessId,
                    selectedAnchorVersion,
                    cancellationToken).ConfigureAwait(false);
            if (snapshot.Status ==
                    WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                        .Eligible &&
                !string.IsNullOrWhiteSpace(snapshot.SubjectId))
            {
                WorkspaceStaffAccessCoordinationOutcome denied =
                    await accessDenier.EnsureAccessDeniedAsync(
                        tenantId,
                        snapshot.SubjectId,
                        WorkspaceStaffAccessTargetState.Departed,
                        cancellationToken).ConfigureAwait(false);
                return denied switch
                {
                    WorkspaceStaffAccessCoordinationOutcome.Allowed =>
                        ClosureResult.Complete(),
                    WorkspaceStaffAccessCoordinationOutcome
                        .OwnerProtected =>
                        ClosureResult.Blocked(OwnerProtected),
                    _ => ClosureResult.Retry(RetryRequired)
                };
            }

            if (snapshot.Status ==
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .ActiveOnboarding)
            {
                return ClosureResult.Blocked(ActiveOnboarding);
            }

            if (snapshot.Status ==
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .ActiveAccessProcess)
            {
                return ClosureResult.Blocked(ActiveAccessProcess);
            }

            WorkspaceStaffCorrelationAnonymisationTombstone?
                tombstone = await correlations.GetTombstoneAsync(
                    anchorProcessId,
                    cancellationToken).ConfigureAwait(false);
            if (tombstone is null)
            {
                return ClosureResult.Blocked(
                    snapshot.Status ==
                        WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                            .Unavailable
                        ? StateUnavailable
                        : StateConflict);
            }

            if (!tombstone.HasValidProof() ||
                tombstone.SelectedAnchorVersion !=
                    selectedAnchorVersion ||
                !MatchesExpectedReceipt(
                    tombstone,
                    expectedOwnerReceiptContractVersion,
                    expectedOwnerReceiptId,
                    expectedOwnerReceiptSha256))
            {
                return ClosureResult.Blocked(StateConflict);
            }

            WorkspaceStaffCorrelationAnonymisationSnapshot
                anonymised = await correlations.ReadAnonymisedAsync(
                    tenantId,
                    anchorProcessId,
                    tombstone.ResultingAnchorVersion,
                    tombstone.OwnerReceiptId,
                    cancellationToken).ConfigureAwait(false);
            return anonymised.Status ==
                    WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                        .Eligible &&
                anonymised.StaffMemberId ==
                    tombstone.StaffMemberId &&
                anonymised.SelectedStaffVersion ==
                    tombstone.SelectedStaffVersion &&
                string.Equals(
                    anonymised.StateSha256,
                    tombstone.ResultingStateSha256,
                    StringComparison.Ordinal)
                ? ClosureResult.Complete()
                : ClosureResult.Blocked(StateConflict);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Workspace Staff correlation access closure failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return ClosureResult.Retry(RetryRequired);
        }
    }

    private static bool MatchesExpectedReceipt(
        WorkspaceStaffCorrelationAnonymisationTombstone tombstone,
        int? expectedContractVersion,
        Guid? expectedReceiptId,
        string? expectedReceiptSha256) =>
        !expectedContractVersion.HasValue ||
        (tombstone.OwnerReceiptContractVersion ==
            expectedContractVersion.Value &&
         tombstone.OwnerReceiptId == expectedReceiptId &&
         string.Equals(
             tombstone.OwnerReceiptSha256,
             expectedReceiptSha256,
             StringComparison.Ordinal));

    private bool IsValid(
        DataRightsAnonymisationContributionRequestV2? request) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationExecutionPrerequisiteContractV2
                .CurrentVersion &&
        request.CaseType == this.CaseType &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        request.PropertyId is null &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        string.Equals(
            request.Coordinate.OwnerKey,
            this.OwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            this.RecordType,
            StringComparison.Ordinal) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0;

    private bool IsValid(
        DataRightsAnonymisationRestoreRequestV3? request) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationRestoreContractV3.CurrentVersion &&
        request.CaseType == this.CaseType &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        request.RoutingPropertyId is null &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        string.Equals(
            request.OwnerKey,
            this.OwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            this.RecordType,
            StringComparison.Ordinal) &&
        request.RecordId != Guid.Empty &&
        request.OwnerReceiptContractVersion > 0 &&
        request.OwnerReceiptId != Guid.Empty &&
        IsSha256(request.OwnerReceiptSha256) &&
        request.ResultingRecordVersion > 1;

    private static bool IsSha256(string? value) =>
        value is
        {
            Length: DataRightsAnonymisationContract.Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static
        DataRightsAnonymisationExecutionPrerequisiteResult
        ExecutionBlocked(string code) =>
        DataRightsAnonymisationExecutionPrerequisiteResult.Blocked(
            DataRightsAnonymisationExecutionPrerequisiteContractV2
                .CurrentVersion,
            code);

    private static
        DataRightsAnonymisationRestorePrerequisiteResult
        RestoreBlocked(string code) =>
        DataRightsAnonymisationRestorePrerequisiteResult.Blocked(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            code);

    private enum ClosureStatus
    {
        Completed,
        Blocked,
        RetryRequired
    }

    private sealed record ClosureResult(
        ClosureStatus Status,
        string Code)
    {
        public static ClosureResult Complete() =>
            new(ClosureStatus.Completed, string.Empty);

        public static ClosureResult Blocked(string code) =>
            new(ClosureStatus.Blocked, code);

        public static ClosureResult Retry(string code) =>
            new(ClosureStatus.RetryRequired, code);
    }
}

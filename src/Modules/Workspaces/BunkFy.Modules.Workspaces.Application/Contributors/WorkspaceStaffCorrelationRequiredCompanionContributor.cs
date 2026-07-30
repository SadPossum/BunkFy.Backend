namespace BunkFy.Modules.Workspaces.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class
    WorkspaceStaffCorrelationRequiredCompanionContributor(
        IStaffAnonymisationRestoreStateReader staffStateReader,
        IWorkspaceStaffCorrelationAnonymisationRepository correlations,
        ILogger<
            WorkspaceStaffCorrelationRequiredCompanionContributor> logger)
    : IDataRightsRequiredCompanionContributor
{
    private const string InvalidRequest =
        "Workspaces.StaffCorrelationCompanionRequestInvalid";
    private const string StaffStateUnavailable =
        "Workspaces.StaffCorrelationStaffStateUnavailable";
    private const string StaffStateConflict =
        "Workspaces.StaffCorrelationStaffStateConflict";
    private const string ActiveOnboarding =
        "Workspaces.StaffCorrelationActiveOnboarding";
    private const string ActiveAccessProcess =
        "Workspaces.StaffCorrelationActiveAccessProcess";
    private const string CorrelationUnavailable =
        "Workspaces.StaffCorrelationUnavailable";
    private const string CorrelationConflict =
        "Workspaces.StaffCorrelationConflict";
    private const string CorrelationOversized =
        "Workspaces.StaffCorrelationOversized";
    private const string RetryRequired =
        "Workspaces.StaffCorrelationRetryRequired";

    public string ContributorKey =>
        WorkspacesDataRightsCoordinates
            .StaffCorrelationCompanionContributorKey;

    public string SourceOwnerKey =>
        StaffDataRightsCoordinates.Owner;

    public string SourceRecordType =>
        StaffDataRightsCoordinates.StaffMemberRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public DataRightsOperation Operation =>
        DataRightsOperation.Anonymisation;

    public int ContractVersion =>
        DataRightsRequiredCompanionContract.CurrentVersion;

    public async Task<DataRightsRequiredCompanionResult> ExpandAsync(
        DataRightsRequiredCompanionRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return DataRightsRequiredCompanionResult.Blocked(
                InvalidRequest);
        }

        try
        {
            StaffAnonymisationRestoreState? staff =
                await staffStateReader.ReadAsync(
                    request.TenantId,
                    request.SourceCoordinate.RecordId,
                    cancellationToken).ConfigureAwait(false);
            if (staff is null)
            {
                return DataRightsRequiredCompanionResult.Blocked(
                    StaffStateUnavailable);
            }

            if (staff.StaffMemberId !=
                    request.SourceCoordinate.RecordId ||
                staff.Version !=
                    request.SourceCoordinate.RecordVersion ||
                staff.State !=
                    StaffAnonymisationRestoreRecordState.Departed)
            {
                return DataRightsRequiredCompanionResult.Blocked(
                    StaffStateConflict);
            }

            WorkspaceStaffCorrelationAnonymisationSnapshot snapshot =
                await correlations.ResolveAsync(
                    request.TenantId,
                    staff.StaffMemberId,
                    staff.Version,
                    staff.AuthSubjectId,
                    cancellationToken).ConfigureAwait(false);
            return snapshot.Status switch
            {
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .Eligible
                    when snapshot.AnchorProcessId.HasValue &&
                         snapshot.AnchorProcessVersion.HasValue =>
                    DataRightsRequiredCompanionResult.Completed(
                        [
                            new(
                                WorkspacesDataRightsCoordinates.Owner,
                                WorkspacesDataRightsCoordinates
                                    .StaffAccessProcessRecordType,
                                snapshot.AnchorProcessId.Value,
                                snapshot.AnchorProcessVersion.Value)
                        ]),
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .NoCorrelation =>
                    DataRightsRequiredCompanionResult.Completed(),
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .ActiveOnboarding =>
                    DataRightsRequiredCompanionResult.Blocked(
                        ActiveOnboarding),
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .ActiveAccessProcess =>
                    DataRightsRequiredCompanionResult.Blocked(
                        ActiveAccessProcess),
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .Oversized =>
                    DataRightsRequiredCompanionResult.Blocked(
                        CorrelationOversized),
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .Conflict =>
                    DataRightsRequiredCompanionResult.Blocked(
                        CorrelationConflict),
                _ =>
                    DataRightsRequiredCompanionResult.Blocked(
                        CorrelationUnavailable)
            };
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Workspace Staff correlation companion resolution failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return DataRightsRequiredCompanionResult.RetryRequired(
                RetryRequired);
        }
    }

    private bool IsValid(
        DataRightsRequiredCompanionRequest? request) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.CaseType == this.CaseType &&
        request.Operation == this.Operation &&
        request.PropertyId is null &&
        request.CaseId != Guid.Empty &&
        request.RemainingSubjectCapacity >= 0 &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        string.Equals(
            request.SourceCoordinate.OwnerKey,
            this.SourceOwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.SourceCoordinate.RecordType,
            this.SourceRecordType,
            StringComparison.Ordinal) &&
        request.SourceCoordinate.RecordId != Guid.Empty &&
        request.SourceCoordinate.RecordVersion > 0;
}

namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Microsoft.Extensions.Logging;

internal static class WorkspaceStaffAnonymisationAccessCodes
{
    public const string RequestInvalid =
        "Workspaces.StaffAnonymisationRequestInvalid";
    public const string StateUnavailable =
        "Workspaces.StaffAnonymisationStateUnavailable";
    public const string StateConflict =
        "Workspaces.StaffAnonymisationStateConflict";
    public const string AccessMappingUnavailable =
        "Workspaces.StaffAnonymisationAccessMappingUnavailable";
    public const string AccessMappingConflict =
        "Workspaces.StaffAnonymisationAccessMappingConflict";
    public const string OwnerProtected =
        "Workspaces.StaffAnonymisationOwnerProtected";
    public const string RetryRequired =
        "Workspaces.StaffAnonymisationAccessRetryRequired";
    public const string CorrelationReceiptInvalid =
        "Workspaces.StaffRetentionCorrelationReceiptInvalid";
    public const string CorrelationScrubRetryRequired =
        "Workspaces.StaffRetentionCorrelationScrubRetryRequired";
}

internal interface IWorkspaceStaffRetentionAccessClosure
{
    Task<WorkspaceStaffAccessClosureResult> EnsureClosedAsync(
        string tenantId,
        Guid staffMemberId,
        long selectedStaffVersion,
        CancellationToken cancellationToken);
}

internal sealed class WorkspaceStaffRetentionAccessClosure(
    IStaffAnonymisationRestoreStateReader staffStateReader,
    IWorkspaceStaffAccessProcessRepository accessProcesses,
    WorkspaceStaffAccessDenier accessDenier,
    ILogger<WorkspaceStaffRetentionAccessClosure> logger)
    : IWorkspaceStaffRetentionAccessClosure
{
    public async Task<WorkspaceStaffAccessClosureResult> EnsureClosedAsync(
        string tenantId,
        Guid staffMemberId,
        long selectedStaffVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            StaffAnonymisationRestoreState? state =
                await staffStateReader.ReadAsync(
                    tenantId,
                    staffMemberId,
                    cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                return WorkspaceStaffAccessClosureResult.Blocked(
                    WorkspaceStaffAnonymisationAccessCodes
                        .StateUnavailable);
            }

            if (state.State !=
                    StaffAnonymisationRestoreRecordState.Departed ||
                state.Version != selectedStaffVersion)
            {
                return WorkspaceStaffAccessClosureResult.Blocked(
                    WorkspaceStaffAnonymisationAccessCodes.StateConflict);
            }

            string? subjectId = NormalizeSubject(state.AuthSubjectId);
            if (subjectId is null)
            {
                return WorkspaceStaffAccessClosureResult.Complete(
                    subjectId: null);
            }

            WorkspaceStaffAccessProcess? process =
                await accessProcesses.GetCompletedDepartureAsync(
                    staffMemberId,
                    selectedStaffVersion,
                    cancellationToken).ConfigureAwait(false);
            if (process is null)
            {
                return WorkspaceStaffAccessClosureResult.Blocked(
                    WorkspaceStaffAnonymisationAccessCodes
                        .AccessMappingUnavailable);
            }

            if (!string.Equals(
                    process.ScopeId,
                    tenantId,
                    StringComparison.Ordinal) ||
                process.StaffMemberId != staffMemberId ||
                process.TargetStaffVersion != selectedStaffVersion ||
                process.TargetState !=
                    WorkspaceStaffAccessTargetState.Departed ||
                process.State != WorkspaceStaffAccessProcessState.Completed ||
                !string.Equals(
                    process.SubjectId,
                    subjectId,
                    StringComparison.Ordinal))
            {
                return WorkspaceStaffAccessClosureResult.Blocked(
                    WorkspaceStaffAnonymisationAccessCodes
                        .AccessMappingConflict);
            }

            WorkspaceStaffAccessCoordinationOutcome denied =
                await accessDenier.EnsureAccessDeniedAsync(
                    process.ScopeId,
                    process.SubjectId,
                    WorkspaceStaffAccessTargetState.Departed,
                    cancellationToken).ConfigureAwait(false);
            return denied switch
            {
                WorkspaceStaffAccessCoordinationOutcome.Allowed =>
                    WorkspaceStaffAccessClosureResult.Complete(subjectId),
                WorkspaceStaffAccessCoordinationOutcome.OwnerProtected =>
                    WorkspaceStaffAccessClosureResult.Blocked(
                        WorkspaceStaffAnonymisationAccessCodes
                            .OwnerProtected),
                _ => WorkspaceStaffAccessClosureResult.Retry(
                    WorkspaceStaffAnonymisationAccessCodes.RetryRequired)
            };
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Workspace Staff retention access closure failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return WorkspaceStaffAccessClosureResult.Retry(
                WorkspaceStaffAnonymisationAccessCodes.RetryRequired);
        }
    }

    private static string? NormalizeSubject(string? subjectId)
    {
        string value = subjectId?.Trim() ?? string.Empty;
        return value.Length == 0 ? null : value;
    }
}

internal enum WorkspaceStaffAccessClosureStatus
{
    Completed,
    Blocked,
    RetryRequired
}

internal sealed record WorkspaceStaffAccessClosureResult(
    WorkspaceStaffAccessClosureStatus Status,
    string Code,
    string? SubjectId)
{
    public static WorkspaceStaffAccessClosureResult Complete(
        string? subjectId) =>
        new(
            WorkspaceStaffAccessClosureStatus.Completed,
            string.Empty,
            subjectId);

    public static WorkspaceStaffAccessClosureResult Blocked(string code) =>
        new(
            WorkspaceStaffAccessClosureStatus.Blocked,
            code,
            SubjectId: null);

    public static WorkspaceStaffAccessClosureResult Retry(string code) =>
        new(
            WorkspaceStaffAccessClosureStatus.RetryRequired,
            code,
            SubjectId: null);
}

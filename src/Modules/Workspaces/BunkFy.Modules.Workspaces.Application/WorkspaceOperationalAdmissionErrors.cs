namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Results;

public static class WorkspaceOperationalAdmissionErrors
{
    public static readonly Error ProcessingRestricted = new(
        "Workspaces.OperationalProcessingRestricted",
        "The workspace is not accepting ordinary operations.");

    public static readonly Error AdmissionUnavailable = new(
        "Workspaces.OperationalAdmissionUnavailable",
        "Workspace operational admission is temporarily unavailable.");
}

internal static class WorkspaceOperationalAdmissionGuard
{
    public static Result RequireAllowed(
        WorkspaceOperationalAdmissionDecision decision) =>
        decision.Outcome switch
        {
            WorkspaceOperationalAdmissionOutcome.Allowed => Result.Success(),
            WorkspaceOperationalAdmissionOutcome.Restricted => Result.Failure(
                    WorkspaceOperationalAdmissionErrors.ProcessingRestricted),
            _ => Result.Failure(
                WorkspaceOperationalAdmissionErrors.AdmissionUnavailable)
        };
}

namespace BunkFy.Modules.Retention.Application.Errors;

using Gma.Framework.Results;

public static class RetentionApplicationErrors
{
    public static readonly Error TargetUnavailable = new(
        "Retention.TargetUnavailable",
        "The retention target is not active and governed.");
    public static readonly Error ExecutionConflict = new(
        "Retention.ExecutionConflict",
        "The retention execution is bound to different coordinates.");
    public static readonly Error ExecutionNotFound = new(
        "Retention.ExecutionNotFound",
        "The retention execution was not found.");
    public static readonly Error TaskRunUnavailable = new(
        "Retention.TaskRunUnavailable",
        "The task run is not a retention execution for the current tenant.");
    public static readonly Error WorkspaceProcessingRestricted = new(
        "Retention.WorkspaceProcessingRestricted",
        "The workspace is not accepting operational changes.");
    public static readonly Error WorkspaceProcessingAdmissionUnavailable = new(
        "Retention.WorkspaceProcessingAdmissionUnavailable",
        "Workspace processing admission is temporarily unavailable.");
}

namespace BunkFy.Modules.Workspaces.Contracts;

public interface IWorkspaceOperationalAdmissionPolicy
{
    ValueTask<WorkspaceOperationalAdmissionDecision> EvaluateAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceOperationalAdmissionDecision(
    WorkspaceOperationalAdmissionOutcome Outcome)
{
    public static WorkspaceOperationalAdmissionDecision Allowed { get; } =
        new(WorkspaceOperationalAdmissionOutcome.Allowed);

    public static WorkspaceOperationalAdmissionDecision Restricted { get; } =
        new(WorkspaceOperationalAdmissionOutcome.Restricted);

    public static WorkspaceOperationalAdmissionDecision Unavailable { get; } =
        new(WorkspaceOperationalAdmissionOutcome.Unavailable);
}

public enum WorkspaceOperationalAdmissionOutcome
{
    Unknown = 0,
    Allowed = 1,
    Restricted = 2,
    Unavailable = 3
}

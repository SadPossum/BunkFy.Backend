namespace BunkFy.Extensions.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Contracts;

internal sealed class StubWorkspaceOperationalAdmissionPolicy(
    WorkspaceOperationalAdmissionOutcome outcome =
        WorkspaceOperationalAdmissionOutcome.Allowed)
    : IWorkspaceOperationalAdmissionPolicy
{
    public List<string> TenantIds { get; } = [];

    public ValueTask<WorkspaceOperationalAdmissionDecision> EvaluateAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        this.TenantIds.Add(tenantId);
        return ValueTask.FromResult(new WorkspaceOperationalAdmissionDecision(outcome));
    }
}

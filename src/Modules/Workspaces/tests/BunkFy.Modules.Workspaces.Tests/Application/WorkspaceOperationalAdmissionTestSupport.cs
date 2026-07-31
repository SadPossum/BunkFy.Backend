namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Logging.Abstractions;

internal static class WorkspaceOperationalAdmissionTestSupport
{
    public static WorkspaceOperationalAdmissionEvaluator Allowed(string tenantId) =>
        Create(tenantId, new SnapshotReader(null));

    public static WorkspaceOperationalAdmissionEvaluator Restricted(string tenantId) =>
        Create(
            tenantId,
            new SnapshotReader(new WorkspaceTerminationFenceSnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                WorkspaceTerminationFenceState.Frozen,
                1)));

    public static WorkspaceOperationalAdmissionEvaluator Unavailable(string tenantId) =>
        Create(tenantId, new ThrowingReader());

    public static WorkspaceOperationalAdmissionEvaluator Create(
        string tenantId,
        IWorkspaceTerminationFenceReader reader,
        bool scopeEnabled = true) =>
        new(
            reader,
            new TestScopeContext(scopeEnabled, tenantId),
            NullLogger<WorkspaceOperationalAdmissionEvaluator>.Instance);

    private sealed class SnapshotReader(
        WorkspaceTerminationFenceSnapshot? snapshot)
        : IWorkspaceTerminationFenceReader
    {
        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class ThrowingReader : IWorkspaceTerminationFenceReader
    {
        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unavailable");
    }

    private sealed class TestScopeContext(
        bool isEnabled,
        string scopeId) : IScopeContext
    {
        public bool IsEnabled => isEnabled;
        public string ScopeId => scopeId;
    }
}

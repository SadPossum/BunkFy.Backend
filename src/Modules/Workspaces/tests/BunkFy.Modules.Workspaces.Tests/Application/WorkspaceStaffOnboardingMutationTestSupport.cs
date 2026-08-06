namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;

internal static class WorkspaceStaffOnboardingMutationTestSupport
{
    public static WorkspaceStaffOnboardingMutationCoordinator Create(
        IWorkspaceStaffOnboardingRepository applications,
        IWorkspaceStaffOnboardingOperationLock? operationLock = null) =>
        new(operationLock ?? new NoOpOperationLock(), applications);

    public static WorkspaceStaffOnboardingMutationCoordinator
        CreateForSourceOnly() => new(new NoOpOperationLock(), null!);

    private sealed class NoOpOperationLock
        : IWorkspaceStaffOnboardingOperationLock
    {
        public Task AcquireSourceReadAsync(
            Guid sourceId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AcquireSourceWriteAsync(
            Guid sourceId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AcquireApplicantAsync(
            Guid sourceId,
            string subjectId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> TryAcquireAsync(
            Guid applicationId,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }
}

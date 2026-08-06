namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Pagination;

internal static class WorkspaceStaffAccessMutationTestSupport
{
    public static WorkspaceStaffAccessMutationCoordinator Create(
        IWorkspaceStaffAccessProcessRepository? processes = null,
        bool processExists = true) => new(
        new NoOpOperationLock(processExists),
        processes ?? ThrowingProcessRepository.Instance);

    private sealed class NoOpOperationLock(bool processExists)
        : IWorkspaceStaffAccessOperationLock
    {
        public Task AcquireStaffAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> TryAcquireProcessAsync(
            Guid processId,
            CancellationToken cancellationToken) =>
            Task.FromResult(processExists);
    }

    private sealed class ThrowingProcessRepository
        : IWorkspaceStaffAccessProcessRepository
    {
        public static ThrowingProcessRepository Instance { get; } = new();

        public Task<WorkspaceStaffAccessProcess?> GetAsync(
            Guid processId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffAccessProcess?> GetByStaffVersionAsync(
            Guid staffMemberId,
            long targetStaffVersion,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffAccessProcess?> GetOpenByStaffAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffAccessProcess?>
            GetLatestCompletedSuspensionAsync(
                Guid staffMemberId,
                string subjectId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffAccessProcess?> GetCompletedDepartureAsync(
            Guid staffMemberId,
            long targetStaffVersion,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffAccessProcessListResponse> ListOpenAsync(
            PageRequest page,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(
            WorkspaceStaffAccessProcess process,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

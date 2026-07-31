namespace Integration.Tests.Tasks;

using System.Text.Json;
using BunkFy.Host.Worker;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceTerminationTaskAdmissionTests
{
    private static readonly Guid ProcessId = Guid.NewGuid();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Active_fence_allows_only_matching_termination_task()
    {
        WorkspaceTerminationTaskExecutionContextContributor contributor =
            new(
                new StubReader(
                    new WorkspaceTerminationFenceSnapshot(
                        ProcessId,
                        Guid.NewGuid(),
                        WorkspaceTerminationFenceState.Frozen,
                        1)),
                NullLogger<
                    WorkspaceTerminationTaskExecutionContextContributor>
                    .Instance);
        TaskExecutionContextPreparationContext matching =
            CreateContext(ProcessId);
        TaskExecutionContextPreparationContext wrong =
            CreateContext(Guid.NewGuid());

        Assert.True((await contributor.PrepareAsync(
            matching,
            default)).IsSuccess);
        Assert.True((await contributor.PrepareAsync(
            wrong,
            default)).IsFailure);
    }

    [Fact]
    public async Task Open_workspace_allows_tenant_task()
    {
        WorkspaceTerminationTaskExecutionContextContributor contributor =
            new(
                new StubReader(null),
                NullLogger<
                    WorkspaceTerminationTaskExecutionContextContributor>
                    .Instance);

        Assert.True((await contributor.PrepareAsync(
            CreateContext(ProcessId),
            default)).IsSuccess);
    }

    [Fact]
    public async Task Fence_reader_failure_denies_tenant_task()
    {
        WorkspaceTerminationTaskExecutionContextContributor contributor =
            new(
                new ThrowingReader(),
                NullLogger<
                    WorkspaceTerminationTaskExecutionContextContributor>
                    .Instance);

        Assert.True((await contributor.PrepareAsync(
            CreateContext(ProcessId),
            default)).IsFailure);
    }

    [Fact]
    public async Task Global_task_does_not_read_the_tenant_fence()
    {
        WorkspaceTerminationTaskExecutionContextContributor contributor =
            new(
                new ThrowingReader(),
                NullLogger<
                    WorkspaceTerminationTaskExecutionContextContributor>
                    .Instance);
        TaskExecutionContextPreparationContext context =
            CreateContext(ProcessId, tenantScoped: false);

        Assert.True((await contributor.PrepareAsync(
            context,
            default)).IsSuccess);
    }

    [Fact]
    public async Task Invalid_fence_snapshot_denies_termination_task()
    {
        WorkspaceTerminationTaskExecutionContextContributor contributor =
            new(
                new StubReader(
                    new WorkspaceTerminationFenceSnapshot(
                        Guid.Empty,
                        Guid.NewGuid(),
                        WorkspaceTerminationFenceState.Frozen,
                        1)),
                NullLogger<
                    WorkspaceTerminationTaskExecutionContextContributor>
                    .Instance);

        Assert.True((await contributor.PrepareAsync(
            CreateContext(Guid.Empty),
            default)).IsFailure);
    }

    private static TaskExecutionContextPreparationContext CreateContext(
        Guid processId,
        bool tenantScoped = true)
    {
        ExecuteTenantTerminationOwnerWorkPayload payload = new(
            processId,
            Guid.NewGuid(),
            1,
            TenantTerminationContributionPhase.Freeze,
            "workspaces");
        TaskHandlerRegistration registration =
            tenantScoped
                ? TaskHandlerRegistration.Create<
                    ExecuteTenantTerminationOwnerWorkPayload,
                    StubHandler>(DataRightsModuleMetadata.Name)
                : TaskHandlerRegistration.Create<
                    GlobalPayload,
                    GlobalHandler>(DataRightsModuleMetadata.Name);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TaskRunLease lease = new(
            Guid.NewGuid(),
            DataRightsModuleMetadata.Name,
            ExecuteTenantTerminationOwnerWorkPayload.TaskName,
            DataRightsModuleMetadata.TenantTerminationWorkerGroup,
            "worker-1",
            "node-1",
            JsonSerializer.Serialize(
                payload,
                JsonOptions),
            1,
            now,
            now.AddMinutes(1),
            "tenant-a",
            payloadVersion:
                ExecuteTenantTerminationOwnerWorkPayload.PayloadVersion);
        return new(
            lease,
            registration,
            lease.CreateExecutionContext());
    }

    private sealed class StubReader(
        WorkspaceTerminationFenceSnapshot? snapshot)
        : IWorkspaceTerminationFenceReader
    {
        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class ThrowingReader
        : IWorkspaceTerminationFenceReader
    {
        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unavailable");
    }

    private sealed class StubHandler
        : ITaskHandler<ExecuteTenantTerminationOwnerWorkPayload>
    {
        public Task HandleAsync(
            ExecuteTenantTerminationOwnerWorkPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [TaskName("global-test")]
    [TaskPayloadVersion(1)]
    [TaskDescription("Exercise global task admission.")]
    [TaskKind(ModuleTaskKind.OneShot)]
    private sealed record GlobalPayload : ITaskPayload;

    private sealed class GlobalHandler : ITaskHandler<GlobalPayload>
    {
        public Task HandleAsync(
            GlobalPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

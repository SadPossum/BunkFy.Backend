namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Api;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceTerminationEndpointAccessPolicyTests
{
    private static readonly Guid ProcessId = Guid.NewGuid();
    private static readonly Guid Epoch = Guid.NewGuid();

    [Fact]
    public async Task Ordinary_endpoint_is_locked_while_fence_is_active()
    {
        WorkspaceTerminationEndpointAccessPolicy policy = CreatePolicy(
            WorkspaceTerminationFenceState.Frozen);
        DefaultHttpContext context = new();

        TenantEndpointAccessDecision decision =
            await policy.AuthorizeAsync(context, "tenant-a", default);

        Assert.False(decision.IsAllowed);
        Assert.Equal(StatusCodes.Status423Locked, decision.StatusCode);
    }

    [Fact]
    public async Task Exact_frozen_process_review_endpoint_is_allowed()
    {
        WorkspaceTerminationEndpointAccessPolicy policy = CreatePolicy(
            WorkspaceTerminationFenceState.Frozen);
        DefaultHttpContext context = CreateTerminationContext(
            ProcessId,
            WorkspaceTerminationAccessPurpose.Review);

        TenantEndpointAccessDecision decision =
            await policy.AuthorizeAsync(context, "tenant-a", default);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task Wrong_process_or_post_destroy_review_is_locked()
    {
        WorkspaceTerminationEndpointAccessPolicy frozenPolicy = CreatePolicy(
            WorkspaceTerminationFenceState.Frozen);
        DefaultHttpContext wrongProcess = CreateTerminationContext(
            Guid.NewGuid(),
            WorkspaceTerminationAccessPurpose.Review);
        Assert.False((await frozenPolicy.AuthorizeAsync(
            wrongProcess,
            "tenant-a",
            default)).IsAllowed);

        WorkspaceTerminationEndpointAccessPolicy destroyingPolicy =
            CreatePolicy(
                WorkspaceTerminationFenceState.DestructionStarted);
        DefaultHttpContext review = CreateTerminationContext(
            ProcessId,
            WorkspaceTerminationAccessPurpose.Review);
        Assert.False((await destroyingPolicy.AuthorizeAsync(
            review,
            "tenant-a",
            default)).IsAllowed);

        DefaultHttpContext recovery = CreateTerminationContext(
            ProcessId,
            WorkspaceTerminationAccessPurpose.Recovery);
        Assert.True((await destroyingPolicy.AuthorizeAsync(
            recovery,
            "tenant-a",
            default)).IsAllowed);
    }

    [Fact]
    public async Task Reader_failure_denies_with_service_unavailable()
    {
        WorkspaceTerminationEndpointAccessPolicy policy = new(
            new ThrowingReader(),
            NullLogger<WorkspaceTerminationEndpointAccessPolicy>.Instance);

        TenantEndpointAccessDecision decision = await policy.AuthorizeAsync(
            new DefaultHttpContext(),
            "tenant-a",
            default);

        Assert.False(decision.IsAllowed);
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            decision.StatusCode);
    }

    [Fact]
    public async Task Invalid_snapshot_denies_with_service_unavailable()
    {
        WorkspaceTerminationEndpointAccessPolicy policy = new(
            new StubReader(
                new WorkspaceTerminationFenceSnapshot(
                    Guid.Empty,
                    Epoch,
                    WorkspaceTerminationFenceState.Frozen,
                    1)),
            NullLogger<WorkspaceTerminationEndpointAccessPolicy>.Instance);

        TenantEndpointAccessDecision decision = await policy.AuthorizeAsync(
            CreateTerminationContext(
                ProcessId,
                WorkspaceTerminationAccessPurpose.Recovery),
            "tenant-a",
            default);

        Assert.False(decision.IsAllowed);
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            decision.StatusCode);
    }

    private static WorkspaceTerminationEndpointAccessPolicy CreatePolicy(
        WorkspaceTerminationFenceState state) =>
        new(
            new StubReader(
                new WorkspaceTerminationFenceSnapshot(
                    ProcessId,
                    Epoch,
                    state,
                    1)),
            NullLogger<WorkspaceTerminationEndpointAccessPolicy>.Instance);

    private static DefaultHttpContext CreateTerminationContext(
        Guid processId,
        WorkspaceTerminationAccessPurpose purpose)
    {
        DefaultHttpContext context = new();
        context.Request.RouteValues["processId"] =
            processId.ToString("D");
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new ModuleEndpointMetadata(
                    DataRightsModuleMetadata.Name),
                new WorkspaceTerminationAccessRequirement(purpose)),
            "termination-test"));
        return context;
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
}

namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceLifecycleAdmissionPolicyTests
{
    private const string TenantId =
        "9a8f9c94-2dd7-42b4-9910-b00cb92f2a98";

    [Fact]
    public async Task Active_fence_restricts_ingestion_and_property_activation()
    {
        StubReader reader = new(
            new WorkspaceTerminationFenceSnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                WorkspaceTerminationFenceState.Frozen,
                1));
        WorkspaceIngestionTenantLifecyclePolicy ingestion = new(
            reader,
            new TestScopeContext(),
            NullLogger<WorkspaceIngestionTenantLifecyclePolicy>.Instance);
        WorkspacePropertyProcessingLifecyclePolicy properties = new(
            reader,
            new TestScopeContext(),
            NullLogger<WorkspacePropertyProcessingLifecyclePolicy>.Instance);

        IngestionTenantLifecycleDecision ingestionDecision =
            await ingestion.AuthorizeAsync(
                TenantId,
                IngestionTenantLifecycleOperation.AdapterIngress);
        PropertyProcessingLifecycleDecision propertyDecision =
            await properties.AuthorizeActivationAsync(
                TenantId,
                Guid.NewGuid());

        Assert.Equal(
            IngestionTenantLifecycleOutcome.Restricted,
            ingestionDecision.Outcome);
        Assert.Equal(
            PropertyProcessingLifecycleOutcome.Restricted,
            propertyDecision.Outcome);
    }

    [Fact]
    public async Task Missing_fence_allows_and_reader_failure_is_unavailable()
    {
        WorkspaceIngestionTenantLifecyclePolicy open = new(
            new StubReader(null),
            new TestScopeContext(),
            NullLogger<WorkspaceIngestionTenantLifecyclePolicy>.Instance);
        Assert.Equal(
            IngestionTenantLifecycleOutcome.Allowed,
            (await open.AuthorizeAsync(
                TenantId,
                IngestionTenantLifecycleOperation.AdapterRunStart)).Outcome);

        WorkspaceIngestionTenantLifecyclePolicy unavailable = new(
            new ThrowingReader(),
            new TestScopeContext(),
            NullLogger<WorkspaceIngestionTenantLifecyclePolicy>.Instance);
        Assert.Equal(
            IngestionTenantLifecycleOutcome.Unavailable,
            (await unavailable.AuthorizeAsync(
                TenantId,
                IngestionTenantLifecycleOperation.AdapterIngress)).Outcome);
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

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }
}

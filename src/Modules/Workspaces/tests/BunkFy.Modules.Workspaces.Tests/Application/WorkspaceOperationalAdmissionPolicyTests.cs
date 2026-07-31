namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceOperationalAdmissionPolicyTests
{
    private const string TenantId =
        "1a369b88-8a6a-46ed-a774-7f6302f1f472";

    [Theory]
    [InlineData(false, TenantId)]
    [InlineData(true, "46f43a5c-26e9-4eae-9ba6-dc80d05cf3f1")]
    [InlineData(true, "not-a-tenant")]
    public async Task Missing_or_mismatched_scope_fails_unavailable_without_storage(
        bool scopeEnabled,
        string requestedTenant)
    {
        RecordingReader reader = new(null);
        WorkspaceOperationalAdmissionEvaluator evaluator =
            WorkspaceOperationalAdmissionTestSupport.Create(
                TenantId,
                reader,
                scopeEnabled);

        WorkspaceOperationalAdmissionDecision decision =
            await evaluator.EvaluateAsync(requestedTenant);

        Assert.Equal(
            WorkspaceOperationalAdmissionOutcome.Unavailable,
            decision.Outcome);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task Malformed_fence_fails_unavailable()
    {
        RecordingReader reader = new(new WorkspaceTerminationFenceSnapshot(
            Guid.Empty,
            Guid.NewGuid(),
            WorkspaceTerminationFenceState.Frozen,
            1));
        WorkspaceOperationalAdmissionEvaluator evaluator =
            WorkspaceOperationalAdmissionTestSupport.Create(TenantId, reader);

        WorkspaceOperationalAdmissionDecision decision =
            await evaluator.EvaluateAsync(TenantId);

        Assert.Equal(
            WorkspaceOperationalAdmissionOutcome.Unavailable,
            decision.Outcome);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task Canonical_tenant_variants_use_the_same_authoritative_scope()
    {
        RecordingReader reader = new(null);
        WorkspaceOperationalAdmissionEvaluator evaluator =
            WorkspaceOperationalAdmissionTestSupport.Create(TenantId, reader);

        WorkspaceOperationalAdmissionDecision decision =
            await evaluator.EvaluateAsync(TenantId.ToUpperInvariant());

        Assert.Equal(
            WorkspaceOperationalAdmissionOutcome.Allowed,
            decision.Outcome);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task Coordinator_opens_the_requested_authoritative_scope()
    {
        WorkspaceOperationalAdmissionEvaluator evaluator =
            WorkspaceOperationalAdmissionTestSupport.Allowed(TenantId);
        ServiceCollection services = new();
        services.AddSingleton(evaluator);
        using ServiceProvider provider = services.BuildServiceProvider();
        RecordingAuthoritativeScope authoritativeScope = new(provider);
        WorkspaceOperationalAdmissionPolicy policy = new(
            authoritativeScope,
            NullLogger<WorkspaceOperationalAdmissionPolicy>.Instance);

        WorkspaceOperationalAdmissionDecision decision =
            await policy.EvaluateAsync(TenantId);

        Assert.Equal(
            WorkspaceOperationalAdmissionOutcome.Allowed,
            decision.Outcome);
        Assert.Equal(Guid.Parse(TenantId), authoritativeScope.OrganizationId);
    }

    private sealed class RecordingReader(
        WorkspaceTerminationFenceSnapshot? snapshot)
        : IWorkspaceTerminationFenceReader
    {
        public int CallCount { get; private set; }

        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default)
        {
            this.CallCount++;
            return Task.FromResult(snapshot);
        }
    }

    private sealed class RecordingAuthoritativeScope(
        IServiceProvider services) : IWorkspaceAuthoritativeScope
    {
        public Guid OrganizationId { get; private set; }

        public Task<TResult> RunAsync<TResult>(
            Guid organizationId,
            Func<IServiceProvider, Task<TResult>> operation)
        {
            this.OrganizationId = organizationId;
            return operation(services);
        }
    }
}

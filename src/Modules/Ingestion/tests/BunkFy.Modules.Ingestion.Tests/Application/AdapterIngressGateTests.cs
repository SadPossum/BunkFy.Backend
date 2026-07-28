namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Application.Ingress;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Controls;
using Gma.Framework.RateLimiting;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterIngressGateTests
{
    private static readonly AdapterIngressIdentity Identity = new(
        "tenant-a",
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        AdapterExecutionMode.Push,
        "fake.http",
        1,
        1,
        "fake.http",
        "user:owner");

    [Fact]
    public async Task Tenant_suspension_denies_without_consuming_quota()
    {
        RecordingLimiter limiter = new();
        AdapterIngressGate gate = CreateGate(
            limiter,
            new AdapterIngressControlSnapshot(IsTenantSuspended: true, IsGlobalStopped: false));

        AdapterIngressGateDecision decision = await gate.AdmitAsync(
            Identity,
            AdapterIngressOperation.Observation,
            permitCount: 1,
            consumeQuota: true,
            CancellationToken.None);

        Assert.Equal(AdapterIngressGateOutcome.PolicyRejected, decision.Outcome);
        Assert.Equal(AdapterIngressPolicyRejection.TenantSuspended, decision.PolicyRejection);
        Assert.Empty(limiter.Requests);
    }

    [Fact]
    public async Task Global_stop_takes_precedence_over_tenant_state()
    {
        RecordingLimiter limiter = new();
        AdapterIngressGate gate = CreateGate(
            limiter,
            new AdapterIngressControlSnapshot(IsTenantSuspended: true, IsGlobalStopped: true));

        AdapterIngressGateDecision decision = await gate.AdmitAsync(
            Identity,
            AdapterIngressOperation.RemoteLeaseClaim,
            permitCount: 1,
            consumeQuota: true,
            CancellationToken.None);

        Assert.Equal(AdapterIngressGateOutcome.PolicyRejected, decision.Outcome);
        Assert.Equal(AdapterIngressPolicyRejection.GlobalStopped, decision.PolicyRejection);
        Assert.Empty(limiter.Requests);
    }

    [Fact]
    public async Task Unavailable_control_store_denies_without_touching_the_limiter()
    {
        RecordingLimiter limiter = new();
        AdapterIngressGate gate = CreateGate(limiter, snapshot: null);

        AdapterIngressGateDecision decision = await gate.AdmitAsync(
            Identity,
            AdapterIngressOperation.RemoteLeaseRenew,
            permitCount: 1,
            consumeQuota: true,
            CancellationToken.None);

        Assert.Equal(AdapterIngressGateOutcome.ProviderUnavailable, decision.Outcome);
        Assert.Empty(limiter.Requests);
    }

    [Fact]
    public async Task Scope_mismatch_denies_before_control_or_quota_access()
    {
        RecordingLimiter limiter = new();
        TestControlRepository controls = new(new AdapterIngressControlSnapshot(false, false));
        AdapterIngressGate gate = CreateGate(
            limiter,
            controls,
            new TestScopeContext("tenant-b"));

        AdapterIngressGateDecision decision = await gate.AdmitAsync(
            Identity,
            AdapterIngressOperation.Observation,
            permitCount: 1,
            consumeQuota: true,
            CancellationToken.None);

        Assert.Equal(AdapterIngressGateOutcome.PolicyRejected, decision.Outcome);
        Assert.Equal(AdapterIngressPolicyRejection.ScopeMismatch, decision.PolicyRejection);
        Assert.Equal(0, controls.AdmissionReadCount);
        Assert.Empty(limiter.Requests);
    }

    [Fact]
    public async Task Provider_unavailability_is_returned_fail_closed()
    {
        RecordingLimiter limiter = new(MultiPartitionRateLimitDecision.ProviderUnavailable());
        AdapterIngressGate gate = CreateGate(
            limiter,
            new AdapterIngressControlSnapshot(false, false));

        AdapterIngressGateDecision decision = await gate.AdmitAsync(
            Identity,
            AdapterIngressOperation.RemoteLeaseComplete,
            permitCount: 1,
            consumeQuota: true,
            CancellationToken.None);

        Assert.Equal(AdapterIngressGateOutcome.ProviderUnavailable, decision.Outcome);
        Assert.Single(limiter.Requests);
    }

    [Fact]
    public async Task Quota_request_combines_credential_and_tenant_partitions_without_raw_ids()
    {
        RecordingLimiter limiter = new();
        AdapterIngressGate gate = CreateGate(
            limiter,
            new AdapterIngressControlSnapshot(false, false));

        AdapterIngressGateDecision decision = await gate.AdmitAsync(
            Identity,
            AdapterIngressOperation.Observation,
            permitCount: 7,
            consumeQuota: true,
            CancellationToken.None);

        Assert.Equal(AdapterIngressGateOutcome.Allowed, decision.Outcome);
        MultiPartitionRateLimitRequest request = Assert.Single(limiter.Requests);
        Assert.Equal(7, request.PermitCount);
        Assert.Equal(4, request.Partitions.Count);
        Assert.StartsWith("bunkfy-adapter-ingress-", request.AtomicGroup, StringComparison.Ordinal);
        Assert.DoesNotContain(Identity.ScopeId, request.AtomicGroup, StringComparison.Ordinal);
        Assert.DoesNotContain(Identity.CredentialId.ToString("N"), request.AtomicGroup, StringComparison.Ordinal);
        Assert.Contains(request.Partitions, partition => partition.Identity == "tenant-minute");
        Assert.Contains(request.Partitions, partition => partition.Identity == "tenant-hour");
        Assert.All(
            request.Partitions,
            partition =>
            {
                Assert.DoesNotContain(Identity.ScopeId, partition.Identity, StringComparison.Ordinal);
                Assert.DoesNotContain(
                    Identity.CredentialId.ToString("N"),
                    partition.Identity,
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task Replay_check_is_allowed_without_consuming_quota()
    {
        RecordingLimiter limiter = new();
        AdapterIngressGate gate = CreateGate(
            limiter,
            new AdapterIngressControlSnapshot(false, false));

        AdapterIngressGateDecision decision = await gate.AdmitAsync(
            Identity,
            AdapterIngressOperation.Observation,
            permitCount: 1,
            consumeQuota: false,
            CancellationToken.None);

        Assert.Equal(AdapterIngressGateOutcome.Allowed, decision.Outcome);
        Assert.Empty(limiter.Requests);
    }

    private static AdapterIngressGate CreateGate(
        RecordingLimiter limiter,
        AdapterIngressControlSnapshot? snapshot) =>
        CreateGate(
            limiter,
            new TestControlRepository(snapshot),
            new TestScopeContext(Identity.ScopeId));

    private static AdapterIngressGate CreateGate(
        RecordingLimiter limiter,
        TestControlRepository controls,
        TestScopeContext scopeContext) =>
        new(
            controls,
            limiter,
            Options.Create(new AdapterIngressQuotaOptions()),
            scopeContext);

    private sealed class RecordingLimiter(
        MultiPartitionRateLimitDecision? decision = null)
        : IMultiPartitionRateLimiter
    {
        public List<MultiPartitionRateLimitRequest> Requests { get; } = [];

        public ValueTask<MultiPartitionRateLimitDecision> AcquireAsync(
            MultiPartitionRateLimitRequest request,
            CancellationToken cancellationToken = default)
        {
            this.Requests.Add(request);
            return ValueTask.FromResult(decision ?? MultiPartitionRateLimitDecision.Acquired());
        }
    }

    private sealed class TestControlRepository(AdapterIngressControlSnapshot? snapshot)
        : IAdapterIngressControlRepository
    {
        public int AdmissionReadCount { get; private set; }

        public Task<AdapterIngressTenantControl?> GetTenantAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AdapterIngressGlobalControl?> GetGlobalAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AdapterIngressControlSnapshot?> ReadAdmissionAsync(CancellationToken cancellationToken)
        {
            this.AdmissionReadCount++;
            return Task.FromResult(snapshot);
        }

        public void Add(AdapterIngressTenantControl control) => throw new NotSupportedException();
        public void Add(AdapterIngressGlobalControl control) => throw new NotSupportedException();
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}

namespace BunkFy.Modules.Retention.Tests.Application;

using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Application.Tasks;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Tasks;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionScheduleProviderTests
{
    [Fact]
    public async Task Schedules_are_deterministic_and_tenant_scoped()
    {
        FakeScopeRepository scopes = new(
            new RetentionScheduleTarget("tenant-b", null),
            new RetentionScheduleTarget("tenant-a", null));
        RetentionScheduleProvider provider = new(
            scopes,
            [new Contributor("raw-source-evidence")]);

        IReadOnlyList<ScheduledTaskDefinition> first = await provider
            .GetSchedulesAsync(CancellationToken.None)
            .ToArrayAsync(CancellationToken.None);
        IReadOnlyList<ScheduledTaskDefinition> second = await provider
            .GetSchedulesAsync(CancellationToken.None)
            .ToArrayAsync(CancellationToken.None);

        Assert.Equal(
            first.Select(schedule => schedule.ScheduleName),
            second.Select(schedule => schedule.ScheduleName));
        Assert.Equal(["tenant-b", "tenant-a"], first.Select(schedule => schedule.ScopeId));
        Assert.Equal(2, first.Select(schedule => schedule.ScheduleName).Distinct().Count());
        Assert.All(first, schedule =>
        {
            Assert.True(schedule.RunOnStart);
            Assert.Equal(3, schedule.MaxAttempts);
            Assert.Equal(TimeSpan.FromHours(1), schedule.Interval);
        });
    }

    [Fact]
    public async Task Duplicate_contributor_coordinate_fails_closed()
    {
        RetentionScheduleProvider provider = new(
            new FakeScopeRepository(new RetentionScheduleTarget("tenant-a", null)),
            [
                new Contributor("raw-source-evidence"),
                new Contributor("raw-source-evidence")
            ]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetSchedulesAsync(CancellationToken.None)
                .ToArrayAsync(CancellationToken.None));

        Assert.Equal("Retention.ContributorDescriptorDuplicate", exception.Message);
    }

    private sealed class Contributor(string dataClassKey)
        : IRetentionExecutionContributor
    {
        public RetentionScheduleDescriptor Schedule { get; } = new(
            "ingestion",
            dataClassKey,
            RetentionTargetScopeKind.Tenant,
            executionPolicyVersion: 1,
            TimeSpan.FromHours(1),
            maxAttempts: 3,
            executionTimeout: TimeSpan.FromMinutes(15));

        public Task<RetentionContributionResult> ExecuteAsync(
            RetentionContributionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeScopeRepository(
        params RetentionScheduleTarget[] tenantTargets)
        : IRetentionScopeRepository
    {
        public Task<IReadOnlyList<RetentionScheduleTarget>> ListActiveTargetsAsync(
            RetentionTargetScopeKind targetKind,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetentionScheduleTarget>>(
                targetKind == RetentionTargetScopeKind.Tenant ? tenantTargets : []);

        public Task ApplyOrganizationAsync(
            RetentionOrganizationWriteModel organization,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ApplyPropertyTopologyAsync(
            RetentionPropertyTopologyWriteModel property,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ApplyPropertyPolicyAsync(
            RetentionPropertyPolicyWriteModel property,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RetentionScheduleTarget>> ListCurrentActiveTargetsAsync(
            RetentionTargetScopeKind targetKind,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsActiveTargetAsync(
            RetentionTargetScopeKind targetKind,
            Guid? propertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

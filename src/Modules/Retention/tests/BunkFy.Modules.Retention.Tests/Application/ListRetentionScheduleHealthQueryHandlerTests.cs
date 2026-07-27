namespace BunkFy.Modules.Retention.Tests.Application;

using BunkFy.Modules.Retention.Application.Handlers;
using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Application.Queries;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Domain.Models;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ListRetentionScheduleHealthQueryHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Overdue_schedule_is_prioritized_ahead_of_never_run_schedule()
    {
        Guid propertyId = Guid.NewGuid();
        RetentionScheduleDescriptor overdue = Descriptor("raw-source-evidence");
        RetentionScheduleDescriptor neverRun = Descriptor("sensitive-history");
        ListRetentionScheduleHealthQueryHandler handler = new(
            new FakeScopeRepository(new("tenant-a", propertyId)),
            new FakeHealthReader(new RetentionScheduleStateSnapshot(
                overdue.OwnerKey,
                overdue.DataClassKey,
                propertyId,
                overdue.ExecutionPolicyVersion,
                (int)RetentionExecutionState.Failed,
                Now.AddHours(-2),
                Now.AddHours(-2).AddMinutes(1),
                Now.AddHours(-1),
                2,
                10,
                0,
                10,
                "owner-timeout",
                null)),
            [
                new Contributor(neverRun),
                new Contributor(overdue)
            ],
            new TestClock());

        Result<RetentionScheduleHealthListResponse> result =
            await handler.HandleAsync(
                new ListRetentionScheduleHealthQuery(),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Value.Items,
            item =>
            {
                Assert.Equal(overdue.DataClassKey, item.DataClassKey);
                Assert.Equal(RetentionExecutionStatus.Failed, item.Status);
                Assert.True(item.Overdue);
                Assert.Equal(2, item.ConsecutiveFailures);
            },
            item =>
            {
                Assert.Equal(neverRun.DataClassKey, item.DataClassKey);
                Assert.Equal(RetentionExecutionStatus.NeverRun, item.Status);
                Assert.False(item.Overdue);
                Assert.Equal(Now, item.NextDueAtUtc);
            });
    }

    private static RetentionScheduleDescriptor Descriptor(string dataClassKey) => new(
        "ingestion",
        dataClassKey,
        RetentionTargetScopeKind.Property,
        executionPolicyVersion: 1,
        TimeSpan.FromHours(1));

    private sealed class Contributor(RetentionScheduleDescriptor schedule)
        : IRetentionExecutionContributor
    {
        public RetentionScheduleDescriptor Schedule { get; } = schedule;

        public Task<RetentionContributionResult> ExecuteAsync(
            RetentionContributionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeScopeRepository(RetentionScheduleTarget target)
        : IRetentionScopeRepository
    {
        public Task<IReadOnlyList<RetentionScheduleTarget>> ListCurrentActiveTargetsAsync(
            RetentionTargetScopeKind targetKind,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetentionScheduleTarget>>([target]);

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

        public Task<IReadOnlyList<RetentionScheduleTarget>> ListActiveTargetsAsync(
            RetentionTargetScopeKind targetKind,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsActiveTargetAsync(
            RetentionTargetScopeKind targetKind,
            Guid? propertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHealthReader(
        params RetentionScheduleStateSnapshot[] snapshots)
        : IRetentionScheduleHealthReader
    {
        public Task<IReadOnlyList<RetentionScheduleStateSnapshot>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetentionScheduleStateSnapshot>>(snapshots);
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}

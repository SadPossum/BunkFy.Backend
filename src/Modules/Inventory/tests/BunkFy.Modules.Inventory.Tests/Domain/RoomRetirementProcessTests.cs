namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.Events;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RoomRetirementProcessTests
{
    [Fact]
    public void Process_requires_finalization_before_topology_completion()
    {
        RoomRetirementProcess process = Create();

        Result premature = process.Complete(Now.AddMinutes(1));
        Result requested = process.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(2));
        RoomRetirementFinalizationRequestedDomainEvent domainEvent =
            Assert.IsType<RoomRetirementFinalizationRequestedDomainEvent>(Assert.Single(process.DomainEvents));
        Result completed = process.Complete(Now.AddMinutes(3));
        Result lateFinalized = process.MarkFinalized(Now.AddMinutes(4));

        Assert.True(premature.IsFailure);
        Assert.True(requested.IsSuccess);
        Assert.Equal(process.Id, domainEvent.TopologyChangeId);
        Assert.True(completed.IsSuccess);
        Assert.True(lateFinalized.IsSuccess);
        Assert.Equal(InventoryRetirementProcessState.Completed, process.State);
        Assert.Equal(Now.AddMinutes(3), process.CompletedAtUtc);
    }

    [Fact]
    public void Rejected_process_remains_drained_and_can_retry()
    {
        RoomRetirementProcess process = Create();
        process.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(1));

        Assert.True(process.Reject(reasonCode: 1, Now.AddMinutes(2)).IsSuccess);
        Assert.True(RoomRetirementProcess.IsDrainActive(process.State));
        Assert.True(process.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(InventoryRetirementProcessState.FinalizationRequested, process.State);
    }

    [Fact]
    public void Draining_process_can_be_canceled_with_an_audited_reason()
    {
        RoomRetirementProcess process = Create();
        long expectedVersion = process.Version;

        Result canceled = process.Cancel(
            expectedVersion,
            "  Keep room in service  ",
            "  user:manager-a  ",
            Now.AddMinutes(1));

        Assert.True(canceled.IsSuccess);
        Assert.Equal(InventoryRetirementProcessState.Canceled, process.State);
        Assert.False(RoomRetirementProcess.IsDrainActive(process.State));
        Assert.Equal(expectedVersion + 1, process.Version);
        Assert.Equal("Keep room in service", process.CancellationReason);
        Assert.Equal("user:manager-a", process.CanceledBy);
        Assert.Equal(Now.AddMinutes(1), process.CanceledAtUtc);
        Assert.Equal(Now.AddMinutes(1), process.UpdatedAtUtc);
    }

    [Fact]
    public void Stale_version_cannot_cancel_or_mutate_the_process()
    {
        RoomRetirementProcess process = Create();

        Result canceled = process.Cancel(
            process.Version - 1,
            "Keep room in service",
            "user:manager-a",
            Now.AddMinutes(1));

        Assert.True(canceled.IsFailure);
        Assert.Equal(InventoryRetirementProcessState.Draining, process.State);
        Assert.Null(process.CancellationReason);
        Assert.Null(process.CanceledBy);
        Assert.Null(process.CanceledAtUtc);
        Assert.Null(process.UpdatedAtUtc);
    }

    [Fact]
    public void Finalization_or_terminal_states_cannot_be_canceled()
    {
        RoomRetirementProcess requested = Create();
        requested.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(1));
        RoomRetirementProcess rejected = Create();
        rejected.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(1));
        rejected.Reject(1, Now.AddMinutes(2));
        RoomRetirementProcess completed = Create();
        completed.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(1));
        completed.MarkFinalized(Now.AddMinutes(2));
        completed.Complete(Now.AddMinutes(3));

        Assert.True(requested.Cancel(requested.Version, "Stop", "user:a", Now).IsFailure);
        Assert.True(rejected.Cancel(rejected.Version, "Stop", "user:a", Now).IsFailure);
        Assert.True(completed.Cancel(completed.Version, "Stop", "user:a", Now).IsFailure);
        Assert.All(
            [requested, rejected, completed],
            process => Assert.Null(process.CanceledAtUtc));
    }

    private static RoomRetirementProcess Create() => RoomRetirementProcess.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Remove room from service",
        "user:operator-a",
        Now).Value;

    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
}

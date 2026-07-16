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

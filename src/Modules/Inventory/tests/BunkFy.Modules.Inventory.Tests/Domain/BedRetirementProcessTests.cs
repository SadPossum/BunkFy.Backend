namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.Events;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class BedRetirementProcessTests
{
    [Fact]
    public void Process_drains_then_waits_for_topology_before_completing()
    {
        BedRetirementProcess process = Create();

        Result requested = process.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(1));
        BedRetirementFinalizationRequestedDomainEvent domainEvent =
            Assert.IsType<BedRetirementFinalizationRequestedDomainEvent>(Assert.Single(process.DomainEvents));
        Result finalized = process.MarkFinalized(Now.AddMinutes(2));
        Result completed = process.Complete(Now.AddMinutes(3));

        Assert.True(requested.IsSuccess);
        Assert.Equal(process.Id, domainEvent.TopologyChangeId);
        Assert.True(finalized.IsSuccess);
        Assert.True(completed.IsSuccess);
        Assert.Equal(InventoryRetirementProcessState.Completed, process.State);
        Assert.Equal(4, process.Version);
        Assert.Equal(Now.AddMinutes(3), process.CompletedAtUtc);
    }

    [Fact]
    public void Rejected_finalization_stays_drained_and_can_be_requested_again()
    {
        BedRetirementProcess process = Create();
        process.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(1));
        process.ClearDomainEvents();

        Assert.True(process.Reject(reasonCode: 2, Now.AddMinutes(2)).IsSuccess);
        Assert.True(BedRetirementProcess.IsDrainActive(process.State));
        Assert.True(process.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(InventoryRetirementProcessState.FinalizationRequested, process.State);
        Assert.Null(process.RejectionReasonCode);
    }

    [Fact]
    public void Duplicate_or_stale_rejection_cannot_regress_the_process()
    {
        BedRetirementProcess rejected = Create();
        rejected.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(1));
        rejected.Reject(reasonCode: 2, Now.AddMinutes(2));
        long rejectedVersion = rejected.Version;

        Assert.True(rejected.Reject(reasonCode: 2, Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(rejectedVersion, rejected.Version);

        BedRetirementProcess completed = Create();
        completed.RequestFinalization(Guid.NewGuid(), Now.AddMinutes(1));
        completed.MarkFinalized(Now.AddMinutes(2));
        completed.Complete(Now.AddMinutes(3));
        long completedVersion = completed.Version;

        Assert.True(completed.Reject(reasonCode: 2, Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(InventoryRetirementProcessState.Completed, completed.State);
        Assert.Equal(completedVersion, completed.Version);
    }

    [Fact]
    public void Draining_process_cannot_complete_from_an_unexpected_topology_event()
    {
        BedRetirementProcess process = Create();

        Result completed = process.Complete(Now.AddMinutes(1));

        Assert.True(completed.IsFailure);
        Assert.Equal(InventoryRetirementProcessState.Draining, process.State);
        Assert.Null(process.CompletedAtUtc);
    }

    private static BedRetirementProcess Create() => BedRetirementProcess.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Remove damaged bed",
        "user:operator-a",
        Now).Value;

    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
}

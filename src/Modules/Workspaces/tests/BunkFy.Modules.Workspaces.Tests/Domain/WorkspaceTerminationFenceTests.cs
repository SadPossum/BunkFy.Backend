namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain.Termination;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceTerminationFenceTests
{
    private const string TenantId =
        "9a8f9c94-2dd7-42b4-9910-b00cb92f2a98";
    private const string Digest =
        "0123456789abcdef0123456789abcdef" +
        "0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Fence_transitions_are_monotonic_and_release_is_pre_destroy_only()
    {
        WorkspaceTerminationFence fence = CreateFence();

        Assert.Equal(WorkspaceTerminationFenceState.Frozen, fence.State);
        Assert.Equal(1, fence.Version);

        Assert.True(fence.BeginDestruction(1, "operator-2", Now.AddMinutes(1))
            .IsSuccess);
        Assert.Equal(
            WorkspaceTerminationFenceState.DestructionStarted,
            fence.State);
        Assert.Equal(2, fence.Version);

        Result release = fence.Release(
            2,
            "operator-2",
            Now.AddMinutes(2));
        Assert.True(release.IsFailure);
        Assert.Equal(
            WorkspaceTerminationFenceErrors.TransitionInvalid.Code,
            release.Error.Code);

        Assert.True(fence.Close(2, "operator-2", Now.AddMinutes(2))
            .IsSuccess);
        Assert.Equal(WorkspaceTerminationFenceState.Closed, fence.State);
        Assert.Equal(3, fence.Version);
    }

    [Fact]
    public void Frozen_fence_can_be_released_with_expected_version()
    {
        WorkspaceTerminationFence fence = CreateFence();

        Assert.True(fence.Release(1, "operator-2", Now.AddMinutes(1))
            .IsSuccess);
        Assert.Equal(WorkspaceTerminationFenceState.Released, fence.State);
        Assert.Equal(2, fence.Version);
        Assert.Equal("operator-2", fence.LastChangedBy);
    }

    [Fact]
    public void Receipt_rejects_impossible_transition_proof()
    {
        Result<WorkspaceTerminationFenceReceipt> receipt =
            WorkspaceTerminationFenceReceipt.Create(
                Guid.NewGuid(),
                TenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                WorkspaceTerminationFenceAction.Release,
                selectedFenceVersion: 1,
                resultingFenceVersion: 2,
                WorkspaceTerminationFenceState.Closed,
                Digest,
                "operator-1",
                Now);

        Assert.True(receipt.IsFailure);
        Assert.Equal(
            WorkspaceTerminationFenceErrors.ReceiptTransitionInvalid.Code,
            receipt.Error.Code);
    }

    private static WorkspaceTerminationFence CreateFence() =>
        WorkspaceTerminationFence.Freeze(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            Digest,
            "operator-1",
            Now).Value;
}

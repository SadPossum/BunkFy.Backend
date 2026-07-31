namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.Termination;
using BunkFy.Modules.Workspaces.Persistence;
using BunkFy.Modules.Workspaces.Persistence.Repositories;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceTerminationFencePersistenceTests
{
    private const string TenantId =
        "9a8f9c94-2dd7-42b4-9910-b00cb92f2a98";
    private const string Digest =
        "0123456789abcdef0123456789abcdef" +
        "0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Apply_replays_and_release_removes_current_fence()
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceTerminationFenceRepository repository = new(context);
        SequentialIds ids = new();
        TestClock clock = new(Now);
        ApplyWorkspaceTerminationFenceCommandHandler apply = new(
            repository,
            new TestScopeContext(),
            clock,
            ids);
        ApplyWorkspaceTerminationFenceCommand command = NewApplyCommand();

        WorkspaceTerminationFenceReceiptDto first =
            (await apply.HandleAsync(command, default)).Value;
        await context.SaveChangesAsync();
        WorkspaceTerminationFenceReceiptDto replay =
            (await apply.HandleAsync(command, default)).Value;

        Assert.Equal(first, replay);
        Assert.Equal(1, await context.WorkspaceTerminationFences.CountAsync());
        Assert.Equal(
            1,
            await context.WorkspaceTerminationFenceReceipts.CountAsync());

        clock.UtcNow = Now.AddMinutes(1);
        ReleaseWorkspaceTerminationFenceCommandHandler release = new(
            repository,
            new TestScopeContext(),
            clock,
            ids);
        ReleaseWorkspaceTerminationFenceCommand releaseCommand = new(
            Guid.NewGuid(),
            command.ProcessId,
            command.CaseId,
            command.ApprovalRevision,
            OperationRevision: 2,
            Guid.NewGuid(),
            command.TerminationEpoch,
            ExpectedFenceVersion: 1,
            command.PolicyEvidenceSha256,
            "operator-2");

        WorkspaceTerminationFenceReceiptDto released =
            (await release.HandleAsync(releaseCommand, default)).Value;
        await context.SaveChangesAsync();

        Assert.Equal(
            WorkspaceTerminationFenceActionDto.Release,
            released.Action);
        Assert.Equal(2, released.ResultingFenceVersion);
        IWorkspaceTerminationFenceReader reader = repository;
        Assert.Null(await reader.GetCurrentAsync());
        Assert.Equal(
            2,
            await context.WorkspaceTerminationFenceReceipts.CountAsync());
    }

    [Fact]
    public async Task Active_fence_blocks_a_second_process()
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceTerminationFenceRepository repository = new(context);
        ApplyWorkspaceTerminationFenceCommandHandler handler = new(
            repository,
            new TestScopeContext(),
            new TestClock(Now),
            new SequentialIds());

        Assert.True((await handler.HandleAsync(
            NewApplyCommand(),
            default)).IsSuccess);
        await context.SaveChangesAsync();

        ApplyWorkspaceTerminationFenceCommand second = NewApplyCommand();
        var result = await handler.HandleAsync(second, default);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Workspaces.TerminationFenceActiveConflict",
            result.Error.Code);
    }

    [Fact]
    public async Task Released_fence_prevents_reusing_termination_epoch()
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceTerminationFenceRepository repository = new(context);
        SequentialIds ids = new();
        TestClock clock = new(Now);
        ApplyWorkspaceTerminationFenceCommandHandler apply = new(
            repository,
            new TestScopeContext(),
            clock,
            ids);
        ApplyWorkspaceTerminationFenceCommand first = NewApplyCommand();
        WorkspaceTerminationFenceReceiptDto frozen =
            (await apply.HandleAsync(first, default)).Value;
        await context.SaveChangesAsync();
        ReleaseWorkspaceTerminationFenceCommandHandler release = new(
            repository,
            new TestScopeContext(),
            new TestClock(Now.AddMinutes(1)),
            ids);
        Assert.True((await release.HandleAsync(
            new ReleaseWorkspaceTerminationFenceCommand(
                Guid.NewGuid(),
                first.ProcessId,
                first.CaseId,
                first.ApprovalRevision,
                2,
                Guid.NewGuid(),
                first.TerminationEpoch,
                frozen.ResultingFenceVersion,
                first.PolicyEvidenceSha256,
                "operator-2"),
            default)).IsSuccess);
        await context.SaveChangesAsync();

        ApplyWorkspaceTerminationFenceCommand reusedEpoch =
            NewApplyCommand() with
            {
                TerminationEpoch = first.TerminationEpoch
            };
        var result = await apply.HandleAsync(
            reusedEpoch,
            default);

        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceTerminationApplicationErrors
                .FenceCoordinatesConflict,
            result.Error);
    }

    [Fact]
    public async Task Termination_receipts_are_append_only()
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceTerminationFence fence = WorkspaceTerminationFence.Freeze(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            Digest,
            "operator-1",
            Now).Value;
        WorkspaceTerminationFenceReceipt receipt =
            WorkspaceTerminationFenceReceipt.Create(
                Guid.NewGuid(),
                TenantId,
                fence.Id,
                fence.ProcessId,
                fence.CaseId,
                fence.ApprovalRevision,
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                fence.TerminationEpoch,
                WorkspaceTerminationFenceAction.Freeze,
                0,
                1,
                BunkFy.Modules.Workspaces.Domain.Termination
                    .WorkspaceTerminationFenceState.Frozen,
                Digest,
                "operator-1",
                Now).Value;
        context.AddRange(fence, receipt);
        await context.SaveChangesAsync();

        context.Remove(receipt);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Historical_fences_cannot_be_deleted()
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceTerminationFence fence = WorkspaceTerminationFence.Freeze(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            Digest,
            "operator-1",
            Now).Value;
        context.Add(fence);
        await context.SaveChangesAsync();

        context.Remove(fence);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
    }

    private static ApplyWorkspaceTerminationFenceCommand NewApplyCommand() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Digest,
            "operator-1");

    private static WorkspacesDbContext CreateContext()
    {
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private sealed class SequentialIds : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}

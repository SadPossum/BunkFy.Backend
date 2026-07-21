namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffAccessProcessTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creation_normalizes_identity_and_captures_distinct_profiles()
    {
        Guid firstProfile = Guid.NewGuid();
        Guid secondProfile = Guid.NewGuid();

        WorkspaceStaffAccessProcess process = WorkspaceStaffAccessProcess.Create(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid(),
            " member-a ",
            WorkspaceStaffAccessTargetState.Suspended,
            2,
            new DateOnly(2026, 7, 21),
            " user:owner ",
            [
                new(secondProfile, "tenant:workspace-a/property:second"),
                new(firstProfile, "tenant:workspace-a"),
                new(firstProfile, "tenant:workspace-a")
            ],
            Now).Value;

        Assert.Equal("member-a", process.SubjectId);
        Assert.Equal("user:owner", process.RequestedBy);
        Assert.Equal(WorkspaceStaffAccessProcessState.Prepared, process.State);
        Assert.Equal(
            [firstProfile, secondProfile],
            process.ProfileSnapshots.Select(snapshot => snapshot.ProfileId).ToArray());
        Assert.Equal(
            ["tenant:workspace-a", "tenant:workspace-a/property:second"],
            process.ProfileSnapshots.Select(snapshot => snapshot.AssignmentScope).ToArray());
    }

    [Fact]
    public void Denial_process_completes_only_after_the_staff_commit_is_observed()
    {
        WorkspaceStaffAccessProcess process = Create(WorkspaceStaffAccessTargetState.Suspended);

        Assert.True(process.MarkAwaitingStaffCommit(Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(WorkspaceStaffAccessProcessState.AwaitingStaffCommit, process.State);
        Assert.Null(process.CompletedAtUtc);

        Assert.True(process.ObserveStaffCommit(Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(WorkspaceStaffAccessProcessState.Completed, process.State);
        Assert.Equal(Now.AddMinutes(2), process.CompletedAtUtc);
    }

    [Fact]
    public void Resume_process_waits_for_restoration_after_the_staff_commit()
    {
        WorkspaceStaffAccessProcess process = Create(WorkspaceStaffAccessTargetState.Active);

        Assert.True(process.MarkAwaitingStaffCommit(Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.ObserveStaffCommit(Now.AddMinutes(2)).IsSuccess);

        Assert.Equal(WorkspaceStaffAccessProcessState.RestorationPending, process.State);
        Assert.Null(process.CompletedAtUtc);
        Assert.True(process.Complete(Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(WorkspaceStaffAccessProcessState.Completed, process.State);
    }

    [Fact]
    public void Operational_failure_is_retryable_without_losing_the_process_phase()
    {
        WorkspaceStaffAccessProcess process = Create(WorkspaceStaffAccessTargetState.Suspended);

        Assert.True(process.RecordFailure("Workspaces.TestFailure", Now.AddMinutes(1)).IsSuccess);

        Assert.Equal(WorkspaceStaffAccessProcessState.Prepared, process.State);
        Assert.Equal("Workspaces.TestFailure", process.FailureCode);
        Assert.Null(process.CompletedAtUtc);
    }

    private static WorkspaceStaffAccessProcess Create(WorkspaceStaffAccessTargetState targetState) =>
        WorkspaceStaffAccessProcess.Create(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid(),
            "member-a",
            targetState,
            2,
            new DateOnly(2026, 7, 21),
            "user:owner",
            [new(Guid.NewGuid(), "tenant:workspace-a")],
            Now).Value;
}

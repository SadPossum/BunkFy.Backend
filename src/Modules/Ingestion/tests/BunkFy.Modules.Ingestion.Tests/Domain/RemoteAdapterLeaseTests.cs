namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Errors;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RemoteAdapterLeaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid RunId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LeaseId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ClaimId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid CredentialId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkerId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    [Fact]
    public void Connection_claims_renews_fences_and_reclaims_with_monotonic_epoch()
    {
        AdapterConnection connection = CreateConnection();

        var first = connection.ClaimRemoteLease(
            RunId, LeaseId, ClaimId, CredentialId, WorkerId, TimeSpan.FromMinutes(2), 1, Now);
        var overlapping = connection.ClaimRemoteLease(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CredentialId, Guid.NewGuid(),
            TimeSpan.FromMinutes(2), connection.Version, Now.AddSeconds(10));
        var wrongWorker = connection.AuthorizeRemoteLeaseOperation(
            RunId, LeaseId, first.Value.LeaseEpoch, CredentialId, Guid.NewGuid(),
            connection.Version, Now.AddSeconds(20));
        var renewed = connection.RenewRemoteLease(
            RunId, LeaseId, first.Value.LeaseEpoch, CredentialId, WorkerId,
            TimeSpan.FromMinutes(2), connection.Version, Now.AddMinutes(1));
        var advanced = connection.AdvanceRemoteCheckpoint(
            RunId, LeaseId, first.Value.LeaseEpoch, CredentialId, WorkerId,
            "checkpoint-1", connection.Version, Now.AddMinutes(2));
        var replacement = connection.ClaimRemoteLease(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CredentialId, Guid.NewGuid(),
            TimeSpan.FromMinutes(2), connection.Version, Now.AddMinutes(4));
        var stale = connection.AuthorizeRemoteLeaseOperation(
            RunId, LeaseId, first.Value.LeaseEpoch, CredentialId, WorkerId,
            connection.Version, Now.AddMinutes(4));

        Assert.True(first.IsSuccess);
        Assert.Equal(1, first.Value.LeaseEpoch);
        Assert.Equal(IngestionDomainErrors.RemoteLeaseAlreadyActive, overlapping.Error);
        Assert.Equal(IngestionDomainErrors.RemoteLeaseMismatch, wrongWorker.Error);
        Assert.True(renewed.IsSuccess);
        Assert.True(advanced.IsSuccess);
        Assert.Equal("checkpoint-1", connection.Checkpoint);
        Assert.True(replacement.IsSuccess);
        Assert.Equal(2, replacement.Value.LeaseEpoch);
        Assert.Equal(IngestionDomainErrors.RemoteLeaseMismatch, stale.Error);
    }

    [Fact]
    public void Disable_revokes_remote_authority_and_mode_change_requires_explicit_fencing()
    {
        AdapterConnection connection = CreateConnection();
        var lease = connection.ClaimRemoteLease(
            RunId, LeaseId, ClaimId, CredentialId, WorkerId, TimeSpan.FromMinutes(2), 1, Now);

        var modeChange = connection.Configure(
            AdapterExecutionMode.Push,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main",
            null,
            connection.Version,
            Now.AddSeconds(1));
        var disabled = connection.Disable(connection.Version, Now.AddSeconds(2));
        var stale = connection.AuthorizeRemoteLeaseOperation(
            RunId, LeaseId, lease.Value.LeaseEpoch, CredentialId, WorkerId,
            connection.Version, Now.AddSeconds(3));

        Assert.Equal(IngestionDomainErrors.RemoteLeaseMustBeReleased, modeChange.Error);
        Assert.True(disabled.IsSuccess);
        Assert.Null(connection.RemoteLeaseId);
        Assert.Equal(IngestionDomainErrors.RemoteLeaseNotActive, stale.Error);
    }

    [Fact]
    public void Remote_run_identity_is_separate_from_task_identity_and_completion_is_fenced()
    {
        var created = IngestionRun.StartRemote(
            RunId,
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            LeaseId,
            ClaimId,
            leaseEpoch: 7,
            CredentialId,
            WorkerId,
            "checkpoint-0",
            Now.AddMinutes(2),
            Now);
        IngestionRun run = created.Value;

        var wrongEpoch = run.CompleteRemote(
            LeaseId, 8, CredentialId, WorkerId, AdapterRunOutcome.Succeeded,
            0, 0, 0, "checkpoint-0", null, run.Version, Now.AddMinutes(1));
        var renewed = run.RenewRemoteLease(
            LeaseId, 7, CredentialId, WorkerId, Now.AddMinutes(3), run.Version, Now.AddMinutes(1));
        var completed = run.CompleteRemote(
            LeaseId, 7, CredentialId, WorkerId, AdapterRunOutcome.Succeeded,
            1, 1, 0, "checkpoint-1", null, run.Version, Now.AddMinutes(2));

        Assert.True(created.IsSuccess);
        Assert.Equal(IngestionRunExecutionKind.RemoteLease, run.ExecutionKind);
        Assert.Null(run.TaskRunId);
        Assert.Equal(IngestionDomainErrors.RemoteLeaseMismatch, wrongEpoch.Error);
        Assert.True(renewed.IsSuccess);
        Assert.True(completed.IsSuccess);
        Assert.Equal(IngestionRunState.Succeeded, run.State);
        Assert.Equal("checkpoint-1", run.AcceptedCheckpoint);
    }

    private static AdapterConnection CreateConnection() => AdapterConnection.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        "fake.http",
        AdapterExecutionMode.RemotePolling,
        IngestionConflictPolicy.SuggestionsOnly,
        "configuration://main",
        "secret://main",
        Now).Value;
}

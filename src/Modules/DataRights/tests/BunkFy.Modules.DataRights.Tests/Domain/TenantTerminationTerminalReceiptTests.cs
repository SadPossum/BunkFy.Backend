namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationTerminalReceiptTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 22, 0, 0, TimeSpan.Zero);
    private static readonly string Digest = new('a', 64);

    [Fact]
    public void Seals_only_complete_proof_coordinates()
    {
        Guid processId = Guid.NewGuid();
        Guid taskRunId = Guid.NewGuid();

        Result<TenantTerminationTerminalReceipt> result = Seal(
            processId,
            taskRunId,
            exportRequested: true,
            exportArtifactId: Guid.NewGuid(),
            exportArtifactVersion: 3,
            exportFragmentSetSha256: Digest);

        Assert.True(result.IsSuccess);
        Assert.Equal(processId, result.Value.ProcessId);
        Assert.Equal(taskRunId, result.Value.VerificationTaskRunId);
        Assert.Equal("workspaces", result.Value.TerminalOwnerKey);
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public void Rejects_partial_export_and_untrusted_replay_shapes()
    {
        Result<TenantTerminationTerminalReceipt> partialExport = Seal(
            Guid.NewGuid(),
            Guid.NewGuid(),
            exportRequested: true,
            exportArtifactId: null,
            exportArtifactVersion: null,
            exportFragmentSetSha256: null);
        Result<TenantTerminationTerminalReceipt> badCheckpoint =
            TenantTerminationTerminalReceipt.Seal(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 4,
                Guid.NewGuid(),
                destroyOperationRevision: 7,
                verificationOperationRevision: 8,
                Guid.NewGuid(),
                verificationTaskAttempt: 1,
                Digest,
                Digest,
                exportRequested: false,
                exportArtifactId: null,
                exportArtifactVersion: null,
                exportFragmentSetSha256: null,
                ownerCount: 1,
                Digest,
                "workspaces",
                terminalOwnerSelectedProofRevision: 1,
                terminalOwnerResultingProofRevision: 2,
                replayCheckpointSequence: 0,
                Digest,
                replayIntegrityKeyVersion: 1,
                Digest,
                Now,
                "executor",
                Now);

        Assert.True(partialExport.IsFailure);
        Assert.True(badCheckpoint.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationTerminalReceiptInvalid,
            partialExport.Error);
        Assert.Equal(partialExport.Error, badCheckpoint.Error);
    }

    private static Result<TenantTerminationTerminalReceipt> Seal(
        Guid processId,
        Guid taskRunId,
        bool exportRequested,
        Guid? exportArtifactId,
        long? exportArtifactVersion,
        string? exportFragmentSetSha256) =>
        TenantTerminationTerminalReceipt.Seal(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            processId,
            Guid.NewGuid(),
            approvalRevision: 4,
            Guid.NewGuid(),
            destroyOperationRevision: 7,
            verificationOperationRevision: 8,
            taskRunId,
            verificationTaskAttempt: 1,
            Digest,
            Digest,
            exportRequested,
            exportArtifactId,
            exportArtifactVersion,
            exportFragmentSetSha256,
            ownerCount: 3,
            Digest,
            "workspaces",
            terminalOwnerSelectedProofRevision: 4,
            terminalOwnerResultingProofRevision: 5,
            replayCheckpointSequence: 6,
            Digest,
            replayIntegrityKeyVersion: 2,
            Digest,
            Now,
            "executor",
            Now.AddSeconds(1));
}

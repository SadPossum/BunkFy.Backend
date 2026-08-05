namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationExportArtifactTests
{
    private const string FrozenSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string PolicySha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string FragmentSetSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string PlaintextSha =
        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private static readonly DateTimeOffset RequestedAt =
        new(2026, 7, 31, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Available_artifact_requires_the_exact_fragment_set()
    {
        TenantTerminationExportArtifact artifact = Request();
        Guid runId = Guid.NewGuid();
        Assert.True(artifact.BeginGeneration(
            runId,
            1,
            RequestedAt.AddMinutes(1)).IsSuccess);

        Result wrongSet = artifact.MarkAvailable(
            runId,
            1,
            fragmentCount: 2,
            recordCount: 7,
            new string('e', 64),
            "data-rights/tenant-exports/artifact.bftxa",
            encryptedByteLength: 8192,
            PlaintextSha,
            encryptionKeyVersion: 2,
            formatVersion: 1,
            RequestedAt.AddMinutes(2),
            RequestedAt.AddHours(24));
        Assert.True(wrongSet.IsFailure);

        Assert.True(artifact.MarkAvailable(
            runId,
            1,
            fragmentCount: 2,
            recordCount: 7,
            FragmentSetSha,
            "data-rights/tenant-exports/artifact.bftxa",
            encryptedByteLength: 8192,
            PlaintextSha,
            encryptionKeyVersion: 2,
            formatVersion: 1,
            RequestedAt.AddMinutes(2),
            RequestedAt.AddHours(24)).IsSuccess);
        Assert.Equal(TenantTerminationExportArtifactState.Available, artifact.State);
        Assert.Equal(2, artifact.FragmentCount);
        Assert.Equal(7, artifact.RecordCount);
    }

    [Fact]
    public void Failed_attempt_requires_a_strictly_new_retry()
    {
        TenantTerminationExportArtifact artifact = Request();
        Guid firstRun = Guid.NewGuid();
        _ = artifact.BeginGeneration(firstRun, 1, RequestedAt.AddMinutes(1));
        _ = artifact.MarkFailed(
            firstRun,
            1,
            "fragment-read-failed",
            RequestedAt.AddMinutes(2));

        Assert.True(artifact.BeginGeneration(
            Guid.NewGuid(),
            1,
            RequestedAt.AddMinutes(3)).IsFailure);
        Assert.True(artifact.BeginGeneration(
            Guid.NewGuid(),
            2,
            RequestedAt.AddMinutes(3)).IsSuccess);
        Assert.Equal(2, artifact.GenerationAttempt);
    }

    [Fact]
    public void Higher_task_attempt_fences_an_abandoned_generation_attempt()
    {
        TenantTerminationExportArtifact artifact = Request();
        Guid runId = Guid.NewGuid();
        _ = artifact.BeginGeneration(
            runId,
            attempt: 1,
            RequestedAt.AddMinutes(1));

        Assert.True(artifact.BeginGeneration(
            runId,
            attempt: 2,
            RequestedAt.AddMinutes(2)).IsSuccess);
        Assert.Equal(2, artifact.GenerationAttempt);
        Assert.Equal(RequestedAt.AddMinutes(2), artifact.GenerationStartedAtUtc);
        Assert.True(artifact.MarkFailed(
            runId,
            attempt: 1,
            "stale-worker",
            RequestedAt.AddMinutes(3)).IsFailure);
    }

    [Fact]
    public void Expiry_and_deletion_reject_competing_workers()
    {
        TenantTerminationExportArtifact artifact = Request();
        DateTimeOffset expiresAt = RequestedAt.AddHours(24);
        Guid deletionRun = Guid.NewGuid();

        Assert.True(artifact.MarkExpired(expiresAt).IsSuccess);
        Assert.True(artifact.BeginDeletion(
            deletionRun,
            expiresAt.AddMinutes(1)).IsSuccess);
        Assert.True(artifact.BeginDeletion(
            Guid.NewGuid(),
            expiresAt.AddMinutes(1)).IsFailure);
        Assert.True(artifact.MarkDeleted(
            deletionRun,
            expiresAt.AddMinutes(2)).IsSuccess);
        Assert.Equal(TenantTerminationExportArtifactState.Deleted, artifact.State);
    }

    [Fact]
    public void Request_rejects_unbounded_or_unbound_fragment_sets()
    {
        Result<TenantTerminationExportArtifact> result =
            TenantTerminationExportArtifact.Request(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 2,
                freezeOperationRevision: 1,
                exportOperationRevision: 2,
                Guid.NewGuid(),
                Guid.NewGuid(),
                FrozenSha,
                PolicySha,
                expectedFragmentCount:
                    TenantTerminationExportArtifact.MaximumFragmentCount + 1,
                FragmentSetSha,
                RequestedAt,
                RequestedAt.AddHours(24));

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationExportArtifactInvalid,
            result.Error);
    }

    private static TenantTerminationExportArtifact Request() =>
        TenantTerminationExportArtifact.Request(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "tenant-a",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            approvalRevision: 2,
            freezeOperationRevision: 1,
            exportOperationRevision: 2,
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            FrozenSha,
            PolicySha,
            expectedFragmentCount: 2,
            FragmentSetSha,
            RequestedAt,
            RequestedAt.AddHours(24)).Value;
}

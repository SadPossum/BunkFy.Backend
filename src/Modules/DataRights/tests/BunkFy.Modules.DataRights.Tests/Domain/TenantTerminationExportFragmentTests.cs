namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationExportFragmentTests
{
    private const string CatalogSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FrozenSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string PolicySha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string PlaintextSha =
        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private static readonly DateTimeOffset RequestedAt =
        new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Available_fragment_requires_exact_stable_owner_proof()
    {
        TenantTerminationExportFragment fragment = Request();
        Guid runId = Guid.NewGuid();

        Assert.True(fragment.BeginGeneration(
            runId,
            attempt: 1,
            RequestedAt.AddMinutes(1)).IsSuccess);
        Assert.True(fragment.MarkAvailable(
            runId,
            attempt: 1,
            recordCount: 7,
            selectedProofRevision: 13,
            resultingProofRevision: 13,
            resultCode: "workspaces.tenant-export.completed",
            catalogVersion: 3,
            CatalogSha,
            storageKey: "data-rights/tenant-exports/fragment.bfdrx",
            encryptedByteLength: 4096,
            PlaintextSha,
            encryptionKeyVersion: 2,
            formatVersion: 1,
            availableAtUtc: RequestedAt.AddMinutes(2),
            expiresAtUtc: RequestedAt.AddHours(24)).IsSuccess);

        Assert.Equal(TenantTerminationExportFragmentState.Available, fragment.State);
        Assert.Equal(7, fragment.RecordCount);
        Assert.Equal(13, fragment.SelectedProofRevision);
        Assert.Equal(fragment.SelectedProofRevision, fragment.ResultingProofRevision);
        Assert.Equal(3, fragment.Version);
        Assert.True(fragment.MarkAvailable(
            runId,
            attempt: 1,
            recordCount: 7,
            selectedProofRevision: 13,
            resultingProofRevision: 13,
            resultCode: "workspaces.tenant-export.completed",
            catalogVersion: 3,
            CatalogSha,
            storageKey: "data-rights/tenant-exports/fragment.bfdrx",
            encryptedByteLength: 4096,
            PlaintextSha,
            encryptionKeyVersion: 2,
            formatVersion: 1,
            availableAtUtc: RequestedAt.AddMinutes(2),
            expiresAtUtc: RequestedAt.AddHours(24)).IsSuccess);
        Assert.Equal(3, fragment.Version);
    }

    [Fact]
    public void Empty_owner_fragment_preserves_revision_zero_as_exact_proof()
    {
        TenantTerminationExportFragment fragment = Request();
        Guid runId = Guid.NewGuid();
        _ = fragment.BeginGeneration(
            runId,
            attempt: 1,
            RequestedAt.AddMinutes(1));

        Assert.True(fragment.MarkAvailable(
            runId,
            attempt: 1,
            recordCount: 0,
            selectedProofRevision: 0,
            resultingProofRevision: 0,
            resultCode: "organizations.tenant-export.empty",
            catalogVersion: 3,
            CatalogSha,
            storageKey: "data-rights/tenant-exports/empty-fragment.bfdrx",
            encryptedByteLength: 128,
            PlaintextSha,
            encryptionKeyVersion: 2,
            formatVersion: 1,
            availableAtUtc: RequestedAt.AddMinutes(2),
            expiresAtUtc: RequestedAt.AddHours(24)).IsSuccess);

        Assert.Equal(0, fragment.SelectedProofRevision);
        Assert.Equal(0, fragment.ResultingProofRevision);
    }

    [Fact]
    public void Changed_owner_revision_or_catalog_fails_closed()
    {
        TenantTerminationExportFragment stale = Request();
        Guid staleRunId = Guid.NewGuid();
        _ = stale.BeginGeneration(staleRunId, 1, RequestedAt.AddMinutes(1));

        Result staleResult = stale.MarkAvailable(
            staleRunId,
            1,
            1,
            selectedProofRevision: 4,
            resultingProofRevision: 5,
            "workspaces.tenant-export.completed",
            3,
            CatalogSha,
            "fragment",
            100,
            PlaintextSha,
            1,
            1,
            RequestedAt.AddMinutes(2),
            RequestedAt.AddHours(24));

        Assert.True(staleResult.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors
                .TenantTerminationExportFragmentCompletionInvalid,
            staleResult.Error);
        Assert.Equal(
            TenantTerminationExportFragmentState.Generating,
            stale.State);

        TenantTerminationExportFragment wrongCatalog = Request();
        Guid catalogRunId = Guid.NewGuid();
        _ = wrongCatalog.BeginGeneration(
            catalogRunId,
            1,
            RequestedAt.AddMinutes(1));
        Result catalogResult = wrongCatalog.MarkAvailable(
            catalogRunId,
            1,
            1,
            4,
            4,
            "workspaces.tenant-export.completed",
            4,
            CatalogSha,
            "fragment",
            100,
            PlaintextSha,
            1,
            1,
            RequestedAt.AddMinutes(2),
            RequestedAt.AddHours(24));

        Assert.True(catalogResult.IsFailure);
    }

    [Fact]
    public void Failed_attempt_can_retry_without_accepting_stale_completion()
    {
        TenantTerminationExportFragment fragment = Request();
        Guid firstRun = Guid.NewGuid();
        Guid secondRun = Guid.NewGuid();
        _ = fragment.BeginGeneration(firstRun, 1, RequestedAt.AddMinutes(1));
        Assert.True(fragment.BeginGeneration(
            firstRun,
            1,
            RequestedAt.AddMinutes(2)).IsSuccess);
        Assert.Equal(2, fragment.Version);
        Assert.True(fragment.MarkFailed(
            firstRun,
            1,
            "owner-export-failed",
            RequestedAt.AddMinutes(2)).IsSuccess);
        Assert.True(fragment.BeginGeneration(
            secondRun,
            1,
            RequestedAt.AddMinutes(3)).IsFailure);
        Assert.True(fragment.BeginGeneration(
            secondRun,
            2,
            RequestedAt.AddMinutes(3)).IsSuccess);
        Assert.Equal(RequestedAt.AddMinutes(3), fragment.GenerationStartedAtUtc);

        Result stale = fragment.MarkAvailable(
            firstRun,
            1,
            0,
            1,
            1,
            "workspaces.tenant-export.completed",
            3,
            CatalogSha,
            "fragment",
            100,
            PlaintextSha,
            1,
            1,
            RequestedAt.AddMinutes(4),
            RequestedAt.AddHours(24));

        Assert.True(stale.IsFailure);
        Assert.Equal(secondRun, fragment.GenerationRunId);
        Assert.Equal(2, fragment.GenerationAttempt);
    }

    [Fact]
    public void Available_fragment_rejects_a_different_generation_run()
    {
        TenantTerminationExportFragment fragment = Request();
        Guid runId = Guid.NewGuid();
        _ = fragment.BeginGeneration(runId, 1, RequestedAt.AddMinutes(1));
        _ = fragment.MarkAvailable(
            runId,
            1,
            0,
            1,
            1,
            "workspaces.tenant-export.completed",
            3,
            CatalogSha,
            "fragment",
            100,
            PlaintextSha,
            1,
            1,
            RequestedAt.AddMinutes(2),
            RequestedAt.AddHours(24));

        Result result = fragment.BeginGeneration(
            Guid.NewGuid(),
            2,
            RequestedAt.AddMinutes(3));

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors
                .TenantTerminationExportFragmentTransitionInvalid,
            result.Error);
    }

    [Fact]
    public void Higher_task_attempt_fences_an_abandoned_generation_attempt()
    {
        TenantTerminationExportFragment fragment = Request();
        Guid runId = Guid.NewGuid();
        _ = fragment.BeginGeneration(
            runId,
            attempt: 1,
            RequestedAt.AddMinutes(1));

        Assert.True(fragment.BeginGeneration(
            runId,
            attempt: 2,
            RequestedAt.AddMinutes(2)).IsSuccess);
        Assert.Equal(2, fragment.GenerationAttempt);
        Assert.Equal(RequestedAt.AddMinutes(2), fragment.GenerationStartedAtUtc);
        Assert.True(fragment.MarkFailed(
            runId,
            attempt: 1,
            "stale-worker",
            RequestedAt.AddMinutes(3)).IsFailure);
    }

    [Fact]
    public void Expiry_and_deletion_are_idempotent_and_monotonic()
    {
        TenantTerminationExportFragment fragment = Request();
        DateTimeOffset expiresAt = RequestedAt.AddHours(24);
        Guid deletionRun = Guid.NewGuid();

        Assert.True(fragment.MarkExpired(expiresAt).IsSuccess);
        Assert.True(fragment.BeginDeletion(
            deletionRun,
            expiresAt.AddMinutes(1)).IsSuccess);
        Assert.True(fragment.BeginDeletion(
            Guid.NewGuid(),
            expiresAt.AddMinutes(1)).IsFailure);
        Assert.True(fragment.MarkDeleted(
            deletionRun,
            expiresAt.AddMinutes(2)).IsSuccess);
        Assert.True(fragment.MarkDeleted(
            deletionRun,
            expiresAt.AddMinutes(3)).IsSuccess);
        Assert.Equal(TenantTerminationExportFragmentState.Deleted, fragment.State);
    }

    [Fact]
    public void Request_rejects_non_monotonic_or_unbound_coordinates()
    {
        Result<TenantTerminationExportFragment> result =
            TenantTerminationExportFragment.Request(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 2,
                freezeOperationRevision: 3,
                exportOperationRevision: 3,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "workspaces",
                ownerContractVersion: 1,
                catalogVersion: 3,
                CatalogSha,
                FrozenSha,
                PolicySha,
                RequestedAt,
                RequestedAt.AddHours(24));

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationExportFragmentInvalid,
            result.Error);
    }

    private static TenantTerminationExportFragment Request() =>
        TenantTerminationExportFragment.Request(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "tenant-a",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            approvalRevision: 2,
            freezeOperationRevision: 1,
            exportOperationRevision: 2,
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            "workspaces",
            ownerContractVersion: 1,
            catalogVersion: 3,
            CatalogSha,
            FrozenSha,
            PolicySha,
            RequestedAt,
            RequestedAt.AddHours(24)).Value;
}

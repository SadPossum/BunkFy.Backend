namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationExportArtifactCoordinatorTests
{
    private const string PolicySha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FrozenSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string WorkspacesCatalogSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string InventoryCatalogSha =
        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string PlaintextSha =
        "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Prepare_is_order_independent_and_requires_every_exact_owner()
    {
        StubContributor workspaces = new(
            "workspaces",
            WorkspacesCatalogSha,
            []);
        StubContributor inventory = new(
            "inventory",
            InventoryCatalogSha,
            ["workspaces"]);
        TenantTerminationProcess process = ExportingProcess(
            workspaces,
            inventory);
        ExportOwnerProof workspace =
            AvailableOwner(process, workspaces, 1, 11);
        ExportOwnerProof inventoryProof =
            AvailableOwner(process, inventory, 2, 22);
        Result<TenantTerminationExportArtifact> first =
            TenantTerminationExportArtifactCoordinator.Prepare(
            process,
            [workspace.WorkItem, inventoryProof.WorkItem],
            [workspace.Fragment, inventoryProof.Fragment],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(8),
            Now.AddHours(12));
        Result<TenantTerminationExportArtifact> reversed =
            TenantTerminationExportArtifactCoordinator.Prepare(
            process,
            [inventoryProof.WorkItem, workspace.WorkItem],
            [inventoryProof.Fragment, workspace.Fragment],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(8),
            Now.AddHours(12));
        Result<TenantTerminationExportArtifact> incomplete =
            TenantTerminationExportArtifactCoordinator.Prepare(
            process,
            [workspace.WorkItem, inventoryProof.WorkItem],
            [workspace.Fragment],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(8),
            Now.AddHours(12));

        Assert.True(first.IsSuccess);
        Assert.True(reversed.IsSuccess);
        Assert.Equal(
            first.Value.FragmentSetSha256,
            reversed.Value.FragmentSetSha256);
        Assert.Equal(2, first.Value.ExpectedFragmentCount);
        Assert.True(incomplete.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors.TenantTerminationExportProofInvalid,
            incomplete.Error);
    }

    [Fact]
    public void Confirmation_requires_available_artifact_before_export_can_advance()
    {
        StubContributor workspaces = new(
            "workspaces",
            WorkspacesCatalogSha,
            []);
        TenantTerminationProcess process = ExportingProcess(workspaces);
        ExportOwnerProof owner =
            AvailableOwner(process, workspaces, 1, 11);
        TenantTerminationExportArtifact artifact =
            TenantTerminationExportArtifactCoordinator.Prepare(
            process,
            [owner.WorkItem],
            [owner.Fragment],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(8),
            Now.AddHours(12)).Value;

        Result unavailable = TenantTerminationExportArtifactCoordinator.Confirm(
            process,
            artifact,
            process.Version,
            "owner:approver",
            Now.AddMinutes(9));
        Assert.True(unavailable.IsFailure);

        Guid runId = Guid.NewGuid();
        _ = artifact.BeginGeneration(runId, 1, Now.AddMinutes(9));
        _ = artifact.MarkAvailable(
            runId,
            1,
            fragmentCount: 1,
            recordCount: 1,
            artifact.FragmentSetSha256,
            "data-rights/tenant-exports/artifact.bftxa",
            encryptedByteLength: 4096,
            PlaintextSha,
            encryptionKeyVersion: 1,
            formatVersion: 1,
            Now.AddMinutes(10),
            Now.AddHours(12));

        Assert.True(TenantTerminationExportArtifactCoordinator.Confirm(
            process,
            artifact,
            process.Version,
            "owner:approver",
            Now.AddMinutes(11)).IsSuccess);
        Assert.Equal(artifact.Id, process.ExportArtifactId);
        Assert.Equal(artifact.Version, process.ExportArtifactVersion);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Export,
            process.OperationRevision,
            process.Version,
            "owner:approver",
            Now.AddMinutes(12)).IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Destroy, process.Phase);
    }

    [Fact]
    public void Prepare_rejects_catalogue_drift_and_short_lived_fragments()
    {
        StubContributor workspaces = new(
            "workspaces",
            WorkspacesCatalogSha,
            []);
        TenantTerminationProcess process = ExportingProcess(workspaces);
        ExportOwnerProof owner =
            AvailableOwner(process, workspaces, 1, 11);

        ExportOwnerProof driftedOwner = AvailableOwner(
            process,
            new StubContributor(
                "workspaces",
                InventoryCatalogSha,
                []),
            1,
            11);
        Assert.True(TenantTerminationExportArtifactCoordinator.Prepare(
            process,
            [driftedOwner.WorkItem],
            [owner.Fragment],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(8),
            Now.AddHours(12)).IsFailure);

        Assert.True(TenantTerminationExportArtifactCoordinator.Prepare(
            process,
            [owner.WorkItem],
            [owner.Fragment],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(8),
            owner.Fragment.ExpiresAtUtc.AddMinutes(1)).IsFailure);
    }

    private static TenantTerminationProcess ExportingProcess(
        params StubContributor[] frozenOwners)
    {
        TenantTerminationProcess process = TenantTerminationProcess.Prepare(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "tenant-a",
            Guid.NewGuid(),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            approvalRevision: 4,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            exportRequested: true,
            PolicySha,
            "owner:approver",
            Now,
            "system:tenant-termination",
            Now).Value;
        _ = process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            "owner:approver",
            Now.AddMinutes(1));
        _ = TenantTerminationTestFixture.CompleteFreeze(
            process,
            process.OperationRevision,
            process.Version,
            "owner:approver",
            Now.AddMinutes(2),
            FrozenSha,
            frozenOwners.Select(owner =>
                new TenantTerminationFrozenOwnerDescriptor(
                    owner.Descriptor.OwnerKey,
                    owner.Descriptor.ContractVersion,
                    owner.Descriptor.CatalogVersion,
                    owner.Descriptor.CatalogSha256))
                .ToArray());
        _ = process.BeginPhase(
            TenantTerminationProcessPhase.Export,
            process.Version,
            "owner:approver",
            Now.AddMinutes(3));
        return process;
    }

    private static ExportOwnerProof AvailableOwner(
        TenantTerminationProcess process,
        StubContributor contributor,
        byte marker,
        long proofRevision)
    {
        Guid id = GuidFrom(marker);
        Guid idempotencyKey = Guid.NewGuid();
        Guid runId = Guid.NewGuid();
        TenantTerminationOwnerWorkItem workItem =
            TenantTerminationOwnerWorkItem.Prepare(
                id,
                process.ScopeId,
                process.Id,
                process.CaseId,
                process.ApprovalRevision,
                process.OperationRevision,
                process.TerminationEpoch,
                idempotencyKey,
                TenantTerminationOwnerPhase.Export,
                contributor.Descriptor.OwnerKey,
                contributor.Descriptor.ContractVersion,
                contributor.Descriptor.CatalogVersion,
                contributor.Descriptor.CatalogSha256,
                process.PolicyEvidenceSha256,
                Now.AddMinutes(3)).Value;
        _ = workItem.BeginProcessing(
            runId,
            1,
            workItem.Version,
            Now.AddMinutes(4));
        TenantTerminationExportFragment fragment =
            TenantTerminationExportFragment.Request(
                id,
                process.ScopeId,
                process.Id,
                process.CaseId,
                process.ApprovalRevision,
                process.FreezeOperationRevision.GetValueOrDefault(),
                process.OperationRevision,
                process.TerminationEpoch,
                idempotencyKey,
                contributor.Descriptor.OwnerKey,
                contributor.Descriptor.ContractVersion,
                contributor.Descriptor.CatalogVersion,
                contributor.Descriptor.CatalogSha256,
                FrozenSha,
                process.PolicyEvidenceSha256,
                Now.AddMinutes(3),
                Now.AddHours(24)).Value;
        _ = fragment.BeginGeneration(runId, 1, Now.AddMinutes(4));
        string resultCode =
            $"{contributor.Descriptor.OwnerKey}.tenant-export.completed";
        _ = fragment.MarkAvailable(
            runId,
            1,
            recordCount: 1,
            proofRevision,
            proofRevision,
            resultCode,
            contributor.Descriptor.CatalogVersion,
            contributor.Descriptor.CatalogSha256,
            $"data-rights/tenant-exports/fragment-{id:N}.bftxf",
            encryptedByteLength: 2048,
            PlaintextSha,
            encryptionKeyVersion: 1,
            formatVersion: 1,
            Now.AddMinutes(5),
            Now.AddHours(24));
        _ = workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            resultCode,
            affectedCount: 1,
            retainedMinimumCount: 0,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            proofRevision,
            proofRevision,
            contributor.Descriptor.CatalogVersion,
            contributor.Descriptor.CatalogSha256,
            runId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(4).AddSeconds(30));
        return new(workItem, fragment);
    }

    private static Guid GuidFrom(byte marker)
    {
        byte[] bytes = new byte[16];
        bytes[0] = marker;
        return new Guid(bytes);
    }

    private sealed record ExportOwnerProof(
        TenantTerminationOwnerWorkItem WorkItem,
        TenantTerminationExportFragment Fragment);

    private sealed class StubContributor(
        string ownerKey,
        string catalogSha256,
        IReadOnlyCollection<string> dependencies)
        : ITenantTerminationContributor
    {
        public TenantTerminationContributorDescriptor Descriptor { get; } =
            new(
                ownerKey,
                TenantTerminationContract.CurrentVersion,
                [
                    new(
                        TenantTerminationContributionPhase.Export,
                        dependencies)
                ],
                MandatoryForProduction: true,
                CatalogVersion: 1,
                catalogSha256);

        public Task<TenantTerminationContributionResult> ExecuteAsync(
            TenantTerminationContributionRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The coordinator does not execute owner exports.");
    }
}

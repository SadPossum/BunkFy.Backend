namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationCancellationCoordinatorTests
{
    private const string TenantId = "tenant-a";
    private const string OwnerKey = "workspaces";
    private const string Actor = "system:tenant-termination";
    private static readonly string Digest = new('a', 64);
    private static readonly string CatalogDigest = new('b', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Matching_durable_restore_proof_completes_cancellation()
    {
        TenantTerminationProcess process = PrepareRestoringProcess();
        StubContributor contributor = Contributor();
        TenantTerminationOwnerWorkItem workItem =
            PrepareCompletedWorkItem(process, contributor.Descriptor);
        TenantTerminationCancellationCoordinator coordinator =
            new([contributor]);

        Result completed = coordinator.Complete(
            process,
            [workItem],
            process.OperationRevision,
            process.Version,
            Actor,
            Now.AddMinutes(7));

        Assert.True(completed.IsSuccess);
        Assert.Equal(
            TenantTerminationProcessStatus.Cancelled,
            process.Status);
        Assert.Equal(TenantTerminationProcessPhase.Restore, process.Phase);
    }

    [Fact]
    public void Missing_or_mismatched_restore_proof_fails_closed()
    {
        TenantTerminationProcess process = PrepareRestoringProcess();
        StubContributor contributor = Contributor();
        TenantTerminationCancellationCoordinator coordinator =
            new([contributor]);
        long selectedVersion = process.Version;

        Result missing = coordinator.Complete(
            process,
            [],
            process.OperationRevision,
            process.Version,
            Actor,
            Now.AddMinutes(7));

        Assert.True(missing.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationCancellationProofInvalid,
            missing.Error);
        Assert.Equal(selectedVersion, process.Version);
        Assert.Equal(TenantTerminationProcessStatus.Running, process.Status);

        TenantTerminationOwnerWorkItem mismatched =
            PrepareCompletedWorkItem(
                process,
                contributor.Descriptor with
                {
                    CatalogSha256 = new string('c', 64)
                });
        Result wrongCatalog = coordinator.Complete(
            process,
            [mismatched],
            process.OperationRevision,
            process.Version,
            Actor,
            Now.AddMinutes(8));

        Assert.True(wrongCatalog.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationCancellationProofInvalid,
            wrongCatalog.Error);
        Assert.Equal(TenantTerminationProcessStatus.Running, process.Status);
    }

    private static TenantTerminationProcess PrepareRestoringProcess()
    {
        TenantTerminationProcess process = TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 3,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "owner:approver",
            Now,
            Actor,
            Now).Value;
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Actor,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            process.OperationRevision,
            process.Version,
            Actor,
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(process.RequestCancellation(
            process.Version,
            Actor,
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Restore,
            process.Version,
            Actor,
            Now.AddMinutes(4)).IsSuccess);
        return process;
    }

    private static TenantTerminationOwnerWorkItem PrepareCompletedWorkItem(
        TenantTerminationProcess process,
        TenantTerminationContributorDescriptor descriptor)
    {
        TenantTerminationOwnerWorkItem workItem =
            TenantTerminationOwnerWorkItem.Prepare(
                Guid.NewGuid(),
                TenantId,
                process.Id,
                process.CaseId,
                process.ApprovalRevision,
                process.OperationRevision,
                process.TerminationEpoch,
                Guid.NewGuid(),
                TenantTerminationOwnerPhase.Restore,
                descriptor.OwnerKey,
                descriptor.ContractVersion,
                descriptor.CatalogVersion,
                descriptor.CatalogSha256,
                process.PolicyEvidenceSha256,
                Now.AddMinutes(4)).Value;
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(5)).IsSuccess);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            "workspace.termination.released",
            affectedCount: 1,
            retainedMinimumCount: 0,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: 1,
            resultingProofRevision: 2,
            descriptor.CatalogVersion,
            descriptor.CatalogSha256,
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(6)).IsSuccess);
        return workItem;
    }

    private static StubContributor Contributor() =>
        new(new(
            OwnerKey,
            TenantTerminationContract.CurrentVersion,
            [TenantTerminationContributionPhase.Restore],
            [],
            MandatoryForProduction: true,
            CatalogVersion: 1,
            CatalogSha256: CatalogDigest));

    private sealed class StubContributor(
        TenantTerminationContributorDescriptor descriptor)
        : ITenantTerminationContributor
    {
        public TenantTerminationContributorDescriptor Descriptor { get; } =
            descriptor;

        public Task<TenantTerminationContributionResult> ExecuteAsync(
            TenantTerminationContributionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

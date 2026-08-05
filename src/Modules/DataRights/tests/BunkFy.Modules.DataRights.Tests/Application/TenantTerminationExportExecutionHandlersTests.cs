namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationExportExecutionHandlersTests
{
    private const string TenantId = "tenant-a";
    private const string OwnerKey = "workspaces";
    private const string Executor = "system:tenant-termination";
    private static readonly string Digest = new('a', 64);
    private static readonly string FrozenDigest = new('b', 64);
    private static readonly string PlaintextDigest = new('c', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Protected_fragment_and_artifact_complete_with_exact_proof()
    {
        StubContributor contributor = new();
        TenantTerminationProcess process = ExportingProcess(contributor);
        TenantTerminationPhasePlanner planner = new([contributor]);
        TenantTerminationOwnerWorkItem workItem = Assert.Single(
            planner.PrepareWorkItems(process, Now.AddMinutes(3)).Value);
        Guid ownerRunId = TenantTerminationExecutionIdentity.CreateTaskRunId(
            workItem.Id,
            dispatchSequence: 1);
        Assert.True(workItem.BeginProcessing(
            ownerRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(4)).IsSuccess);

        StubTerminationRepository repository = new(process, [workItem]);
        StubFragmentRepository fragments = new();
        BeginTenantTerminationExportFragmentGenerationCommandHandler beginFragment =
            new(
                repository,
                fragments,
                new FixedArtifactPolicy(TimeSpan.FromHours(24)),
                planner,
                new FixedClock(Now.AddMinutes(4).AddSeconds(10)));
        BeginTenantTerminationExportFragmentGenerationCommand beginCommand = new(
            process.Id,
            workItem.Id,
            process.OperationRevision,
            workItem.OwnerKey,
            ownerRunId,
            TaskAttempt: 1,
            workItem.Version);

        Result<TenantTerminationExportFragmentGenerationStart> begun =
            await beginFragment.HandleAsync(
                beginCommand,
                CancellationToken.None);

        Assert.True(begun.IsSuccess);
        TenantTerminationExportFragment fragment = Assert.Single(
            fragments.Items);
        Assert.Equal(
            TenantTerminationExportFragmentState.Generating,
            fragment.State);
        Assert.Equal(
            process.FrozenRevisionSha256,
            begun.Value.Request.AssemblyRequest.FrozenRevisionSha256);
        Assert.Equal(
            process.WorkspaceFenceRevision,
            begun.Value.Request.AssemblyRequest.WorkspaceFenceRevision);

        TenantTerminationContributionRequest ownerRequest = new(
            workItem.OwnerContractVersion,
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            process.OperationRevision,
            process.TerminationEpoch,
            TenantTerminationContributionPhase.Export,
            workItem.Id,
            workItem.IdempotencyKey,
            process.PolicyEvidenceSha256,
            Executor,
            begun.Value.Request.AssemblyRequest.DeadlineUtc);
        TenantTerminationReplayDispatch dispatch =
            TenantTerminationReplayDispatch.Create(
                ownerRequest,
                workItem.OwnerKey,
                workItem.CatalogVersion,
                workItem.CatalogSha256,
                TenantTerminationExecutionBoundary.TenantScopedTask,
                ownerRunId,
                taskAttempt: 1,
                Now.AddMinutes(4).AddSeconds(20));
        TenantTerminationContributionResult contribution = new(
            TenantTerminationContributionStatus.Completed,
            "workspaces.tenant-export.completed",
            AffectedCount: 2,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 0,
            HoldReviewAtUtc: null,
            SelectedProofRevision: 11,
            ResultingProofRevision: 11,
            workItem.CatalogVersion,
            workItem.CatalogSha256,
            Now.AddMinutes(5));
        TenantTerminationReplayResult replayResult =
            TenantTerminationReplayResult.Create(
                dispatch,
                contribution,
                Now.AddMinutes(5).AddSeconds(1));
        StubTenantTerminationReplayStore replayStore = new();
        replayStore.Add(new(dispatch, replayResult));
        RecordingTenantTerminationCoordinationSignal signal = new();
        RecordTenantTerminationOwnerResultCommandHandler recorder = new(
            repository,
            replayStore,
            planner,
            signal);
        CompleteTenantTerminationExportFragmentGenerationCommandHandler
            completeFragment = new(
                repository,
                fragments,
                replayStore,
                new RecordResultDispatcher(recorder));
        TenantTerminationProtectedExportFragment protectedFragment = new(
            new(
                FrozenDigest,
                new(
                    OwnerKey,
                    RecordCount: 2,
                    SelectedProofRevision: 11,
                    ResultingProofRevision: 11,
                    contribution.ResultCode,
                    contribution.RecordedAtUtc)),
            "data-rights/tenant-exports/workspaces.bftxf",
            EncryptedByteLength: 2048,
            PlaintextDigest,
            EncryptionKeyVersion: 1,
            FormatVersion: 1,
            AvailableAtUtc: Now.AddMinutes(6),
            ExpiresAtUtc: fragment.ExpiresAtUtc);

        Result<TenantTerminationOwnerResultRecorded> fragmentCompleted =
            await completeFragment.HandleAsync(
                new(
                    process.Id,
                    workItem.Id,
                    process.OperationRevision,
                    workItem.OwnerKey,
                    ownerRunId,
                    TaskAttempt: 1,
                    workItem.Version,
                    begun.Value.FragmentVersion,
                    protectedFragment),
                CancellationToken.None);

        Assert.True(fragmentCompleted.IsSuccess);
        Assert.Equal(TenantTerminationExportFragmentState.Available, fragment.State);
        Assert.Equal(TenantTerminationOwnerWorkState.Completed, workItem.State);

        StubArtifactRepository artifacts = new();
        Guid artifactRunId = TenantTerminationExecutionIdentity
            .CreateExportArtifactTaskRunId(
                process.Id,
                process.OperationRevision);
        BeginTenantTerminationExportArtifactGenerationCommandHandler beginArtifact =
            new(
                repository,
                fragments,
                artifacts,
                signal,
                new FixedClock(Now.AddMinutes(7)));
        Result<TenantTerminationExportArtifactGenerationStart> artifactBegun =
            await beginArtifact.HandleAsync(
                new(
                    process.Id,
                    process.OperationRevision,
                    artifactRunId,
                    TaskAttempt: 1),
                CancellationToken.None);

        Assert.True(artifactBegun.IsSuccess);
        Assert.True(artifactBegun.Value.DispatchRequired);
        Assert.Equal(
            TenantTerminationExecutionIdentity.CreateExportArtifactId(
                process.Id,
                process.OperationRevision),
            artifactBegun.Value.Artifact.Id);
        TenantTerminationExportArtifact artifact = artifactBegun.Value.Artifact;
        CompleteTenantTerminationExportArtifactGenerationCommandHandler
            completeArtifact = new(
                repository,
                artifacts,
                signal,
                new FixedClock(Now.AddMinutes(9)));
        TenantTerminationProtectedExportArtifact protectedArtifact = new(
            FragmentCount: 1,
            RecordCount: 2,
            artifact.FragmentSetSha256,
            "data-rights/tenant-exports/final.bftxa",
            EncryptedByteLength: 4096,
            PlaintextDigest,
            EncryptionKeyVersion: 1,
            FormatVersion: 1,
            AvailableAtUtc: Now.AddMinutes(8),
            ExpiresAtUtc: artifact.ExpiresAtUtc);

        Result<TenantTerminationExportArtifactGenerationCompleted>
            artifactCompleted = await completeArtifact.HandleAsync(
                new(
                    process.Id,
                    process.OperationRevision,
                    artifact.Id,
                    artifactRunId,
                    TaskAttempt: 1,
                    artifactBegun.Value.ProcessVersion,
                    artifactBegun.Value.ArtifactVersion,
                    protectedArtifact),
                CancellationToken.None);

        Assert.True(artifactCompleted.IsSuccess);
        Assert.Equal(TenantTerminationExportArtifactState.Available, artifact.State);
        Assert.True(process.HasCurrentExportConfirmation());
        Assert.Equal(artifact.Id, process.ExportArtifactId);
        Assert.Equal(2, signal.Captures.Count);
    }

    private static TenantTerminationProcess ExportingProcess(
        StubContributor contributor)
    {
        TenantTerminationProcess process = TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 4,
            Guid.NewGuid(),
            exportRequested: true,
            Digest,
            "owner:approver",
            Now,
            "creator",
            Now).Value;
        _ = process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Executor,
            Now.AddMinutes(1));
        _ = process.CompleteFreeze(
            process.OperationRevision,
            workspaceFenceRevision: 9,
            FrozenDigest,
            [new(
                OwnerKey,
                contributor.Descriptor.ContractVersion,
                contributor.Descriptor.CatalogVersion,
                contributor.Descriptor.CatalogSha256)],
            process.Version,
            Executor,
            Now.AddMinutes(2));
        _ = process.BeginPhase(
            TenantTerminationProcessPhase.Export,
            process.Version,
            Executor,
            Now.AddMinutes(3));
        return process;
    }

    private sealed class StubContributor : ITenantTerminationContributor
    {
        public TenantTerminationContributorDescriptor Descriptor { get; } =
            new(
                OwnerKey,
                TenantTerminationContract.CurrentVersion,
                [new(TenantTerminationContributionPhase.Export, [])],
                MandatoryForProduction: true,
                CatalogVersion: 1,
                CatalogSha256: Digest);

        public Task<TenantTerminationContributionResult> ExecuteAsync(
            TenantTerminationContributionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubTerminationRepository(
        TenantTerminationProcess process,
        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems)
        : ITenantTerminationRepository
    {
        public Task AddProcessAsync(
            TenantTerminationProcess candidate,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddOwnerWorkItemAsync(
            TenantTerminationOwnerWorkItem workItem,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantTerminationProcess?> GetProcessAsync(
            Guid processId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(
                process.Id == processId ? process : null);

        public Task<TenantTerminationProcess?> GetActiveProcessAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(process);

        public Task<TenantTerminationProcess?> GetProcessByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(
                process.IdempotencyKey == idempotencyKey ? process : null);

        public Task<TenantTerminationOwnerWorkItem?> GetOwnerWorkItemAsync(
            Guid processId,
            TenantTerminationOwnerPhase phase,
            string ownerKey,
            long operationRevision,
            CancellationToken cancellationToken) =>
            Task.FromResult(workItems.SingleOrDefault(item =>
                item.ProcessId == processId &&
                item.Phase == phase &&
                item.OwnerKey == ownerKey &&
                item.OperationRevision == operationRevision));

        public Task<IReadOnlyList<TenantTerminationOwnerWorkItem>>
            ListOwnerWorkItemsAsync(
                Guid processId,
                TenantTerminationOwnerPhase phase,
                long operationRevision,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantTerminationOwnerWorkItem>>(
                workItems.Where(item =>
                    item.ProcessId == processId &&
                    item.Phase == phase &&
                    item.OperationRevision == operationRevision).ToArray());
    }

    private sealed class StubFragmentRepository
        : ITenantTerminationExportFragmentRepository
    {
        public List<TenantTerminationExportFragment> Items { get; } = [];

        public Task AddAsync(
            TenantTerminationExportFragment fragment,
            CancellationToken cancellationToken)
        {
            this.Items.Add(fragment);
            return Task.CompletedTask;
        }

        public Task<TenantTerminationExportFragment?> GetAsync(
            Guid workItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item =>
                item.Id == workItemId));

        public Task<TenantTerminationExportFragment?>
            GetByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item =>
                item.IdempotencyKey == idempotencyKey));

        public Task<IReadOnlyList<TenantTerminationExportFragment>> ListAsync(
            Guid processId,
            long exportOperationRevision,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantTerminationExportFragment>>(
                this.Items.Where(item =>
                    item.ProcessId == processId &&
                    item.ExportOperationRevision == exportOperationRevision)
                    .ToArray());
    }

    private sealed class StubArtifactRepository
        : ITenantTerminationExportArtifactRepository
    {
        private readonly List<TenantTerminationExportArtifact> items = [];

        public Task AddAsync(
            TenantTerminationExportArtifact artifact,
            CancellationToken cancellationToken)
        {
            this.items.Add(artifact);
            return Task.CompletedTask;
        }

        public Task<TenantTerminationExportArtifact?> GetAsync(
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.items.SingleOrDefault(item =>
                item.Id == artifactId));

        public Task<TenantTerminationExportArtifact?>
            GetByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult(this.items.SingleOrDefault(item =>
                item.IdempotencyKey == idempotencyKey));

        public Task<TenantTerminationExportArtifact?> GetByProcessAsync(
            Guid processId,
            long exportOperationRevision,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.items.SingleOrDefault(item =>
                item.ProcessId == processId &&
                item.ExportOperationRevision == exportOperationRevision));
    }

    private sealed class RecordResultDispatcher(
        RecordTenantTerminationOwnerResultCommandHandler handler)
        : IRequestDispatcher
    {
        public async Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            if (command is not RecordTenantTerminationOwnerResultCommand record)
            {
                throw new NotSupportedException();
            }

            Result<TenantTerminationOwnerResultRecorded> result =
                await handler.HandleAsync(
                    record,
                    cancellationToken);
            return (Result<TResponse>)(object)result;
        }

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedArtifactPolicy(TimeSpan lifetime)
        : IDataRightsExportArtifactPolicy
    {
        public DateTimeOffset ExpiresAt(DateTimeOffset requestedAtUtc) =>
            requestedAtUtc.Add(lifetime);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

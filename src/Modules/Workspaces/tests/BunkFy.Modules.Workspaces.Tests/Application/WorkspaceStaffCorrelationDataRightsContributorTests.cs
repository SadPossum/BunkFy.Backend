namespace BunkFy.Modules.Workspaces.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Contributors;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    WorkspaceStaffCorrelationDataRightsContributorTests
{
    private static readonly Guid StaffMemberId = Guid.NewGuid();
    private static readonly Guid AnchorId = Guid.NewGuid();

    [Fact]
    public async Task Eligible_staff_expands_exact_workspace_anchor()
    {
        StubCorrelationRepository repository = new(
            EligibleSnapshot());
        WorkspaceStaffCorrelationRequiredCompanionContributor contributor =
            new(
                new StubStaffStateReader(
                    new(
                        StaffMemberId,
                        7,
                        StaffAnonymisationRestoreRecordState
                            .Departed,
                        "subject-a",
                        AnonymisedAtUtc: null)),
                repository,
                NullLogger<
                    WorkspaceStaffCorrelationRequiredCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                Request(),
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Completed,
            result.Status);
        DataRightsSubjectCoordinate coordinate =
            Assert.Single(result.Coordinates);
        Assert.Equal(
            WorkspacesDataRightsCoordinates.Owner,
            coordinate.OwnerKey);
        Assert.Equal(
            WorkspacesDataRightsCoordinates
                .StaffAccessProcessRecordType,
            coordinate.RecordType);
        Assert.Equal(AnchorId, coordinate.RecordId);
        Assert.Equal(4, coordinate.RecordVersion);
        Assert.Equal("subject-a", repository.SubjectId);
    }

    [Fact]
    public async Task Staff_without_workspace_correlation_adds_no_companion()
    {
        WorkspaceStaffCorrelationRequiredCompanionContributor contributor =
            new(
                new StubStaffStateReader(
                    new(
                        StaffMemberId,
                        7,
                        StaffAnonymisationRestoreRecordState
                            .Departed,
                        AuthSubjectId: null,
                        AnonymisedAtUtc: null)),
                new StubCorrelationRepository(
                    WorkspaceStaffCorrelationAnonymisationSnapshot
                        .NoCorrelation()),
                NullLogger<
                    WorkspaceStaffCorrelationRequiredCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                Request(),
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Completed,
            result.Status);
        Assert.Empty(result.Coordinates);
    }

    [Fact]
    public async Task No_correlation_is_valid_at_selection_capacity()
    {
        WorkspaceStaffCorrelationRequiredCompanionContributor contributor =
            new(
                new StubStaffStateReader(
                    new(
                        StaffMemberId,
                        7,
                        StaffAnonymisationRestoreRecordState
                            .Departed,
                        AuthSubjectId: null,
                        AnonymisedAtUtc: null)),
                new StubCorrelationRepository(
                    WorkspaceStaffCorrelationAnonymisationSnapshot
                        .NoCorrelation()),
                NullLogger<
                    WorkspaceStaffCorrelationRequiredCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                Request() with { RemainingSubjectCapacity = 0 },
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Completed,
            result.Status);
        Assert.Empty(result.Coordinates);
    }

    [Theory]
    [InlineData(
        WorkspaceStaffCorrelationAnonymisationSnapshotStatus
            .ActiveOnboarding)]
    [InlineData(
        WorkspaceStaffCorrelationAnonymisationSnapshotStatus
            .ActiveAccessProcess)]
    [InlineData(
        WorkspaceStaffCorrelationAnonymisationSnapshotStatus
            .Conflict)]
    [InlineData(
        WorkspaceStaffCorrelationAnonymisationSnapshotStatus
            .Oversized)]
    public async Task Unsafe_workspace_state_blocks_expansion(
        WorkspaceStaffCorrelationAnonymisationSnapshotStatus status)
    {
        WorkspaceStaffCorrelationRequiredCompanionContributor contributor =
            new(
                new StubStaffStateReader(
                    new(
                        StaffMemberId,
                        7,
                        StaffAnonymisationRestoreRecordState
                            .Departed,
                        "subject-a",
                        AnonymisedAtUtc: null)),
                new StubCorrelationRepository(
                    Snapshot(status)),
                NullLogger<
                    WorkspaceStaffCorrelationRequiredCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                Request(),
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Blocked,
            result.Status);
        Assert.Empty(result.Coordinates);
        Assert.False(string.IsNullOrWhiteSpace(result.OutcomeCode));
    }

    [Fact]
    public async Task Policy_contribution_points_to_staff_authority()
    {
        WorkspaceStaffCorrelationAnonymisationPolicyContributor contributor =
            new(new StubCorrelationRepository(EligibleSnapshot()));

        DataRightsAnonymisationPolicyContributionResult result =
            await contributor.EvaluateAsync(
                new(
                    DataRightsAnonymisationPolicyContract
                        .CurrentVersion,
                    "tenant-a",
                    DataRightsCaseType.StaffRights,
                    PropertyId: null,
                    Guid.NewGuid(),
                    new(
                        WorkspacesDataRightsCoordinates.Owner,
                        WorkspacesDataRightsCoordinates
                            .StaffAccessProcessRecordType,
                        AnchorId,
                        4)),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationPolicyContributionStatus
                .Approved,
            result.Status);
        Assert.Equal(
            DataRightsAnonymisationPolicyContributionRole
                .Companion,
            result.Role);
        Assert.Equal(
            new DataRightsSubjectCoordinate(
                StaffDataRightsCoordinates.Owner,
                StaffDataRightsCoordinates
                    .StaffMemberRecordType,
                StaffMemberId,
                7),
            result.AuthorityCoordinate);
        DataRightsApprovalEvidenceBinding binding =
            Assert.Single(result.StateBindings);
        Assert.Equal(
            WorkspacesDataRightsCoordinates
                .StaffCorrelationStateBindingKey,
            binding.Key);
        Assert.Equal(4, binding.Version);
        Assert.Equal(new string('a', 64), binding.Sha256);
    }

    [Fact]
    public async Task Restore_contributor_maps_exact_owner_proof()
    {
        DataRightsAnonymisationRestoreRequestV3 request =
            CreateRestoreRequest();
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt receipt =
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt.Create(
                request.TenantId,
                request.LedgerEntryId,
                request.TenantSequence,
                request.LedgerEntrySha256,
                request.RecordId,
                StaffMemberId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingRecordVersion,
                new string('b', 64),
                onboardingRecordsScrubbed: 1,
                accessProcessRecordsScrubbed: 1,
                accessPlanRecordsScrubbed: 1,
                request.OriginallyCompletedAtUtc,
                tombstoneRevision: 2,
                replayedAtUtc: new DateTimeOffset(
                    2026,
                    7,
                    30,
                    12,
                    0,
                    0,
                    TimeSpan.Zero)).Value;
        FakeRestoreDispatcher dispatcher =
            new(Result.Success(receipt));
        WorkspaceStaffCorrelationDataRightsAnonymisationRestoreContributor
            contributor = new(dispatcher);

        DataRightsAnonymisationRestoreResult result =
            await contributor.RestoreAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationRestoreStatus.Completed,
            result.Status);
        DataRightsAnonymisationRestoreProof proof =
            Assert.IsType<DataRightsAnonymisationRestoreProof>(
                result.Proof);
        Assert.Equal(receipt.LedgerEntryId, proof.LedgerEntryId);
        Assert.Equal(receipt.OwnerReceiptId, proof.OwnerReceiptId);
        Assert.Equal(
            receipt.OwnerReceiptSha256,
            proof.OwnerReceiptSha256);
        Assert.Equal(
            receipt.ResultingAnchorVersion,
            proof.ResultingRecordVersion);
        Assert.Equal(
            receipt.TombstoneRevision,
            proof.TombstoneRevision);
        Assert.Equal(receipt.ReplayedAtUtc, proof.ReplayedAtUtc);
        RestoreWorkspaceStaffCorrelationAnonymisationCommand command =
            Assert.IsType<
                RestoreWorkspaceStaffCorrelationAnonymisationCommand>(
                dispatcher.Command);
        Assert.Same(request, command.Request);
    }

    [Fact]
    public async Task Restore_contributor_maps_failure_without_proof()
    {
        FakeRestoreDispatcher dispatcher = new(
            Result.Failure<
                WorkspaceStaffCorrelationAnonymisationRestoreReceipt>(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .RestoreProofConflict));
        WorkspaceStaffCorrelationDataRightsAnonymisationRestoreContributor
            contributor = new(dispatcher);

        DataRightsAnonymisationRestoreResult result =
            await contributor.RestoreAsync(
                CreateRestoreRequest(),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationRestoreStatus.Failed,
            result.Status);
        Assert.Equal(
            WorkspaceStaffCorrelationAnonymisationApplicationErrors
                .RestoreProofConflict.Code,
            result.OutcomeCode);
        Assert.Null(result.Proof);
    }

    private static DataRightsRequiredCompanionRequest Request() =>
        new(
            DataRightsRequiredCompanionContract.CurrentVersion,
            "tenant-a",
            DataRightsCaseType.StaffRights,
            DataRightsOperation.Anonymisation,
            PropertyId: null,
            Guid.NewGuid(),
            new(
                StaffDataRightsCoordinates.Owner,
                StaffDataRightsCoordinates.StaffMemberRecordType,
                StaffMemberId,
                7),
            RemainingSubjectCapacity: 99);

    private static WorkspaceStaffCorrelationAnonymisationSnapshot
        EligibleSnapshot() =>
        new(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .Eligible,
            AnchorId,
            AnchorProcessVersion: 4,
            StaffMemberId,
            SelectedStaffVersion: 7,
            "subject-a",
            new string('a', 64),
            OnboardingRecordCount: 1,
            AccessProcessRecordCount: 2,
            AccessPlanRecordCount: 1);

    private static WorkspaceStaffCorrelationAnonymisationSnapshot Snapshot(
        WorkspaceStaffCorrelationAnonymisationSnapshotStatus status) =>
        EligibleSnapshot() with { Status = status };

    private static DataRightsAnonymisationRestoreRequestV3
        CreateRestoreRequest() =>
        new(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            TenantSequence: 3,
            new string('a', 64),
            DataRightsCaseType.StaffRights,
            DataRightsExecutionScopeKind.Tenant,
            RoutingPropertyId: null,
            WorkspacesDataRightsCoordinates.Owner,
            WorkspacesDataRightsCoordinates
                .StaffAccessProcessRecordType,
            AnchorId,
            OwnerReceiptContractVersion: 1,
            Guid.NewGuid(),
            new string('b', 64),
            ResultingRecordVersion: 5,
            new DateTimeOffset(
                2026,
                7,
                29,
                12,
                0,
                0,
                TimeSpan.Zero));

    private sealed class StubStaffStateReader(
        StaffAnonymisationRestoreState? state)
        : IStaffAnonymisationRestoreStateReader
    {
        public Task<StaffAnonymisationRestoreState?> ReadAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult(state);
    }

    private sealed class StubCorrelationRepository(
        WorkspaceStaffCorrelationAnonymisationSnapshot snapshot)
        : IWorkspaceStaffCorrelationAnonymisationRepository
    {
        public string? SubjectId { get; private set; }

        public Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
            ResolveAsync(
                string tenantId,
                Guid staffMemberId,
                long selectedStaffVersion,
                string? subjectId,
                CancellationToken cancellationToken)
        {
            this.SubjectId = subjectId;
            return Task.FromResult(snapshot);
        }

        public Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
            ReadAsync(
                string tenantId,
                Guid anchorProcessId,
                long selectedAnchorVersion,
                CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);

        public Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
            ReadAnonymisedAsync(
                string tenantId,
                Guid anchorProcessId,
                long resultingAnchorVersion,
                Guid ownerReceiptId,
                CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);

        public Task<WorkspaceStaffCorrelationAnonymisationReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult<
                WorkspaceStaffCorrelationAnonymisationReceipt?>(null);

        public Task<
            WorkspaceStaffCorrelationAnonymisationTombstone?>
            GetTombstoneAsync(
                Guid anchorProcessId,
                CancellationToken cancellationToken) =>
            Task.FromResult<
                WorkspaceStaffCorrelationAnonymisationTombstone?>(null);

        public Task<
            WorkspaceStaffCorrelationAnonymisationTombstone?>
            FindTombstoneAsync(
                string tenantId,
                Guid staffMemberId,
                long selectedStaffVersion,
                CancellationToken cancellationToken) =>
            Task.FromResult<
                WorkspaceStaffCorrelationAnonymisationTombstone?>(null);

        public Task<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt?>
            GetRestoreReceiptAsync(
                Guid ledgerEntryId,
                CancellationToken cancellationToken) =>
            Task.FromResult<
                WorkspaceStaffCorrelationAnonymisationRestoreReceipt?>(
                null);

        public Task<Result<
            WorkspaceStaffCorrelationAnonymisationReceipt>>
            ApplyAsync(
                WorkspaceStaffCorrelationAnonymisationApplyRequest
                    request,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>>
            RestoreAsync(
                WorkspaceStaffCorrelationAnonymisationRestoreRequest
                    request,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRestoreDispatcher(
        Result<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
            result)
        : IRequestDispatcher
    {
        public object? Command { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            this.Command = command;
            return Task.FromResult(
                (Result<TResponse>)(object)result);
        }

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

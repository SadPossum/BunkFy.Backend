namespace BunkFy.Modules.Workspaces.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using BunkFy.Modules.Workspaces.Tests;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    RestoreWorkspaceStaffCorrelationAnonymisationCommandHandlerTests
{
    private const string TenantId = "tenant-a";
    private static readonly Guid AnchorProcessId = Guid.NewGuid();
    private static readonly Guid StaffMemberId = Guid.NewGuid();
    private static readonly DateTimeOffset OriginallyCompletedAtUtc =
        new(2026, 7, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReplayedAtUtc =
        new(2026, 7, 30, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Valid_restore_is_serialized_and_forwards_exact_proof()
    {
        DataRightsAnonymisationRestoreRequestV3 request =
            CreateRequest();
        RecordingRepository repository = new();
        List<string> calls = [];
        RecordingWorkspaceCrossGraphMutationLock crossGraphLock =
            new(calls);
        RecordingOperationLock operationLock = new(calls: calls);
        RestoreWorkspaceStaffCorrelationAnonymisationCommandHandler
            handler = new(
                repository,
                crossGraphLock,
                WorkspaceStaffAccessMutationTestSupport.Create(
                    calls: calls),
                operationLock,
                new TestScopeContext(),
                new TestClock());

        Result<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
            result = await handler.HandleAsync(
                new(
                    request),
                CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(1, crossGraphLock.AcquireCount);
        Assert.Equal(1, operationLock.AcquireCount);
        Assert.Equal(
            ["tenant-exclusive", "staff-coordinate", "correlation-row"],
            calls);
        WorkspaceStaffCorrelationAnonymisationRestoreRequest
            forwarded = Assert.IsType<
                WorkspaceStaffCorrelationAnonymisationRestoreRequest>(
                repository.Request);
        Assert.Equal(TenantId, forwarded.TenantId);
        Assert.Equal(request.LedgerEntryId, forwarded.LedgerEntryId);
        Assert.Equal(request.TenantSequence, forwarded.TenantSequence);
        Assert.Equal(
            request.LedgerEntrySha256,
            forwarded.LedgerEntrySha256);
        Assert.Equal(request.RecordId, forwarded.AnchorProcessId);
        Assert.Equal(
            request.OwnerReceiptContractVersion,
            forwarded.OwnerReceiptContractVersion);
        Assert.Equal(request.OwnerReceiptId, forwarded.OwnerReceiptId);
        Assert.Equal(
            request.OwnerReceiptSha256,
            forwarded.OwnerReceiptSha256);
        Assert.Equal(
            request.ResultingRecordVersion,
            forwarded.ResultingAnchorVersion);
        Assert.Equal(
            OriginallyCompletedAtUtc,
            forwarded.OriginallyCompletedAtUtc);
        Assert.Equal(ReplayedAtUtc, forwarded.ReplayedAtUtc);
    }

    [Fact]
    public async Task Lock_contention_fails_without_touching_repository()
    {
        RecordingRepository repository = new();
        RestoreWorkspaceStaffCorrelationAnonymisationCommandHandler
            handler = new(
                repository,
                new RecordingWorkspaceCrossGraphMutationLock(),
                WorkspaceStaffAccessMutationTestSupport.Create(),
                new RecordingOperationLock(acquired: false),
                new TestScopeContext(),
                new TestClock());

        Result<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
            result = await handler.HandleAsync(
                new(
                    CreateRequest()),
                CancellationToken.None);

        Assert.Equal(
            WorkspaceStaffCorrelationAnonymisationApplicationErrors
                .RestoreProofConflict,
            result.Error);
        Assert.Null(repository.Request);
    }

    [Fact]
    public async Task Invalid_owner_coordinate_fails_before_lock()
    {
        RecordingOperationLock operationLock = new();
        RecordingWorkspaceCrossGraphMutationLock crossGraphLock =
            new();
        RecordingRepository repository = new();
        RestoreWorkspaceStaffCorrelationAnonymisationCommandHandler
            handler = new(
                repository,
                crossGraphLock,
                WorkspaceStaffAccessMutationTestSupport.Create(),
                operationLock,
                new TestScopeContext(),
                new TestClock());

        Result<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
            result = await handler.HandleAsync(
                new(
                    CreateRequest() with
                    {
                        OwnerKey = "another-owner"
                    }),
                CancellationToken.None);

        Assert.Equal(
            WorkspaceStaffCorrelationAnonymisationApplicationErrors
                .RestoreRequestInvalid,
            result.Error);
        Assert.Equal(0, operationLock.AcquireCount);
        Assert.Equal(0, crossGraphLock.AcquireCount);
        Assert.Null(repository.Request);
    }

    private static DataRightsAnonymisationRestoreRequestV3
        CreateRequest() =>
        new(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            TenantId,
            Guid.NewGuid(),
            TenantSequence: 3,
            new string('a', 64),
            DataRightsCaseType.StaffRights,
            DataRightsExecutionScopeKind.Tenant,
            RoutingPropertyId: null,
            WorkspacesDataRightsCoordinates.Owner,
            WorkspacesDataRightsCoordinates
                .StaffAccessProcessRecordType,
            AnchorProcessId,
            OwnerReceiptContractVersion: 1,
            Guid.NewGuid(),
            new string('b', 64),
            ResultingRecordVersion: 5,
            OriginallyCompletedAtUtc);

    private sealed class RecordingRepository
        : IWorkspaceStaffCorrelationAnonymisationRepository
    {
        public WorkspaceStaffCorrelationAnonymisationRestoreRequest?
            Request
        { get; private set; }

        public Task<Result<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>>
            RestoreAsync(
                WorkspaceStaffCorrelationAnonymisationRestoreRequest
                    request,
                CancellationToken cancellationToken)
        {
            this.Request = request;
            return Task.FromResult(
                WorkspaceStaffCorrelationAnonymisationRestoreReceipt
                    .Create(
                        request.TenantId,
                        request.LedgerEntryId,
                        request.TenantSequence,
                        request.LedgerEntrySha256,
                        request.AnchorProcessId,
                        StaffMemberId,
                        request.OwnerReceiptContractVersion,
                        request.OwnerReceiptId,
                        request.OwnerReceiptSha256,
                        request.ResultingAnchorVersion,
                        new string('c', 64),
                        onboardingRecordsScrubbed: 1,
                        accessProcessRecordsScrubbed: 1,
                        accessPlanRecordsScrubbed: 1,
                        request.OriginallyCompletedAtUtc,
                        tombstoneRevision: 2,
                        replayedAtUtc:
                            request.ReplayedAtUtc));
        }

        public Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
            ResolveAsync(
                string tenantId,
                Guid staffMemberId,
                long selectedStaffVersion,
                string? subjectId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
            ReadAsync(
                string tenantId,
                Guid anchorProcessId,
                long selectedAnchorVersion,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
            ReadAnonymisedAsync(
                string tenantId,
                Guid anchorProcessId,
                long resultingAnchorVersion,
                Guid ownerReceiptId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffCorrelationAnonymisationReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffCorrelationAnonymisationTombstone?>
            GetTombstoneAsync(
                Guid anchorProcessId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffCorrelationAnonymisationTombstone?>
            FindTombstoneAsync(
                string tenantId,
                Guid staffMemberId,
                long selectedStaffVersion,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt?>
            GetRestoreReceiptAsync(
                Guid ledgerEntryId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<
            WorkspaceStaffCorrelationAnonymisationReceipt>>
            ApplyAsync(
                WorkspaceStaffCorrelationAnonymisationApplyRequest
                    request,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingOperationLock(
        bool acquired = true,
        List<string>? calls = null)
        : IWorkspaceStaffCorrelationOperationLock
    {
        public int AcquireCount { get; private set; }

        public Task<bool> TryAcquireAsync(
            Guid anchorProcessId,
            CancellationToken cancellationToken)
        {
            this.AcquireCount++;
            Assert.Equal(AnchorProcessId, anchorProcessId);
            calls?.Add("correlation-row");
            return Task.FromResult(acquired);
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => ReplayedAtUtc;
    }
}

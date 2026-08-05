namespace BunkFy.Extensions.DataRights.AccessControl.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AccessControlTenantTerminationContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 7, 30, 0, TimeSpan.Zero);
    private static readonly Guid WorkspaceId =
        Guid.Parse("08b74d32-84da-4b44-80cc-b74dc8d5bacc");
    private static readonly string TenantId = WorkspaceId.ToString("D");
    private static readonly AccessScope RootScope =
        WorkspaceAccessScopes.Create(TenantId);
    private static readonly AccessControlScopeCoordinate Coordinate =
        new(RootScope, TenantId);
    private static readonly Guid ProcessId =
        Guid.Parse("74875bfc-05bb-474a-a182-12552252108a");
    private static readonly Guid TerminationEpoch =
        Guid.Parse("c8d15486-3678-47ee-9365-c74a27f3bea9");

    [Fact]
    public void Descriptor_is_the_terminal_bunkfy_access_control_owner()
    {
        AccessControlTenantTerminationContributor contributor =
            CreateContributor(new TestLifecycle());

        Assert.Equal(
            AccessControlTenantTerminationMetadata.OwnerKey,
            contributor.Descriptor.OwnerKey);
        Assert.Equal(
            [
                TenantTerminationContributionPhase.Export,
                TenantTerminationContributionPhase.Destroy
            ],
            contributor.Descriptor.PhasePlans.Select(plan => plan.Phase));
        Assert.All(
            contributor.Descriptor.PhasePlans,
            plan => Assert.Equal(
                AccessControlTenantTerminationMetadata.DependencyOwnerKeys,
                plan.DependsOnOwnerKeys));
        Assert.True(contributor.Descriptor.MandatoryForProduction);
        Assert.Equal(
            AccessControlTenantTerminationMetadata.ExportFieldIds
                .Order(StringComparer.Ordinal),
            contributor.ExportDescriptor.FieldIds
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Missing_scope_exports_a_stable_empty_revision_zero()
    {
        bool exported = false;
        TestLifecycle lifecycle = new()
        {
            Export = (_, _) =>
            {
                exported = true;
                throw new InvalidOperationException();
            }
        };
        CapturingSink sink = new();

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExportAsync(
                Request(),
                sink,
                CancellationToken.None);

        Assert.False(exported);
        Assert.Equal(TenantTerminationContributionStatus.Completed, result.Status);
        Assert.Equal(0, result.AffectedCount);
        Assert.Equal(0, result.SelectedProofRevision);
        Assert.Equal(0, result.ResultingProofRevision);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task Revision_zero_scope_exports_all_four_stores()
    {
        List<AccessControlScopeExportStore> calls = [];
        TestLifecycle lifecycle = OpenLifecycle(revision: 0) with
        {
            Export = (request, _) =>
            {
                Assert.Equal(Coordinate, request.Coordinate);
                calls.Add(request.Store);
                AccessControlScopeExportRecord record = RecordFor(request.Store);
                return Task.FromResult(new AccessControlScopeExportPage(
                    AccessControlScopeExportStatus.Completed,
                    ScopeRevision: 0,
                    request.Store,
                    [record],
                    Cursor(RecordId(record)),
                    HasMore: false));
            }
        };
        CapturingSink sink = new();

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExportAsync(
                Request(),
                sink,
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Completed, result.Status);
        Assert.Equal(4, result.AffectedCount);
        Assert.Equal(0, result.SelectedProofRevision);
        Assert.Equal(0, result.ResultingProofRevision);
        Assert.Equal(
            Enum.GetValues<AccessControlScopeExportStore>()
                .Where(store =>
                    store != AccessControlScopeExportStore.Unknown),
            calls);
        Assert.Equal(
            AccessControlTenantTerminationMetadata.RecordTypes,
            sink.Records.Select(record => record.RecordType));
    }

    [Fact]
    public async Task Cross_workspace_record_fails_closed()
    {
        AccessScope other = WorkspaceAccessScopes.Create(Guid.NewGuid().ToString("D"));
        TestLifecycle lifecycle = OpenLifecycle(4) with
        {
            Export = (request, _) => Task.FromResult(
                request.Store == AccessControlScopeExportStore.RoleAssignments
                    ? new AccessControlScopeExportPage(
                        AccessControlScopeExportStatus.Completed,
                        4,
                        request.Store,
                        [new AccessControlRoleAssignmentExportRecord(
                            Guid.NewGuid(),
                            AccessSubjectKind.User,
                            "subject-a",
                            "manager",
                            ["properties.read"],
                            other.Value,
                            Now.AddDays(-1),
                            null,
                            null)],
                        Cursor(Guid.NewGuid()),
                        false)
                    : EmptyPage(request, 4))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExportAsync(
                Request(),
                new CapturingSink(),
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Failed, result.Status);
        Assert.Equal(
            "access-control.termination.export-record-invalid",
            result.ResultCode);
    }

    [Fact]
    public async Task Stale_page_is_retryable_without_terminal_proof()
    {
        TestLifecycle lifecycle = OpenLifecycle(4) with
        {
            Export = (request, _) => Task.FromResult(
                new AccessControlScopeExportPage(
                    AccessControlScopeExportStatus.Stale,
                    5,
                    request.Store,
                    [],
                    null,
                    false))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExportAsync(
                Request(),
                new CapturingSink(),
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Null(result.SelectedProofRevision);
        Assert.Null(result.ResultingProofRevision);
    }

    [Fact]
    public async Task Changed_final_revision_discards_the_owner_result()
    {
        int snapshots = 0;
        TestLifecycle lifecycle = OpenLifecycle(4) with
        {
            Snapshot = (_, _) => Task.FromResult(
                new AccessControlScopeSnapshot(
                    AccessControlScopeStatus.Open,
                    snapshots++ == 0 ? 4 : 5))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExportAsync(
                Request(),
                new CapturingSink(),
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal(
            "access-control.termination.export-revision-changed",
            result.ResultCode);
    }

    [Fact]
    public async Task Mismatched_fence_fails_before_scope_selection()
    {
        bool selected = false;
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) =>
            {
                selected = true;
                throw new InvalidOperationException();
            }
        };
        WorkspaceTerminationFenceSnapshot wrong = Fence() with
        {
            ProcessId = Guid.NewGuid()
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle, wrong).ExportAsync(
                Request(),
                new CapturingSink(),
                CancellationToken.None);

        Assert.False(selected);
        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
    }

    [Fact]
    public async Task Non_canonical_workspace_coordinate_fails_closed()
    {
        TenantTerminationExportRequest request = Request() with
        {
            Contribution = Request().Contribution with
            {
                TenantId = TenantId.ToUpperInvariant()
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(
                    new TestLifecycle(),
                    Fence(),
                    request.Contribution.TenantId)
                .ExportAsync(
                    request,
                    new CapturingSink(),
                    CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Failed, result.Status);
        Assert.Equal(
            "access-control.termination.export-request-invalid",
            result.ResultCode);
    }

    [Fact]
    public async Task Destroy_reports_bounded_durable_progress()
    {
        TenantTerminationContributionRequest request = DestroyRequest();
        TestLifecycle lifecycle = OpenLifecycle(7) with
        {
            Destroy = (destroy, _) =>
            {
                Assert.Equal(request.IdempotencyKey, destroy.OperationId);
                Assert.Equal(Coordinate, destroy.Coordinate);
                Assert.Equal(7, destroy.ExpectedRevision);
                Assert.Equal(
                    AccessControlScopeLifecycleLimits.MaximumDestroyBatchSize,
                    destroy.BatchSize);
                return Task.FromResult(new AccessControlScopeDestroyResult(
                    AccessControlScopeDestroyStatus.InProgress,
                    new AccessControlScopeDestroyProgress(
                        request.IdempotencyKey,
                        ResultingRevision: 8,
                        destroy.BatchSize,
                        AccessControlScopeDestructionStage.ProfileAssignments,
                        RemovedRecordCount: 1000,
                        CompletedBatchCount: 1,
                        RemovalProofVersion: 1,
                        new string('a', 64),
                        Now.AddMinutes(-1),
                        Now),
                    null));
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal(1000, result.AffectedCount);
        Assert.Null(result.SelectedProofRevision);
        Assert.Null(result.ResultingProofRevision);
    }

    [Fact]
    public async Task Destroy_replays_using_the_durable_selected_revision()
    {
        TenantTerminationContributionRequest request = DestroyRequest();
        AccessControlScopeDestroyReceipt receipt = Receipt(
            request.IdempotencyKey,
            resultingRevision: 9,
            removedCount: 0);
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) => Task.FromResult(
                new AccessControlScopeSnapshot(
                    AccessControlScopeStatus.Closed,
                    9)
                {
                    SelectedRevision = 0
                }),
            Destroy = (destroy, _) =>
            {
                Assert.Equal(0, destroy.ExpectedRevision);
                return Task.FromResult(new AccessControlScopeDestroyResult(
                    AccessControlScopeDestroyStatus.Replayed,
                    null,
                    receipt));
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Completed, result.Status);
        Assert.Equal(0, result.SelectedProofRevision);
        Assert.Equal(9, result.ResultingProofRevision);
        Assert.Equal(receipt.CompletedAtUtc, result.RecordedAtUtc);
    }

    [Fact]
    public async Task Destroy_closes_a_missing_scope_at_a_later_global_revision()
    {
        TenantTerminationContributionRequest request = DestroyRequest();
        TestLifecycle lifecycle = new()
        {
            Destroy = (destroy, _) =>
            {
                Assert.Equal(0, destroy.ExpectedRevision);
                return Task.FromResult(new AccessControlScopeDestroyResult(
                    AccessControlScopeDestroyStatus.Completed,
                    null,
                    Receipt(request.IdempotencyKey, 12, 0)));
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Completed, result.Status);
        Assert.Equal(0, result.SelectedProofRevision);
        Assert.Equal(12, result.ResultingProofRevision);
    }

    [Fact]
    public async Task Busy_destruction_is_retryable_without_terminal_proof()
    {
        TestLifecycle lifecycle = OpenLifecycle(3) with
        {
            Destroy = (_, _) => Task.FromResult(
                new AccessControlScopeDestroyResult(
                    AccessControlScopeDestroyStatus.Busy,
                    null,
                    null))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                DestroyRequest(),
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Null(result.SelectedProofRevision);
        Assert.Null(result.ResultingProofRevision);
    }

    [Fact]
    public async Task Malformed_completion_receipt_fails_closed()
    {
        TestLifecycle lifecycle = OpenLifecycle(3) with
        {
            Destroy = (_, _) => Task.FromResult(
                new AccessControlScopeDestroyResult(
                    AccessControlScopeDestroyStatus.Completed,
                    null,
                    Receipt(Guid.NewGuid(), 4, 1)))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                DestroyRequest(),
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Failed, result.Status);
        Assert.Equal(
            "access-control.termination.destroy-response-invalid",
            result.ResultCode);
    }

    private static AccessControlTenantTerminationContributor CreateContributor(
        IAccessControlScopeLifecycle lifecycle,
        WorkspaceTerminationFenceSnapshot? fence = null,
        string? scopeId = null) =>
        new(
            lifecycle,
            new TestScopeContext(scopeId ?? TenantId),
            new FixedClock(),
            new TestFenceReader(fence ?? Fence()));

    private static TestLifecycle OpenLifecycle(long revision) => new()
    {
        Snapshot = (_, _) => Task.FromResult(
            new AccessControlScopeSnapshot(
                AccessControlScopeStatus.Open,
                revision)),
        Export = (request, _) => Task.FromResult(EmptyPage(request, revision))
    };

    private static AccessControlScopeExportPage EmptyPage(
        AccessControlScopeExportRequest request,
        long revision) =>
        new(
            AccessControlScopeExportStatus.Completed,
            revision,
            request.Store,
            [],
            request.AfterCursor,
            HasMore: false);

    private static TenantTerminationExportRequest Request() =>
        new(
            new TenantTerminationContributionRequest(
                TenantTerminationContract.CurrentVersion,
                TenantId,
                ProcessId,
                Guid.Parse("0500738e-56a5-4d59-b64e-ed3452b225da"),
                ApprovalRevision: 2,
                OperationRevision: 4,
                TerminationEpoch,
                TenantTerminationContributionPhase.Export,
                Guid.Parse("e9ab9a84-973e-4ea9-b9e1-b3e256074769"),
                Guid.Parse("d07d7678-42c2-4d98-920d-4f2f91f67173"),
                new string('b', 64),
                "system:tenant-termination",
                Now.AddHours(1)),
            FreezeOperationRevision: 3,
            WorkspaceFenceRevision: 5,
            new string('c', 64),
            Now.AddMinutes(-1));

    private static TenantTerminationContributionRequest DestroyRequest() =>
        Request().Contribution with
        {
            Phase = TenantTerminationContributionPhase.Destroy
        };

    private static WorkspaceTerminationFenceSnapshot Fence() =>
        new(
            ProcessId,
            TerminationEpoch,
            WorkspaceTerminationFenceState.Frozen,
            Version: 5);

    private static AccessControlScopeDestroyReceipt Receipt(
        Guid operationId,
        long resultingRevision,
        long removedCount) =>
        new(
            operationId,
            resultingRevision,
            AccessControlScopeLifecycleLimits.MaximumDestroyBatchSize,
            removedCount,
            CompletedBatchCount: removedCount == 0 ? 0 : 3,
            RemovalProofVersion: 1,
            new string('a', 64),
            Now.AddMinutes(-2),
            Now.AddMinutes(-1));

    private static AccessControlScopeExportRecord RecordFor(
        AccessControlScopeExportStore store) => store switch
        {
            AccessControlScopeExportStore.RoleAssignments =>
                new AccessControlRoleAssignmentExportRecord(
                    Guid.Parse("8836ca4d-ae9c-4681-a64f-cbb2daa64f30"),
                    AccessSubjectKind.User,
                    "subject-member",
                    "workspace-manager",
                    ["properties.read", "reservations.manage"],
                    RootScope.Value,
                    Now.AddDays(-9),
                    null,
                    null),
            AccessControlScopeExportStore.Profiles =>
                new AccessControlProfileExportRecord(
                    Guid.Parse("5ecf67b6-0087-40f1-a34e-25888fc69a86"),
                    RootScope.Value,
                    "front-desk",
                    "Front desk",
                    "Front desk operations",
                    AccessProfileStatus.Active,
                    Version: 3,
                    AccessSubjectKind.User,
                    "subject-owner",
                    Now.AddDays(-8),
                    AccessSubjectKind.User,
                    "subject-owner",
                    Now.AddDays(-1),
                    ["properties.read", "reservations.manage"]),
            AccessControlScopeExportStore.ProfileAssignments =>
                new AccessControlProfileAssignmentExportRecord(
                    Guid.Parse("f1b75506-6871-4d1f-b489-25439772873e"),
                    Guid.Parse("5ecf67b6-0087-40f1-a34e-25888fc69a86"),
                    RootScope.Value,
                    "front-desk",
                    RootScope.Value,
                    AccessSubjectKind.User,
                    "subject-member",
                    AccessSubjectKind.User,
                    "subject-owner",
                    Now.AddDays(-7)),
            AccessControlScopeExportStore.ProfileChanges =>
                new AccessControlProfileChangeExportRecord(
                    Guid.Parse("96ea9e10-4762-440c-b4fa-785a3c25656b"),
                    Guid.Parse("5ecf67b6-0087-40f1-a34e-25888fc69a86"),
                    RootScope.Value,
                    "front-desk",
                    AccessProfileChangeKind.Assigned,
                    AccessSubjectKind.User,
                    "subject-owner",
                    AccessSubjectKind.User,
                    "subject-member",
                    RootScope.Value,
                    ProfileVersion: 3,
                    Now.AddDays(-7)),
            _ => throw new ArgumentOutOfRangeException(nameof(store), store, null)
        };

    private static Guid RecordId(AccessControlScopeExportRecord record) =>
        record switch
        {
            AccessControlRoleAssignmentExportRecord value =>
                value.AssignmentId,
            AccessControlProfileExportRecord value => value.ProfileId,
            AccessControlProfileAssignmentExportRecord value =>
                value.AssignmentId,
            AccessControlProfileChangeExportRecord value => value.ChangeId,
            _ => throw new ArgumentOutOfRangeException(nameof(record))
        };

    private static string Cursor(Guid id) => $"id:{id:D}";

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestFenceReader(
        WorkspaceTerminationFenceSnapshot snapshot)
        : IWorkspaceTerminationFenceReader
    {
        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkspaceTerminationFenceSnapshot?>(snapshot);
    }

    private sealed class CapturingSink : IDataRightsExportSink
    {
        public List<DataRightsExportRecord> Records { get; } = [];

        public ValueTask WriteAsync(
            DataRightsExportRecord record,
            CancellationToken cancellationToken)
        {
            this.Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed record TestLifecycle : IAccessControlScopeLifecycle
    {
        public Func<
            AccessControlScopeCoordinate,
            CancellationToken,
            Task<AccessControlScopeSnapshot>> Snapshot
        { get; init; } = (_, _) => Task.FromResult(
            new AccessControlScopeSnapshot(
                AccessControlScopeStatus.Missing,
                Revision: 0));

        public Func<
            AccessControlScopeExportRequest,
            CancellationToken,
            Task<AccessControlScopeExportPage>> Export
        { get; init; } =
            (_, _) => throw new InvalidOperationException();

        public Func<
            AccessControlScopeDestroyRequest,
            CancellationToken,
            Task<AccessControlScopeDestroyResult>> Destroy
        { get; init; } =
            (_, _) => throw new InvalidOperationException();

        public Task<AccessControlScopeSnapshot> GetSnapshotAsync(
            AccessControlScopeCoordinate coordinate,
            CancellationToken cancellationToken) =>
            this.Snapshot(coordinate, cancellationToken);

        public Task<AccessControlScopeExportPage> ExportAsync(
            AccessControlScopeExportRequest request,
            CancellationToken cancellationToken) =>
            this.Export(request, cancellationToken);

        public Task<AccessControlScopeDestroyResult> DestroyBatchAsync(
            AccessControlScopeDestroyRequest request,
            CancellationToken cancellationToken) =>
            this.Destroy(request, cancellationToken);
    }
}

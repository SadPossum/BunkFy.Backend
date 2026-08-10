namespace BunkFy.Extensions.DataRights.Organizations.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsTenantTerminationContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 4, 30, 0, TimeSpan.Zero);
    private static readonly Guid OrganizationId =
        Guid.Parse("08b74d32-84da-4b44-80cc-b74dc8d5bacc");
    private static readonly string TenantId = OrganizationId.ToString("D");
    private static readonly Guid ProcessId =
        Guid.Parse("74875bfc-05bb-474a-a182-12552252108a");
    private static readonly Guid TerminationEpoch =
        Guid.Parse("c8d15486-3678-47ee-9365-c74a27f3bea9");

    [Fact]
    public void Descriptor_is_the_terminal_bunkfy_organizations_owner()
    {
        OrganizationsTenantTerminationContributor contributor =
            CreateContributor(new TestLifecycle());

        Assert.Equal(
            OrganizationsTenantTerminationMetadata.OwnerKey,
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
                OrganizationsTenantTerminationMetadata.DependencyOwnerKeys,
                plan.DependsOnOwnerKeys));
        Assert.True(contributor.Descriptor.MandatoryForProduction);
        Assert.Equal(
            OrganizationsTenantTerminationMetadata.ExportFieldIds
                .Order(StringComparer.Ordinal),
            contributor.ExportDescriptor.FieldIds
                .Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            contributor.ExportDescriptor.FieldIds,
            field => field.Contains("digest", StringComparison.Ordinal));
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
    public async Task Legacy_revision_zero_scope_exports_all_five_stores()
    {
        List<OrganizationScopeExportStore> calls = [];
        TestLifecycle lifecycle = OpenLifecycle(revision: 0) with
        {
            Export = (request, _) =>
            {
                calls.Add(request.Store);
                OrganizationScopeExportRecord record = RecordFor(request.Store);
                return Task.FromResult(new OrganizationScopeExportPage(
                    OrganizationScopeExportStatus.Completed,
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
        Assert.Equal(5, result.AffectedCount);
        Assert.Equal(0, result.SelectedProofRevision);
        Assert.Equal(0, result.ResultingProofRevision);
        Assert.Equal(
            Enum.GetValues<OrganizationScopeExportStore>()
                .Where(store => store != OrganizationScopeExportStore.Unknown),
            calls);
        Assert.Equal(
            OrganizationsTenantTerminationMetadata.RecordTypes,
            sink.Records.Select(record => record.RecordType));
    }

    [Fact]
    public async Task Cross_organization_record_fails_closed()
    {
        TestLifecycle lifecycle = OpenLifecycle(4) with
        {
            Export = (request, _) => Task.FromResult(
                request.Store == OrganizationScopeExportStore.Organization
                    ? new OrganizationScopeExportPage(
                        OrganizationScopeExportStatus.Completed,
                        4,
                        request.Store,
                        [new OrganizationScopeOrganizationExportRecord(
                            Guid.NewGuid(),
                            "Wrong",
                            "wrong",
                            OrganizationStatus.Active,
                            1,
                            1,
                            "actor",
                            Now.AddDays(-1),
                            "actor",
                            Now)],
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
            "organizations.termination.export-record-invalid",
            result.ResultCode);
    }

    [Fact]
    public async Task Stale_page_is_retryable_without_terminal_proof()
    {
        TestLifecycle lifecycle = OpenLifecycle(4) with
        {
            Export = (request, _) => Task.FromResult(
                new OrganizationScopeExportPage(
                    OrganizationScopeExportStatus.Stale,
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
                new OrganizationScopeSnapshot(
                    OrganizationScopeStatus.Open,
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
            "organizations.termination.export-revision-changed",
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
            "organizations.termination.export-request-invalid",
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
                Assert.Equal(OrganizationId, destroy.OrganizationId);
                Assert.Equal(7, destroy.ExpectedRevision);
                Assert.Equal(
                    OrganizationScopeLifecycleLimits.MaximumDestroyBatchSize,
                    destroy.BatchSize);
                return Task.FromResult(new OrganizationScopeDestroyResult(
                    OrganizationScopeDestroyStatus.InProgress,
                    new OrganizationScopeDestroyProgress(
                        request.IdempotencyKey,
                        ResultingRevision: 8,
                        destroy.BatchSize,
                        OrganizationScopeDestructionStage.Memberships,
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
    public async Task Destroy_replays_from_the_closed_scope_revision()
    {
        TenantTerminationContributionRequest request = DestroyRequest();
        OrganizationScopeDestroyReceipt receipt = Receipt(
            request.IdempotencyKey,
            resultingRevision: 8,
            removedCount: 2500);
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) => Task.FromResult(
                new OrganizationScopeSnapshot(
                    OrganizationScopeStatus.Closed,
                    8)),
            Destroy = (destroy, _) =>
            {
                Assert.Equal(7, destroy.ExpectedRevision);
                return Task.FromResult(new OrganizationScopeDestroyResult(
                    OrganizationScopeDestroyStatus.Replayed,
                    null,
                    receipt));
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Completed, result.Status);
        Assert.Equal(2500, result.AffectedCount);
        Assert.Equal(7, result.SelectedProofRevision);
        Assert.Equal(8, result.ResultingProofRevision);
        Assert.Equal(receipt.CompletedAtUtc, result.RecordedAtUtc);
    }

    [Fact]
    public async Task Destroy_closes_a_missing_scope_with_exact_zero_selection()
    {
        TenantTerminationContributionRequest request = DestroyRequest();
        TestLifecycle lifecycle = new()
        {
            Destroy = (destroy, _) =>
            {
                Assert.Equal(0, destroy.ExpectedRevision);
                return Task.FromResult(new OrganizationScopeDestroyResult(
                    OrganizationScopeDestroyStatus.Completed,
                    null,
                    Receipt(request.IdempotencyKey, 1, 0)));
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Completed, result.Status);
        Assert.Equal(0, result.SelectedProofRevision);
        Assert.Equal(1, result.ResultingProofRevision);
    }

    [Fact]
    public async Task Busy_destruction_is_retryable_without_terminal_proof()
    {
        TestLifecycle lifecycle = OpenLifecycle(3) with
        {
            Destroy = (_, _) => Task.FromResult(
                new OrganizationScopeDestroyResult(
                    OrganizationScopeDestroyStatus.Busy,
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
                new OrganizationScopeDestroyResult(
                    OrganizationScopeDestroyStatus.Completed,
                    null,
                    Receipt(Guid.NewGuid(), 4, 1)))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                DestroyRequest(),
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Failed, result.Status);
        Assert.Equal(
            "organizations.termination.destroy-response-invalid",
            result.ResultCode);
    }

    private static OrganizationsTenantTerminationContributor CreateContributor(
        IOrganizationScopeLifecycle lifecycle,
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
            new OrganizationScopeSnapshot(
                OrganizationScopeStatus.Open,
                revision)),
        Export = (request, _) => Task.FromResult(EmptyPage(request, revision))
    };

    private static OrganizationScopeExportPage EmptyPage(
        OrganizationScopeExportRequest request,
        long revision) =>
        new(
            OrganizationScopeExportStatus.Completed,
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

    private static OrganizationScopeDestroyReceipt Receipt(
        Guid operationId,
        long resultingRevision,
        long removedCount) =>
        new(
            operationId,
            resultingRevision,
            OrganizationScopeLifecycleLimits.MaximumDestroyBatchSize,
            removedCount,
            CompletedBatchCount: removedCount == 0 ? 0 : 3,
            RemovalProofVersion: 1,
            new string('a', 64),
            Now.AddMinutes(-2),
            Now.AddMinutes(-1));

    private static OrganizationScopeExportRecord RecordFor(
        OrganizationScopeExportStore store) => store switch
        {
            OrganizationScopeExportStore.Organization =>
                new OrganizationScopeOrganizationExportRecord(
                    OrganizationId,
                    "Like Hostel",
                    "like-hostel",
                    OrganizationStatus.Active,
                    ActiveOwnerCount: 1,
                    Version: 3,
                    "subject-owner",
                    Now.AddDays(-10),
                    "subject-owner",
                    Now.AddDays(-1)),
            OrganizationScopeExportStore.Memberships =>
                new OrganizationScopeMembershipExportRecord(
                    Guid.Parse("8836ca4d-ae9c-4681-a64f-cbb2daa64f30"),
                    OrganizationId,
                    "subject-member",
                    OrganizationMembershipRole.Member,
                    OrganizationMembershipStatus.Active,
                    Version: 2,
                    "subject-owner",
                    Now.AddDays(-9),
                    "subject-owner",
                    Now.AddDays(-2)),
            OrganizationScopeExportStore.Invitations =>
                new OrganizationScopeInvitationExportRecord(
                    Guid.Parse("5ecf67b6-0087-40f1-a34e-25888fc69a86"),
                    OrganizationId,
                    "subject-owner",
                    "member@example.test",
                    TokenVersion: 1,
                    Now.AddDays(1),
                    OrganizationInvitationStatus.Pending,
                    AcceptedSubjectId: null,
                    AcceptedMembershipId: null,
                    AcceptedAtUtc: null,
                    Version: 1,
                    "subject-owner",
                    Now.AddDays(-1),
                    "subject-owner",
                    Now.AddDays(-1)),
            OrganizationScopeExportStore.EnrollmentLinks =>
                new OrganizationScopeEnrollmentLinkExportRecord(
                    Guid.Parse("f1b75506-6871-4d1f-b489-25439772873e"),
                    OrganizationId,
                    "subject-owner",
                    TokenVersion: 2,
                    Now.AddDays(2),
                    MaximumClaims: 10,
                    ReservedClaims: 1,
                    OrganizationEnrollmentApprovalMode.RequiresApproval,
                    OrganizationEnrollmentLinkStatus.Active,
                    Version: 4,
                    "subject-owner",
                    Now.AddDays(-4),
                    "subject-owner",
                    Now.AddDays(-1)),
            OrganizationScopeExportStore.EnrollmentClaims =>
                new OrganizationScopeEnrollmentClaimExportRecord(
                    Guid.Parse("96ea9e10-4762-440c-b4fa-785a3c25656b"),
                    OrganizationId,
                    Guid.Parse("f1b75506-6871-4d1f-b489-25439772873e"),
                    "subject-applicant",
                    OrganizationEnrollmentClaimStatus.Pending,
                    MembershipId: null,
                    DecisionExpiresAtUtc: Now.AddDays(1),
                    Version: 1,
                    Now.AddHours(-1),
                    "subject-applicant",
                    Now.AddHours(-1)),
            _ => throw new ArgumentOutOfRangeException(nameof(store), store, null)
        };

    private static Guid RecordId(OrganizationScopeExportRecord record) =>
        record switch
        {
            OrganizationScopeOrganizationExportRecord value =>
                value.OrganizationId,
            OrganizationScopeMembershipExportRecord value =>
                value.MembershipId,
            OrganizationScopeInvitationExportRecord value =>
                value.InvitationId,
            OrganizationScopeEnrollmentLinkExportRecord value =>
                value.EnrollmentLinkId,
            OrganizationScopeEnrollmentClaimExportRecord value =>
                value.EnrollmentClaimId,
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

    private sealed record TestLifecycle : IOrganizationScopeLifecycle
    {
        public Func<Guid, CancellationToken, Task<OrganizationScopeSnapshot>>
            Snapshot
        { get; init; } = (_, _) => Task.FromResult(
                new OrganizationScopeSnapshot(
                    OrganizationScopeStatus.Missing,
                    Revision: 0));

        public Func<
            OrganizationScopeExportRequest,
            CancellationToken,
            Task<OrganizationScopeExportPage>> Export
        { get; init; } =
            (_, _) => throw new InvalidOperationException();

        public Func<
            OrganizationScopeDestroyRequest,
            CancellationToken,
            Task<OrganizationScopeDestroyResult>> Destroy
        { get; init; } =
            (_, _) => throw new InvalidOperationException();

        public Task<OrganizationScopeSnapshot> GetSnapshotAsync(
            Guid organizationId,
            CancellationToken cancellationToken) =>
            this.Snapshot(organizationId, cancellationToken);

        public Task<OrganizationScopeExportPage> ExportAsync(
            OrganizationScopeExportRequest request,
            CancellationToken cancellationToken) =>
            this.Export(request, cancellationToken);

        public Task<OrganizationScopeDestroyResult> DestroyBatchAsync(
            OrganizationScopeDestroyRequest request,
            CancellationToken cancellationToken) =>
            this.Destroy(request, cancellationToken);
    }
}

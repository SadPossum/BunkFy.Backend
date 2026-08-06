namespace BunkFy.Modules.Properties.Tests;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using BunkFy.Modules.Properties.Persistence;
using BunkFy.Modules.Properties.Persistence.Repositories;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DomainPropertyGovernanceAcknowledgement =
    BunkFy.Modules.Properties.Domain.ValueObjects.PropertyGovernanceAcknowledgement;

[Trait("Category", "Unit")]
public sealed partial class PropertiesTenantTerminationExportContributorTests
{
    private const string TenantId =
        "10000000-0000-0000-0000-000000000001";
    private const string Digest =
        "0123456789abcdef0123456789abcdef" +
        "0123456789abcdef0123456789abcdef";
    private static readonly Guid ProcessId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TerminationEpoch =
        Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid PropertyId =
        Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid RoomId =
        Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid BedId =
        Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset FrozenAtUtc =
        new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now =
        FrozenAtUtc.AddMinutes(1);

    [Fact]
    public void Descriptor_declares_phase_specific_dependencies()
    {
        MutableFenceReader fences = new();
        using PropertiesDbContext context = CreateContext(fences);
        PropertiesTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences);

        Assert.Collection(
            contributor.Descriptor.PhasePlans,
            export =>
            {
                Assert.Equal(
                    TenantTerminationContributionPhase.Export,
                    export.Phase);
                Assert.Equal(
                    [PropertiesTenantTerminationMetadata.ExportDependencyOwnerKey],
                    export.DependsOnOwnerKeys);
            },
            destroy =>
            {
                Assert.Equal(
                    TenantTerminationContributionPhase.Destroy,
                    destroy.Phase);
                Assert.Equal(
                    PropertiesTenantTerminationMetadata
                        .DestroyDependencyOwnerKeys,
                    destroy.DependsOnOwnerKeys);
            });
    }

    [Fact]
    public async Task Export_streams_complete_topology_and_governance_in_stable_order()
    {
        MutableFenceReader fences = new();
        await using PropertiesDbContext context = CreateContext(fences);
        SeedGraph(context);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        fences.Current = FrozenFence();
        PropertiesTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences);
        CollectingSink first = new();

        TenantTerminationContributionResult result =
            await contributor.ExportAsync(
                Request(),
                first,
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.Completed,
            result.Status);
        Assert.Equal("properties.termination.exported", result.ResultCode);
        Assert.Equal(5, result.AffectedCount);
        Assert.Equal(1, result.SelectedProofRevision);
        Assert.Equal(1, result.ResultingProofRevision);
        Assert.Equal(
            [
                PropertiesTenantTerminationMetadata.PropertyRecordType,
                PropertiesTenantTerminationMetadata
                    .GovernanceAcknowledgementRecordType,
                PropertiesTenantTerminationMetadata.RoomRecordType,
                PropertiesTenantTerminationMetadata.BedRecordType,
                PropertiesTenantTerminationMetadata
                    .GovernanceRevisionRecordType
            ],
            first.Records.Select(record => record.RecordType).ToArray());
        Assert.Equal(
            "user:owner",
            Field(
                    first.Records[^1],
                    "properties.staff-actor-reference")
                .GetString());
        Assert.Equal(
            PropertiesTenantTerminationMetadata.ExportSchemaId,
            contributor.ExportDescriptor.ExportSchemaId);
        Assert.Equal(
            PropertiesTenantTerminationMetadata.ExportFieldIds
                .OrderBy(field => field, StringComparer.Ordinal),
            contributor.ExportDescriptor.FieldIds);

        CollectingSink replay = new();
        TenantTerminationContributionResult replayResult =
            await contributor.ExportAsync(
                Request(),
                replay,
                CancellationToken.None);

        Assert.Equal(result.AffectedCount, replayResult.AffectedCount);
        Assert.Equal(
            first.Records.Select(Identity).ToArray(),
            replay.Records.Select(Identity).ToArray());
    }

    [Fact]
    public async Task Export_retries_without_records_for_a_different_fence()
    {
        MutableFenceReader fences = new()
        {
            Current = FrozenFence() with { Version = 2 }
        };
        await using PropertiesDbContext context = CreateContext(fences);
        PropertiesTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences);
        CollectingSink sink = new();

        TenantTerminationContributionResult result =
            await contributor.ExportAsync(
                Request(),
                sink,
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal(
            "properties.termination.export-fence-unavailable",
            result.ResultCode);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task Operational_save_rejects_a_frozen_workspace_without_advancing_revision()
    {
        MutableFenceReader fences = new();
        await using PropertiesDbContext context = CreateContext(fences);
        context.Properties.Add(CreateProperty(PropertyId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        fences.Current = FrozenFence();
        context.Properties.Add(CreateProperty(Guid.NewGuid(), "SECOND"));

        PropertiesOperationalAdmissionException failure =
            await Assert.ThrowsAsync<
                PropertiesOperationalAdmissionException>(
                () => context.SaveChangesAsync());

        Assert.Equal(
            PropertiesOperationalAdmissionFailure.Restricted,
            failure.Failure);
        context.ChangeTracker.Clear();
        Assert.Equal(
            1,
            await context.TenantRevisions
                .Select(revision => revision.Revision)
                .SingleAsync());
        Assert.Single(await context.Properties.ToListAsync());
    }

    [Fact]
    public async Task Operational_saves_advance_the_tenant_revision_once_each()
    {
        MutableFenceReader fences = new();
        await using PropertiesDbContext context = CreateContext(fences);
        Property property = CreateProperty(PropertyId);
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        Assert.True(property.Update(
            "Updated hostel",
            property.Code.Value,
            property.TimeZoneId.Value,
            property.Version,
            Guid.NewGuid(),
            Now).IsSuccess);
        await context.SaveChangesAsync();

        Assert.Equal(
            2,
            await context.TenantRevisions
                .Select(revision => revision.Revision)
                .SingleAsync());
    }

    [Fact]
    public async Task Operational_save_fails_closed_when_fence_read_fails()
    {
        await using PropertiesDbContext context = CreateContext(
            new ThrowingFenceReader());
        context.Properties.Add(CreateProperty(PropertyId));

        PropertiesOperationalAdmissionException failure =
            await Assert.ThrowsAsync<
                PropertiesOperationalAdmissionException>(
                () => context.SaveChangesAsync());

        Assert.Equal(
            PropertiesOperationalAdmissionFailure.Unavailable,
            failure.Failure);
        context.ChangeTracker.Clear();
        Assert.Empty(await context.Properties.ToListAsync());
        Assert.Empty(await context.TenantRevisions.ToListAsync());
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task Governance_revisions_are_append_only(
        EntityState attemptedState)
    {
        MutableFenceReader fences = new();
        await using PropertiesDbContext context = CreateContext(fences);
        SeedGraph(context);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        PropertyGovernanceRevision revision =
            await context.GovernanceRevisions.SingleAsync();
        context.Entry(revision).State = attemptedState;

        InvalidOperationException failure = await Assert.ThrowsAsync<
            InvalidOperationException>(() => context.SaveChangesAsync());

        Assert.Equal(
            "Property governance revisions are append-only.",
            failure.Message);
    }

    private static void SeedGraph(PropertiesDbContext context)
    {
        Property property = CreateProperty(PropertyId);
        PropertyGovernanceBinding binding =
            PropertyGovernanceBinding.Create(
                "GB",
                "uk-hostel-policy",
                policyVersion: 3,
                "eu-west",
                "standard-transfer",
                "hostel-retention",
                retentionPolicyVersion: 2,
                Digest,
                FrozenAtUtc.AddDays(-10),
                FrozenAtUtc.AddDays(10),
                FrozenAtUtc.AddDays(-2)).Value;
        DomainPropertyGovernanceAcknowledgement acknowledgement =
            DomainPropertyGovernanceAcknowledgement.Create(
                "controller-terms",
                acknowledgementVersion: 2).Value;
        Assert.True(property.ActivateProcessing(
            binding,
            [acknowledgement],
            property.Version,
            Guid.NewGuid(),
            FrozenAtUtc.AddDays(-2),
            "user:owner").IsSuccess);

        Room room = Room.Create(
            RoomId,
            TenantId,
            PropertyId,
            "4A",
            "Main House",
            "4",
            Guid.NewGuid(),
            FrozenAtUtc.AddDays(-5)).Value;
        Assert.True(room.AddBed(
            BedId,
            "1",
            room.Version,
            Guid.NewGuid(),
            FrozenAtUtc.AddDays(-5)).IsSuccess);

        PropertyGovernanceRevisionCoordinates current = new(
            binding.OperatingCountryCode,
            binding.PolicyId,
            binding.PolicyVersion,
            binding.DataRegionId,
            binding.TransferProfileId,
            binding.RetentionPolicyId,
            binding.RetentionPolicyVersion,
            binding.ContentSha256,
            Digest);
        PropertyGovernanceRevision revision = new(new(
            Guid.Parse("80000000-0000-0000-0000-000000000001"),
            TenantId,
            PropertyId,
            property.Version,
            PropertyGovernanceRevisionAction.Activated,
            "policy-allowed",
            Previous: null,
            Current: current,
            "user:owner",
            FrozenAtUtc.AddDays(-2)));

        context.Properties.Add(property);
        context.Rooms.Add(room);
        context.PropertyOperationLocks.Add(
            new PropertyOperationLock(property.Id, property.ScopeId));
        context.RoomOperationLocks.Add(
            new RoomOperationLock(room.Id, room.ScopeId));
        context.GovernanceRevisions.Add(revision);
    }

    private static Property CreateProperty(
        Guid propertyId,
        string code = "LIKE") =>
        Property.Create(
            propertyId,
            TenantId,
            $"Hostel {code}",
            code,
            "Europe/London",
            Guid.NewGuid(),
            FrozenAtUtc.AddDays(-5)).Value;

    private static WorkspaceTerminationFenceSnapshot FrozenFence() =>
        new(
            ProcessId,
            TerminationEpoch,
            WorkspaceTerminationFenceState.Frozen,
            Version: 3);

    private static TenantTerminationExportRequest Request() =>
        new(
            new TenantTerminationContributionRequest(
                TenantTerminationContract.CurrentVersion,
                TenantId,
                ProcessId,
                CaseId,
                ApprovalRevision: 1,
                OperationRevision: 2,
                TerminationEpoch,
                TenantTerminationContributionPhase.Export,
                Guid.Parse("90000000-0000-0000-0000-000000000001"),
                Guid.Parse("a0000000-0000-0000-0000-000000000001"),
                Digest,
                "termination-exporter",
                Now.AddMinutes(5)),
            FreezeOperationRevision: 1,
            WorkspaceFenceRevision: 3,
            Digest,
            FrozenAtUtc);

    private static JsonElement Field(
        DataRightsExportRecord record,
        string fieldId) =>
        Assert.Single(
            record.Fields,
            field => field.FieldId == fieldId).Value;

    private static string Identity(DataRightsExportRecord record) =>
        $"{record.RecordType}|{record.RecordId:N}|{record.RecordVersion}";

    private static PropertiesDbContext CreateContext(
        IWorkspaceTerminationFenceReader fences)
    {
        DbContextOptions<PropertiesDbContext> options =
            new DbContextOptionsBuilder<PropertiesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new(options, new TestScopeContext(), fences);
    }

    private sealed class CollectingSink : IDataRightsExportSink
    {
        public List<DataRightsExportRecord> Records { get; } = [];

        public ValueTask WriteAsync(
            DataRightsExportRecord record,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MutableFenceReader
        : IWorkspaceTerminationFenceReader
    {
        public WorkspaceTerminationFenceSnapshot? Current { get; set; }

        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(this.Current);
        }
    }

    private sealed class ThrowingFenceReader
        : IWorkspaceTerminationFenceReader
    {
        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Fence store unavailable.");
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}

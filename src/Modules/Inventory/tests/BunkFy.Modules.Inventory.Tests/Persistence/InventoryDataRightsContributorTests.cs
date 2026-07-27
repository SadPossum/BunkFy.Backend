namespace BunkFy.Modules.Inventory.Tests;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Persistence;
using BunkFy.Modules.Inventory.Persistence.Repositories;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryDataRightsContributorTests
{
    private const string ScopeId = "tenant-a";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Discovery_requires_an_exact_reservation_and_revalidates_version()
    {
        TestScopeContext scope = new(ScopeId);
        await using InventoryDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = SeedKnownProperty(dbContext);
        Guid reservationId = Guid.NewGuid();
        InventoryAllocation allocation = CreateAllocation(
            propertyId,
            reservationId);
        dbContext.Allocations.Add(allocation);
        await dbContext.SaveChangesAsync();

        InventoryDataRightsDiscoveryContributor contributor =
            new(dbContext, scope);
        DataRightsSubjectDiscoveryResult discovered =
            await contributor.DiscoverAsync(
                new(
                    ScopeId,
                    propertyId,
                    new(
                        reservationId,
                        Email: null,
                        Phone: null,
                        Name: null,
                        DateOfBirth: null),
                    DataRightsSubjectDiscoveryLimits.MaxCandidates),
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectDiscoveryStatus.Succeeded,
            discovered.Status);
        DataRightsSubjectCandidate candidate =
            Assert.Single(discovered.Candidates);
        Assert.Equal(
            InventoryDataRightsCoordinates.Owner,
            candidate.Coordinate.OwnerKey);
        Assert.Equal(
            InventoryDataRightsCoordinates.AllocationRecordType,
            candidate.Coordinate.RecordType);
        Assert.Equal(allocation.Id, candidate.Coordinate.RecordId);
        Assert.Equal(allocation.Version, candidate.Coordinate.RecordVersion);
        Assert.Null(candidate.EmailHint);
        Assert.Null(candidate.PhoneHint);
        Assert.DoesNotContain(
            reservationId.ToString(),
            candidate.DisplayName,
            StringComparison.OrdinalIgnoreCase);

        DataRightsSubjectDiscoveryResult weak =
            await contributor.DiscoverAsync(
                new(
                    ScopeId,
                    propertyId,
                    new(
                        RecordId: null,
                        Email: "guest@example.test",
                        Phone: null,
                        Name: null,
                        DateOfBirth: null),
                    DataRightsSubjectDiscoveryLimits.MaxCandidates),
                CancellationToken.None);
        Assert.Equal(
            DataRightsSubjectDiscoveryStatus.ScopeUnavailable,
            weak.Status);

        DataRightsSubjectSelectionValidation valid =
            await contributor.ValidateSelectionAsync(
                new(
                    ScopeId,
                    propertyId,
                    candidate.Coordinate),
                CancellationToken.None);
        DataRightsSubjectSelectionValidation stale =
            await contributor.ValidateSelectionAsync(
                new(
                    ScopeId,
                    propertyId,
                    candidate.Coordinate with
                    {
                        RecordVersion =
                            candidate.Coordinate.RecordVersion + 1
                    }),
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectSelectionValidationStatus.Valid,
            valid.Status);
        Assert.Equal(
            DataRightsSubjectSelectionValidationStatus.Stale,
            stale.Status);

        Assert.True(allocation.Anonymise(
            allocation.Version,
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        await dbContext.SaveChangesAsync();
        DataRightsSubjectDiscoveryResult hidden =
            await contributor.DiscoverAsync(
                new(
                    ScopeId,
                    propertyId,
                    new(
                        reservationId,
                        Email: null,
                        Phone: null,
                        Name: null,
                        DateOfBirth: null),
                    DataRightsSubjectDiscoveryLimits.MaxCandidates),
                CancellationToken.None);
        DataRightsSubjectSelectionValidation noLongerSelectable =
            await contributor.ValidateSelectionAsync(
                new(
                    ScopeId,
                    propertyId,
                    candidate.Coordinate),
                CancellationToken.None);

        Assert.Empty(hidden.Candidates);
        Assert.Equal(
            DataRightsSubjectSelectionValidationStatus.NotFound,
            noLongerSelectable.Status);
    }

    [Fact]
    public async Task Export_writes_the_selected_allocation_and_ordered_decisions()
    {
        TestScopeContext scope = new(ScopeId);
        await using InventoryDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = SeedKnownProperty(dbContext);
        Guid reservationId = Guid.NewGuid();
        InventoryAllocation allocation = CreateAllocation(
            propertyId,
            reservationId);
        dbContext.Allocations.Add(allocation);
        dbContext.AllocationAmendmentDecisions.AddRange(
            CreateDecision(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                allocation,
                reservationId,
                Now.AddMinutes(2)),
            CreateDecision(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                allocation,
                reservationId,
                Now.AddMinutes(1)));
        await dbContext.SaveChangesAsync();

        CollectingSink sink = new();
        InventoryDataRightsExportContributor contributor =
            new(dbContext, scope);
        DataRightsSubjectExportResult result =
            await contributor.ExportAsync(
                new(
                    ScopeId,
                    propertyId,
                    new(
                        InventoryDataRightsCoordinates.Owner,
                        InventoryDataRightsCoordinates.AllocationRecordType,
                        allocation.Id,
                        allocation.Version)),
                sink,
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectExportStatus.Succeeded,
            result.Status);
        Assert.Equal(3, result.RecordCount);
        Assert.Equal(result.RecordCount, sink.Records.Count);
        Assert.Equal(
            [
                InventoryDataRightsCoordinates.AllocationRecordType,
                InventoryDataRightsExportContributor
                    .AmendmentDecisionRecordType,
                InventoryDataRightsExportContributor
                    .AmendmentDecisionRecordType
            ],
            sink.Records.Select(record => record.RecordType));
        Assert.Equal(
            reservationId,
            Field(
                sink.Records[0],
                "inventory.guest-reservation-reference")
                .GetGuid());
        Assert.Equal(
            "inventory.personal-data",
            contributor.Descriptor.CatalogId);
        Assert.Equal(
            InventoryDataRightsExportSchema.ExportSchemaVersion,
            contributor.Descriptor.ExportSchemaVersion);

        DataRightsSubjectExportResult stale =
            await contributor.ExportAsync(
                new(
                    ScopeId,
                    propertyId,
                    new(
                        InventoryDataRightsCoordinates.Owner,
                        InventoryDataRightsCoordinates.AllocationRecordType,
                        allocation.Id,
                        allocation.Version + 1)),
                new CollectingSink(),
                CancellationToken.None);
        Assert.Equal(DataRightsSubjectExportStatus.Stale, stale.Status);

        Assert.True(allocation.Anonymise(
            allocation.Version,
            Guid.NewGuid(),
            Now.AddMinutes(3)).IsSuccess);
        await dbContext.SaveChangesAsync();
        DataRightsSubjectExportResult hidden =
            await contributor.ExportAsync(
                new(
                    ScopeId,
                    propertyId,
                    new(
                        InventoryDataRightsCoordinates.Owner,
                        InventoryDataRightsCoordinates
                            .AllocationRecordType,
                        allocation.Id,
                        allocation.Version)),
                new CollectingSink(),
                CancellationToken.None);
        Assert.Equal(
            DataRightsSubjectExportStatus.NotFound,
            hidden.Status);
    }

    [Fact]
    public async Task Export_fails_closed_before_writing_when_the_record_limit_is_exceeded()
    {
        TestScopeContext scope = new(ScopeId);
        await using InventoryDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = SeedKnownProperty(dbContext);
        Guid reservationId = Guid.NewGuid();
        InventoryAllocation allocation = CreateAllocation(
            propertyId,
            reservationId);
        dbContext.Allocations.Add(allocation);
        dbContext.AllocationAmendmentDecisions.AddRange(
            Enumerable.Range(
                    0,
                    InventoryDataRightsExportContributor.MaximumExportRecords)
                .Select(index => CreateDecision(
                    Guid.NewGuid(),
                    allocation,
                    reservationId,
                    Now.AddTicks(index))));
        await dbContext.SaveChangesAsync();

        CollectingSink sink = new();
        InventoryDataRightsExportContributor contributor =
            new(dbContext, scope);
        DataRightsSubjectExportResult result =
            await contributor.ExportAsync(
                new(
                    ScopeId,
                    propertyId,
                    new(
                        InventoryDataRightsCoordinates.Owner,
                        InventoryDataRightsCoordinates.AllocationRecordType,
                        allocation.Id,
                        allocation.Version)),
                sink,
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectExportStatus.ScopeUnavailable,
            result.Status);
        Assert.Equal(0, result.RecordCount);
        Assert.Empty(sink.Records);
    }

    private static InventoryAllocation CreateAllocation(
        Guid propertyId,
        Guid reservationId) =>
        InventoryAllocation.CreateRejected(
            Guid.NewGuid(),
            ScopeId,
            reservationId,
            Guid.NewGuid(),
            propertyId,
            new(2026, 8, 1),
            new(2026, 8, 3),
            [Guid.NewGuid()],
            InventoryAllocationRejection.AllocationConflict,
            Now).Value;

    private static InventoryAllocationAmendmentDecision CreateDecision(
        Guid amendmentRequestId,
        InventoryAllocation allocation,
        Guid reservationId,
        DateTimeOffset decidedAtUtc) =>
        new(new InventoryAllocationAmendmentDecisionRecord(
            amendmentRequestId,
            ScopeId,
            allocation.Id,
            reservationId,
            allocation.PropertyId,
            new string('a', 64),
            Confirmed: false,
            InventoryAllocationRejectionReason.AllocationConflict,
            AllocationVersion: null,
            decidedAtUtc));

    private static Guid SeedKnownProperty(InventoryDbContext dbContext)
    {
        Guid propertyId = Guid.NewGuid();
        InventoryPropertyTopology property =
            InventoryPropertyTopology.Create(propertyId, ScopeId);
        property.Apply(
            "Hostel",
            "hostel",
            "UTC",
            PropertyStatus.Active,
            sourceVersion: 1);
        dbContext.PropertyTopology.Add(property);
        return propertyId;
    }

    private static JsonElement Field(
        DataRightsExportRecord record,
        string fieldId) =>
        Assert.Single(record.Fields, field =>
            string.Equals(
                field.FieldId,
                fieldId,
                StringComparison.Ordinal)).Value;

    private static InventoryDbContext CreateDbContext(
        IScopeContext scopeContext)
    {
        DbContextOptions<InventoryDbContext> options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(
                    $"inventory-data-rights-{Guid.NewGuid():N}")
                .Options;
        return new(options, scopeContext);
    }

    private sealed class CollectingSink : IDataRightsExportSink
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

    private sealed class TestScopeContext(string scopeId)
        : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}

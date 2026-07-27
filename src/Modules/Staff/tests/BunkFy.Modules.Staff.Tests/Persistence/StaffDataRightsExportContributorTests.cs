namespace BunkFy.Modules.Staff.Tests.Persistence;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffDataRightsExportContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Export_is_catalogue_versioned_and_contains_profile_and_assignment_history()
    {
        await using StaffDbContext dbContext = CreateDbContext("tenant-a");
        StaffMember member = CreateMember("tenant-a");
        Guid propertyId = Guid.NewGuid();
        StaffPropertyAssignment assignment = member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            "Front desk",
            isPrimary: true,
            new DateOnly(2026, 1, 1),
            member.Version,
            "user:manager",
            Guid.NewGuid(),
            Now.AddMinutes(1)).Value;
        Assert.True(member.UnassignProperty(
            propertyId,
            new DateOnly(2026, 7, 26),
            member.Version,
            "user:manager",
            "Moved to another property.",
            Guid.NewGuid(),
            Now.AddMinutes(2)).IsSuccess);
        dbContext.StaffMembers.Add(member);
        await dbContext.SaveChangesAsync();
        StaffDataRightsExportContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));
        CollectingSink sink = new();

        DataRightsSubjectExportResult result = await contributor.ExportAsync(
            Request("tenant-a", member.Id, member.Version),
            sink,
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectExportStatus.Succeeded, result.Status);
        Assert.Equal(2, result.RecordCount);
        Assert.Equal("staff.personal-data", contributor.Descriptor.CatalogId);
        Assert.Equal(2, contributor.Descriptor.CatalogVersion);
        Assert.Equal(
            StaffDataRightsExportSchema.ExportSchemaId,
            contributor.Descriptor.ExportSchemaId);
        Assert.DoesNotContain("staff.scope-id", contributor.Descriptor.FieldIds);
        Assert.DoesNotContain("staff.audit-actor-id", contributor.Descriptor.FieldIds);
        Assert.DoesNotContain("staff.change-reason", contributor.Descriptor.FieldIds);

        DataRightsExportRecord profileRecord = Assert.Single(
            sink.Records,
            record => record.RecordType == StaffDataRightsDiscoveryContributor.ProfileRecordType);
        Assert.Equal(member.Id, profileRecord.RecordId);
        Assert.Equal("Maya Chen", Field(profileRecord, "staff.display-name").GetString());
        Assert.Equal(
            "maya.chen@example.test",
            Field(profileRecord, "staff.work-email").GetString());
        Assert.Equal("active", Field(profileRecord, "staff.status").GetString());

        DataRightsExportRecord assignmentRecord = Assert.Single(
            sink.Records,
            record => record.RecordType == StaffDataRightsExportContributor.AssignmentRecordType);
        Assert.Equal(assignment.Id, assignmentRecord.RecordId);
        Assert.Equal(member.Id, Field(assignmentRecord, "staff.staff-member-id").GetGuid());
        Assert.Equal(propertyId, Field(assignmentRecord, "staff.property-id").GetGuid());
        Assert.False(Field(assignmentRecord, "staff.assignment-is-current").GetBoolean());
        Assert.Equal(assignment.UnassignedAtVersion, assignmentRecord.RecordVersion);
    }

    [Fact]
    public async Task Export_fails_closed_for_stale_or_cross_scope_coordinate_without_writing()
    {
        await using StaffDbContext dbContext = CreateDbContext("tenant-a");
        StaffMember member = CreateMember("tenant-a");
        dbContext.StaffMembers.Add(member);
        await dbContext.SaveChangesAsync();
        StaffDataRightsExportContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));
        CollectingSink staleSink = new();
        CollectingSink wrongTenantSink = new();
        CollectingSink propertySink = new();

        DataRightsSubjectExportResult stale = await contributor.ExportAsync(
            Request("tenant-a", member.Id, member.Version + 1),
            staleSink,
            CancellationToken.None);
        DataRightsSubjectExportResult wrongTenant = await contributor.ExportAsync(
            Request("tenant-b", member.Id, member.Version),
            wrongTenantSink,
            CancellationToken.None);
        DataRightsSubjectExportResult propertyScoped = await contributor.ExportAsync(
            new(
                "tenant-a",
                DataRightsCaseType.StaffRights,
                Guid.NewGuid(),
                new(
                    StaffDataRightsDiscoveryContributor.Owner,
                    StaffDataRightsDiscoveryContributor.ProfileRecordType,
                    member.Id,
                    member.Version)),
            propertySink,
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectExportStatus.Stale, stale.Status);
        Assert.Equal(DataRightsSubjectExportStatus.ScopeUnavailable, wrongTenant.Status);
        Assert.Equal(DataRightsSubjectExportStatus.ScopeUnavailable, propertyScoped.Status);
        Assert.Empty(staleSink.Records);
        Assert.Empty(wrongTenantSink.Records);
        Assert.Empty(propertySink.Records);
    }

    [Fact]
    public async Task Assignment_limit_is_checked_before_any_export_record_is_written()
    {
        await using StaffDbContext dbContext = CreateDbContext("tenant-a");
        StaffMember member = CreateMember("tenant-a");
        for (int index = 0;
             index <= StaffDataRightsExportContributor.MaximumAssignmentRecords;
             index++)
        {
            Assert.True(member.AssignProperty(
                Guid.NewGuid(),
                Guid.NewGuid(),
                propertyJobTitle: null,
                isPrimary: false,
                new DateOnly(2026, 1, 1),
                member.Version,
                "user:manager",
                Guid.NewGuid(),
                Now.AddSeconds(index + 1)).IsSuccess);
        }

        dbContext.StaffMembers.Add(member);
        await dbContext.SaveChangesAsync();
        StaffDataRightsExportContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));
        CollectingSink sink = new();

        DataRightsSubjectExportResult result = await contributor.ExportAsync(
            Request("tenant-a", member.Id, member.Version),
            sink,
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectExportStatus.ScopeUnavailable, result.Status);
        Assert.Empty(sink.Records);
    }

    private static DataRightsSubjectExportRequest Request(
        string tenantId,
        Guid staffMemberId,
        long version) =>
        new(
            tenantId,
            DataRightsCaseType.StaffRights,
            PropertyId: null,
            new(
                StaffDataRightsDiscoveryContributor.Owner,
                StaffDataRightsDiscoveryContributor.ProfileRecordType,
                staffMemberId,
                version));

    private static StaffMember CreateMember(string tenantId) =>
        StaffMember.Create(
            Guid.NewGuid(),
            tenantId,
            "Maya Chen",
            "Maya Q. Chen",
            "maya.chen@example.test",
            "+44 20 1234 5678",
            "EMP-42",
            "Manager",
            "Operations",
            "account-maya",
            "user:test",
            Guid.NewGuid(),
            Now).Value;

    private static JsonElement Field(DataRightsExportRecord record, string fieldId) =>
        Assert.Single(record.Fields, field => field.FieldId == fieldId).Value;

    private static StaffDbContext CreateDbContext(string tenantId)
    {
        DbContextOptions<StaffDbContext> options =
            new DbContextOptionsBuilder<StaffDbContext>()
                .UseInMemoryDatabase($"staff-data-rights-export-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext(tenantId));
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

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}

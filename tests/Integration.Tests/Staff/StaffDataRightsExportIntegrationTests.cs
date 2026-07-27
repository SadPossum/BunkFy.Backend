namespace Integration.Tests;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class StaffDataRightsExportIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Staff_discovery_and_export_use_authoritative_postgresql_records()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_staff_data_rights_export_tests")
            .Build();
        await postgreSql.StartAsync();

        StaffMember member = StaffMember.Create(
            Guid.NewGuid(),
            "tenant-a",
            "Maya Chen",
            "Maya Q. Chen",
            "maya.chen@example.test",
            "+44 20 1234 5678",
            "EMP-42",
            "Manager",
            "Operations",
            "account-maya",
            "user:operator",
            Guid.NewGuid(),
            Now).Value;
        Guid propertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            "Front desk",
            isPrimary: true,
            new DateOnly(2026, 1, 1),
            member.Version,
            "user:operator",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);

        using ServiceProvider provider = CreatePersistenceProvider(postgreSql.GetConnectionString());
        using IServiceScope scope = provider.CreateScope();
        StaffDbContext dbContext = scope.ServiceProvider.GetRequiredService<StaffDbContext>();
        await dbContext.Database.MigrateAsync();
        dbContext.StaffMembers.Add(member);
        await dbContext.SaveChangesAsync();

        IDataRightsSubjectDiscoveryContributor discovery = scope.ServiceProvider
            .GetServices<IDataRightsSubjectDiscoveryContributor>()
            .Single(candidate => candidate.OwnerKey == "staff");
        DataRightsSubjectDiscoveryResult discovered = await discovery.DiscoverAsync(
            new(
                "tenant-a",
                DataRightsCaseType.StaffRights,
                PropertyId: null,
                new(
                    RecordId: null,
                    Email: null,
                    Phone: null,
                    Name: null,
                    DateOfBirth: null,
                    AccountSubjectId: "account-maya"),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);
        DataRightsSubjectCandidate candidate = Assert.Single(discovered.Candidates);

        IDataRightsSubjectExportContributor exporter = scope.ServiceProvider
            .GetServices<IDataRightsSubjectExportContributor>()
            .Single(contributor => contributor.OwnerKey == "staff");
        CollectingSink sink = new();
        DataRightsSubjectExportResult exported = await exporter.ExportAsync(
            new(
                "tenant-a",
                DataRightsCaseType.StaffRights,
                PropertyId: null,
                candidate.Coordinate),
            sink,
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectDiscoveryStatus.Succeeded, discovered.Status);
        Assert.Equal(member.Id, candidate.Coordinate.RecordId);
        Assert.Equal(DataRightsSubjectExportStatus.Succeeded, exported.Status);
        Assert.Equal(2, exported.RecordCount);
        DataRightsExportRecord profile = Assert.Single(
            sink.Records,
            record => record.RecordType == "staff-member");
        Assert.Equal(
            "maya.chen@example.test",
            Field(profile, "staff.work-email").GetString());
        DataRightsExportRecord assignment = Assert.Single(
            sink.Records,
            record => record.RecordType == "staff-property-assignment");
        Assert.Equal(propertyId, Field(assignment, "staff.property-id").GetGuid());
    }

    private static ServiceProvider CreatePersistenceProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = connectionString;
        builder.Services.AddSingleton<IScopeContext>(new TestScopeContext("tenant-a"));
        builder.AddStaffPersistence();
        return builder.Services.BuildServiceProvider();
    }

    private static JsonElement Field(DataRightsExportRecord record, string fieldId) =>
        Assert.Single(record.Fields, field => field.FieldId == fieldId).Value;

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

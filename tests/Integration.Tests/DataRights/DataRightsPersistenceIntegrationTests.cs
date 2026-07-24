namespace Integration.Tests;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class DataRightsPersistenceIntegrationTests
{
    private const string InitialMigration = "20260723052104_InitialDataRights";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Subject_selection_migration_upgrades_existing_cases_and_round_trips_coordinates()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_data_rights_subject_selection_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid caseId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid propertyId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid guestId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        DateTimeOffset createdAtUtc = new(2026, 7, 23, 6, 0, 0, TimeSpan.Zero);
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DataRightsCase existingCase = DataRightsCase.Create(
            caseId,
            "tenant-a",
            request,
            "staff:privacy",
            createdAtUtc).Value;

        await using (DataRightsDbContext initial = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await initial.Database.GetService<IMigrator>().MigrateAsync(InitialMigration);
            initial.Cases.Add(existingCase);
            await initial.SaveChangesAsync();
        }

        DateTimeOffset selectedAtUtc = createdAtUtc.AddMinutes(10);
        await using (DataRightsDbContext upgraded = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await upgraded.Database.MigrateAsync();
            DataRightsCase dataRightsCase = await upgraded.Cases.SingleAsync(item => item.Id == caseId);

            Assert.True(dataRightsCase.BeginDiscovery(
                dataRightsCase.Version,
                "staff:privacy",
                selectedAtUtc).IsSuccess);
            Assert.True(dataRightsCase.SelectSubject(
                "guests",
                "guest-record",
                guestId,
                7,
                dataRightsCase.Version,
                "staff:privacy",
                selectedAtUtc).IsSuccess);

            await upgraded.SaveChangesAsync();
        }

        await using (DataRightsDbContext reloaded = CreateDbContext(postgreSql.GetConnectionString()))
        {
            DataRightsCase dataRightsCase = await reloaded.Cases
                .SingleAsync(item => item.Id == caseId);
            var selected = Assert.Single(dataRightsCase.SelectedSubjects);

            Assert.Equal("guests", selected.OwnerKey);
            Assert.Equal("guest-record", selected.RecordType);
            Assert.Equal(guestId, selected.RecordId);
            Assert.Equal(7, selected.RecordVersion);
            Assert.Equal("staff:privacy", selected.SelectedBy);
            Assert.Equal(selectedAtUtc, selected.SelectedAtUtc);

            reloaded.Cases.Remove(dataRightsCase);
            await reloaded.SaveChangesAsync();
            Assert.Empty(await reloaded.Database.SqlQueryRaw<Guid>(
                """SELECT "RecordId" AS "Value" FROM "data-rights"."selected_subjects" """)
                .ToListAsync());
        }
    }

    private static DataRightsDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<DataRightsDbContext> options = new DbContextOptionsBuilder<DataRightsDbContext>()
            .UseNpgsql(connectionString, provider => provider
                .MigrationsAssembly(DataRightsMigrations.PostgreSqlAssembly)
                .MigrationsHistoryTable(DataRightsMigrations.HistoryTable, DataRightsMigrations.Schema))
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

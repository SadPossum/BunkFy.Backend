namespace Integration.Tests.DataRights;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class GuestRightsResponseDeadlinePersistenceIntegrationTests
{
    private const string PreviousMigration =
        "20260804183357_AddTenantTerminationOperatorCaseLifecycle";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Deadline_evidence_round_trips_and_guards_shape_and_downgrade()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_guest_rights_deadline_tests")
                .Build();
        await postgreSql.StartAsync();

        Guid propertyId = Guid.Parse(
            "21000000-0000-0000-0000-000000000001");
        Guid caseId = Guid.Parse(
            "11000000-0000-0000-0000-000000000001");
        DateTimeOffset receivedAtUtc =
            new(2026, 1, 31, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset dueAtUtc =
            new(2026, 2, 28, 10, 0, 0, TimeSpan.Zero);
        DataRightsResponseDeadlinePolicyEvidence evidence =
            DataRightsResponseDeadlinePolicyEvidence.Create(
                propertyId,
                propertyTopologySourceVersion: 7,
                propertyPolicySourceVersion: 11,
                "GB",
                "integration-hostel-policy",
                policyVersion: 2,
                new string('d', 64),
                DataRightsResponseRight.Export,
                "rights-response-access-export",
                periodYears: 0,
                periodMonths: 1,
                periodDays: 0,
                "Europe/London",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
                receivedAtUtc,
                receivedAtUtc.AddSeconds(1),
                dueAtUtc)
            .Value;
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.DataSubject)
            .Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            caseId,
            "tenant-a",
            request,
            "staff:privacy",
            receivedAtUtc,
            evidence)
            .Value;
        DataRightsPropertyProjection property = new(
            "tenant-a",
            propertyId,
            "Integration Hostel",
            "Europe/London",
            PropertyStatus.Active,
            version: 7);
        PropertyGovernancePolicyBinding policy = new(
            "GB",
            "integration-hostel-policy",
            2,
            "integration-region",
            "integration-no-transfer",
            "integration-guest-operational",
            1,
            new string('d', 64),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            receivedAtUtc.AddDays(-1),
            [new("integration-operator-notice", 1)]);
        property.ApplyPolicy(
            PropertyProcessingStatus.Enabled,
            policy,
            sourceVersion: 11);

        await using (DataRightsDbContext writer = CreateDbContext(
            postgreSql.GetConnectionString()))
        {
            await writer.Database.MigrateAsync();
            writer.PropertyProjections.Add(property);
            writer.Cases.Add(dataRightsCase);
            await writer.SaveChangesAsync();
        }

        await using (DataRightsDbContext reader = CreateDbContext(
            postgreSql.GetConnectionString()))
        {
            DataRightsCase restored = await reader.Cases
                .SingleAsync(item => item.Id == caseId);
            DataRightsResponseDeadlinePolicyEvidence restoredEvidence =
                Assert.IsType<DataRightsResponseDeadlinePolicyEvidence>(
                    restored.ResponseDeadlinePolicyEvidence);
            Assert.Equal(dueAtUtc, restored.DueAtUtc);
            Assert.True(evidence.HasSameCoordinates(restoredEvidence));

            DataRightsPropertyProjection restoredProperty =
                await reader.PropertyProjections.SingleAsync(
                    item => item.Id == propertyId);
            Assert.Equal("Europe/London", restoredProperty.TimeZoneId);
            Assert.Equal(7, restoredProperty.TopologySourceVersion);
            Assert.Equal(11, restoredProperty.PolicySourceVersion);

            PostgresException invalidShape =
                await Assert.ThrowsAsync<PostgresException>(() =>
                    reader.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "data-rights"."cases"
                        SET "DueAtUtc" = {dueAtUtc.AddDays(1)}
                        WHERE "Id" = {caseId}
                        """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, invalidShape.SqlState);
            Assert.Equal(
                "CK_data_rights_cases_response_deadline_evidence",
                invalidShape.ConstraintName);
        }

        await using DataRightsDbContext downgrade = CreateDbContext(
            postgreSql.GetConnectionString());
        PostgresException unsafeDowngrade =
            await Assert.ThrowsAsync<PostgresException>(() =>
                downgrade.Database.GetService<IMigrator>()
                    .MigrateAsync(PreviousMigration));
        Assert.Equal(PostgresErrorCodes.RaiseException, unsafeDowngrade.SqlState);
        Assert.Contains(
            "Cannot downgrade Data Rights while response-deadline policy evidence exists",
            unsafeDowngrade.MessageText,
            StringComparison.Ordinal);
    }

    private static DataRightsDbContext CreateDbContext(
        string connectionString)
    {
        DbContextOptions<DataRightsDbContext> options =
            new DbContextOptionsBuilder<DataRightsDbContext>()
                .UseNpgsql(connectionString, provider => provider
                    .MigrationsAssembly(
                        DataRightsMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(
                        DataRightsMigrations.HistoryTable,
                        DataRightsMigrations.Schema))
                .Options;
        return new DataRightsDbContext(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

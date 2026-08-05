namespace Integration.Tests.DataRights;

using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class DataRightsProofRevisionMigrationIntegrationTests
{
    private const string PreviousMigration =
        "20260731153319_AddTenantTerminationExportArtifacts";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Zero_revision_owner_proofs_are_enabled_by_the_upgrade()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_data_rights_proof_revision_migration")
                .Build();
        await postgreSql.StartAsync();

        await using (DataRightsDbContext previous = CreateDbContext(
            postgreSql.GetConnectionString()))
        {
            await previous.Database.GetService<IMigrator>()
                .MigrateAsync(PreviousMigration);
        }

        AssertRequiresPositiveRevision(
            await ReadProofConstraintsAsync(
                postgreSql.GetConnectionString()));

        await using (DataRightsDbContext upgraded = CreateDbContext(
            postgreSql.GetConnectionString()))
        {
            await upgraded.Database.MigrateAsync();
        }

        AssertAllowsZeroRevision(
            await ReadProofConstraintsAsync(
                postgreSql.GetConnectionString()));
    }

    private static void AssertRequiresPositiveRevision(
        IReadOnlyDictionary<string, string> constraints)
    {
        Assert.Equal(2, constraints.Count);
        Assert.All(constraints.Values, definition =>
        {
            Assert.Contains(
                "\"SelectedProofRevision\" >= 1",
                definition,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "\"SelectedProofRevision\" >= 0",
                definition,
                StringComparison.Ordinal);
        });
    }

    private static void AssertAllowsZeroRevision(
        IReadOnlyDictionary<string, string> constraints)
    {
        Assert.Equal(2, constraints.Count);
        Assert.All(constraints.Values, definition =>
        {
            Assert.Contains(
                "\"SelectedProofRevision\" >= 0",
                definition,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "\"SelectedProofRevision\" >= 1",
                definition,
                StringComparison.Ordinal);
        });
    }

    private static async Task<IReadOnlyDictionary<string, string>>
        ReadProofConstraintsAsync(string connectionString)
    {
        const string ownerConstraint =
            "CK_data_rights_tenant_termination_owner_result";
        const string fragmentConstraint =
            "CK_data_rights_tenant_termination_export_fragment_proof";
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT constraint_name, pg_get_constraintdef(pg_constraint.oid)
            FROM information_schema.table_constraints
            JOIN pg_constraint
              ON pg_constraint.conname = constraint_name
            JOIN pg_namespace
              ON pg_namespace.oid = pg_constraint.connamespace
             AND pg_namespace.nspname = constraint_schema
            WHERE constraint_schema = 'data-rights'
              AND constraint_name IN (
                'CK_data_rights_tenant_termination_owner_result',
                'CK_data_rights_tenant_termination_export_fragment_proof')
            ORDER BY constraint_name;
            """,
            connection);

        Dictionary<string, string> constraints =
            new(StringComparer.Ordinal);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            constraints.Add(reader.GetString(0), reader.GetString(1));
        }

        Assert.Contains(ownerConstraint, constraints.Keys);
        Assert.Contains(fragmentConstraint, constraints.Keys);
        return constraints;
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
        public string? ScopeId => "tenant-a";
    }
}

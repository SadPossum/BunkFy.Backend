namespace Integration.Tests;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class StaffDirectoryPostgreSqlIntegrationTests
{
    private const string TenantId =
        "aa000000-0000-0000-0000-000000000001";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 8, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Retired_property_is_excluded_from_operational_directory_but_retained_in_history()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_staff_directory_tests")
                .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);
        await using StaffDbContext dbContext = CreateDbContext(
            postgreSql.GetConnectionString());
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        Guid activePropertyId = Guid.NewGuid();
        Guid retiredPropertyId = Guid.NewGuid();
        StaffMember member = CreateMember();
        Assign(member, activePropertyId, isPrimary: true);
        Assign(member, retiredPropertyId, isPrimary: false);
        var repository = new StaffMemberRepository(dbContext);
        await repository.AddAsync(member, CancellationToken.None)
            .ConfigureAwait(false);
        dbContext.PropertyProjections.AddRange(
            new StaffPropertyProjection(
                TenantId,
                activePropertyId,
                "Open House",
                PropertyStatus.Active,
                1),
            new StaffPropertyProjection(
                TenantId,
                retiredPropertyId,
                string.Empty,
                PropertyStatus.Retired,
                2));
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        StaffMember history = Assert.IsType<StaffMember>(
            await repository.GetForDataRightsAsync(
                    member.Id,
                    CancellationToken.None)
                .ConfigureAwait(false));
        StaffDirectoryMemberDto directory = Assert.IsType<StaffDirectoryMemberDto>(
            await repository.GetDirectoryAsync(
                    member.Id,
                    CancellationToken.None)
                .ConfigureAwait(false));
        StaffDirectoryListResponse list = await repository.ListDirectoryAsync(
                search: null,
                status: null,
                new PageRequest(1, 20),
                CancellationToken.None)
            .ConfigureAwait(false);
        StaffDirectoryMemberDto? retiredDirectory =
            await repository.GetDirectoryAtPropertyAsync(
                    retiredPropertyId,
                    member.Id,
                    CancellationToken.None)
                .ConfigureAwait(false);
        StaffPropertyDirectoryListResponse retiredList =
            await repository.ListDirectoryAtPropertyAsync(
                    retiredPropertyId,
                    search: null,
                    status: null,
                    new PageRequest(1, 20),
                    CancellationToken.None)
                .ConfigureAwait(false);

        Assert.Equal(2, history.Assignments.Count);
        Assert.Equal(
            activePropertyId,
            Assert.Single(directory.Assignments).PropertyId);
        Assert.Equal(1, Assert.Single(list.Items).CurrentPropertyCount);
        Assert.Null(retiredDirectory);
        Assert.Empty(retiredList.Items);
    }

    private static StaffDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<StaffDbContext> options =
            new DbContextOptionsBuilder<StaffDbContext>()
                .UseNpgsql(connectionString, provider => provider
                    .MigrationsAssembly(StaffMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(
                        StaffMigrations.HistoryTable,
                        StaffMigrations.Schema))
                .Options;
        return new(
            options,
            new TestScopeContext(),
            OpenWorkspaceTerminationFenceReader.Instance);
    }

    private static StaffMember CreateMember() => StaffMember.Create(
        Guid.NewGuid(),
        TenantId,
        "Ada Operator",
        "Ada Lovelace",
        "ada@example.test",
        null,
        "EMP-100",
        "Manager",
        "Operations",
        "auth-ada",
        "user:owner",
        Guid.NewGuid(),
        Now).Value;

    private static void Assign(
        StaffMember member,
        Guid propertyId,
        bool isPrimary) => Assert.True(member.AssignProperty(
        Guid.NewGuid(),
        propertyId,
        propertyJobTitle: null,
        isPrimary,
        new DateOnly(2026, 8, 11),
        member.Version,
        "user:owner",
        Guid.NewGuid(),
        Now).IsSuccess);

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }
}

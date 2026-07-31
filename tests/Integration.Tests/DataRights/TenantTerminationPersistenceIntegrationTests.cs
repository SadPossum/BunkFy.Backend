namespace Integration.Tests;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class TenantTerminationPersistenceIntegrationTests
{
    private const string BeforeTenantTerminationMigration =
        "20260729222749_VersionScopedAnonymisationOwnerProtocol";
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Migration_fences_one_active_process_and_exact_owner_coordinates()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_tenant_termination_foundation_tests")
                .Build();
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();

        DataRightsCase firstCase = CreateCase("tenant-a");
        await using (DataRightsDbContext previous = CreateDbContext(
            connectionString,
            "tenant-a"))
        {
            await previous.Database.GetService<IMigrator>()
                .MigrateAsync(BeforeTenantTerminationMigration);
            previous.Cases.Add(firstCase);
            await previous.SaveChangesAsync();
        }

        TenantTerminationProcess firstProcess = PrepareProcess(
            "tenant-a",
            firstCase.Id);
        DataRightsCase secondCase = CreateCase("tenant-a");
        TenantTerminationProcess secondProcess = PrepareProcess(
            "tenant-a",
            secondCase.Id);
        await using (DataRightsDbContext tenantA = CreateDbContext(
            connectionString,
            "tenant-a"))
        {
            await tenantA.Database.MigrateAsync();
            Assert.Equal(
                firstCase.Id,
                (await tenantA.Cases.SingleAsync()).Id);
            Assert.Empty(await tenantA.Database.GetPendingMigrationsAsync());

            tenantA.Cases.Add(secondCase);
            tenantA.TenantTerminationProcesses.Add(firstProcess);
            await tenantA.SaveChangesAsync();

            PostgresException duplicateActive =
                await Assert.ThrowsAsync<PostgresException>(() =>
                    tenantA.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "data-rights"."tenant_termination_processes"
                            ("Id", "IdempotencyKey", "CaseId",
                             "ApprovalRevision", "TerminationEpoch",
                             "ExportRequested", "PolicyEvidenceSha256",
                             "ApprovedBy", "ApprovedAtUtc", "Phase", "Status",
                             "OperationRevision", "OutcomeCode",
                             "HoldReviewAtUtc", "CreatedBy", "CreatedAtUtc",
                             "LastChangedBy", "LastChangedAtUtc", "Version",
                             "ScopeId")
                        VALUES
                            ({secondProcess.Id}, {secondProcess.IdempotencyKey},
                             {secondProcess.CaseId},
                             {secondProcess.ApprovalRevision},
                             {secondProcess.TerminationEpoch},
                             {secondProcess.ExportRequested},
                             {secondProcess.PolicyEvidenceSha256},
                             {secondProcess.ApprovedBy},
                             {secondProcess.ApprovedAtUtc},
                             {(int)secondProcess.Phase},
                             {(int)secondProcess.Status},
                             {secondProcess.OperationRevision}, NULL, NULL,
                             {secondProcess.CreatedBy},
                             {secondProcess.CreatedAtUtc},
                             {secondProcess.LastChangedBy},
                             {secondProcess.LastChangedAtUtc},
                             {secondProcess.Version}, {"tenant-a"});
                        """));
            Assert.Equal(
                PostgresErrorCodes.UniqueViolation,
                duplicateActive.SqlState);

            CompleteProcess(firstProcess);
            await tenantA.SaveChangesAsync();
            tenantA.TenantTerminationProcesses.Add(secondProcess);
            await tenantA.SaveChangesAsync();

            TenantTerminationOwnerWorkItem mismatched =
                PrepareOwnerWork(
                    secondProcess,
                    firstCase.Id,
                    Guid.NewGuid());
            tenantA.TenantTerminationOwnerWorkItems.Add(mismatched);
            DbUpdateException mismatch =
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => tenantA.SaveChangesAsync());
            Assert.Equal(
                PostgresErrorCodes.ForeignKeyViolation,
                Assert.IsType<PostgresException>(
                    mismatch.GetBaseException()).SqlState);
            tenantA.ChangeTracker.Clear();

            TenantTerminationOwnerWorkItem valid = PrepareOwnerWork(
                secondProcess,
                secondProcess.CaseId,
                Guid.NewGuid());
            tenantA.TenantTerminationOwnerWorkItems.Add(valid);
            await tenantA.SaveChangesAsync();
            Assert.Equal(
                valid.Id,
                (await tenantA.TenantTerminationOwnerWorkItems.SingleAsync()).Id);
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            connectionString,
            "tenant-b");
        DataRightsCase tenantBCase = CreateCase("tenant-b");
        TenantTerminationProcess tenantBProcess = PrepareProcess(
            "tenant-b",
            tenantBCase.Id);
        tenantB.Cases.Add(tenantBCase);
        tenantB.TenantTerminationProcesses.Add(tenantBProcess);
        await tenantB.SaveChangesAsync();
        Assert.Equal(
            tenantBProcess.Id,
            (await tenantB.TenantTerminationProcesses.SingleAsync()).Id);
    }

    private static void CompleteProcess(TenantTerminationProcess process)
    {
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            "owner:approver",
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            process.OperationRevision,
            process.Version,
            "owner:approver",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Destroy,
            process.Version,
            "owner:executor",
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Destroy,
            process.OperationRevision,
            process.Version,
            "owner:executor",
            Now.AddMinutes(4)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Verify,
            process.Version,
            "owner:executor",
            Now.AddMinutes(5)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Verify,
            process.OperationRevision,
            process.Version,
            "owner:executor",
            Now.AddMinutes(6)).IsSuccess);
    }

    private static DataRightsCase CreateCase(string tenantId)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.TenantOwner).Value;
        return DataRightsCase.Create(
            Guid.NewGuid(),
            tenantId,
            request,
            "owner:approver",
            Now).Value;
    }

    private static TenantTerminationProcess PrepareProcess(
        string tenantId,
        Guid caseId) =>
        TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            caseId,
            approvalRevision: 4,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "owner:approver",
            Now,
            "system:tenant-termination",
            Now).Value;

    private static TenantTerminationOwnerWorkItem PrepareOwnerWork(
        TenantTerminationProcess process,
        Guid caseId,
        Guid idempotencyKey) =>
        TenantTerminationOwnerWorkItem.Prepare(
            Guid.NewGuid(),
            process.ScopeId,
            process.Id,
            caseId,
            process.ApprovalRevision,
            operationRevision: 1,
            process.TerminationEpoch,
            idempotencyKey,
            TenantTerminationOwnerPhase.Freeze,
            "workspaces",
            ownerContractVersion: 1,
            catalogVersion: 1,
            Digest,
            process.PolicyEvidenceSha256,
            Now).Value;

    private static DataRightsDbContext CreateDbContext(
        string connectionString,
        string tenantId)
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
        return new(options, new TestScopeContext(tenantId));
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}

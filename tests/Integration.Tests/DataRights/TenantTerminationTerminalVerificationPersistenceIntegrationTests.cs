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

public sealed class
    TenantTerminationTerminalVerificationPersistenceIntegrationTests
{
    private const string BeforeTerminalVerificationMigration =
        "20260804041828_AllowZeroTenantTerminationProofRevision";
    private const string Executor = "system:tenant-termination";
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 22, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task
        Terminal_receipt_resumes_crash_boundary_and_binds_exact_process()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_tenant_termination_terminal_receipt")
                .Build();
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();

        await ApplyMigrationsAsync(connectionString);
        PersistedTermination tenantA = await PersistVerifyBoundaryAsync(
            connectionString,
            "tenant-a");
        await CompleteFromPersistedBoundaryAsync(
            connectionString,
            "tenant-a",
            tenantA);

        await using (DataRightsDbContext verifier = CreateDbContext(
            connectionString,
            "tenant-a"))
        {
            TenantTerminationProcess process =
                await verifier.TenantTerminationProcesses.SingleAsync();
            TenantTerminationTerminalReceipt receipt =
                await verifier.TenantTerminationTerminalReceipts.SingleAsync();
            Assert.Equal(TenantTerminationProcessPhase.Completed, process.Phase);
            Assert.Equal(
                TenantTerminationProcessStatus.Completed,
                process.Status);
            Assert.Equal(receipt.Id, process.TerminalReceiptId);
            Assert.Equal(receipt.Version, process.TerminalReceiptVersion);
            Assert.Equal(process.Id, receipt.ProcessId);
            Assert.Equal(
                process.VerificationConfirmedOperationRevision,
                receipt.VerificationOperationRevision);
        }

        PersistedTermination tenantB = await PersistVerifyBoundaryAsync(
            connectionString,
            "tenant-b");
        await CompleteFromPersistedBoundaryAsync(
            connectionString,
            "tenant-b",
            tenantB);

        await using DataRightsDbContext crossLink = CreateDbContext(
            connectionString,
            "tenant-a");
        PostgresException rejected =
            await Assert.ThrowsAsync<PostgresException>(() =>
                crossLink.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "data-rights"."tenant_termination_processes"
                    SET "TerminalReceiptId" = {tenantB.ReceiptId},
                        "TerminalReceiptVersion" = 1
                    WHERE "Id" = {tenantA.ProcessId};
                    """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, rejected.SqlState);

        TenantTerminationProcess unchanged =
            await crossLink.TenantTerminationProcesses.SingleAsync();
        Assert.Equal(tenantA.ReceiptId, unchanged.TerminalReceiptId);
    }

    private static async Task ApplyMigrationsAsync(string connectionString)
    {
        await using DataRightsDbContext context = CreateDbContext(
            connectionString,
            "tenant-a");
        IMigrator migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeTerminalVerificationMigration);
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private static async Task<PersistedTermination>
        PersistVerifyBoundaryAsync(
            string connectionString,
            string tenantId)
    {
        DataRightsCase dataRightsCase = CreateCase(tenantId);
        TenantTerminationProcess process = PrepareVerifyProcess(
            tenantId,
            dataRightsCase);
        Guid receiptId = Guid.NewGuid();

        await using DataRightsDbContext writer = CreateDbContext(
            connectionString,
            tenantId);
        writer.Cases.Add(dataRightsCase);
        writer.TenantTerminationProcesses.Add(process);
        await writer.SaveChangesAsync();

        return new(process.Id, receiptId);
    }

    private static async Task CompleteFromPersistedBoundaryAsync(
        string connectionString,
        string tenantId,
        PersistedTermination persisted)
    {
        await using DataRightsDbContext finisher = CreateDbContext(
            connectionString,
            tenantId);
        TenantTerminationProcess process =
            await finisher.TenantTerminationProcesses.SingleAsync(candidate =>
                candidate.Id == persisted.ProcessId);
        TenantTerminationTerminalReceipt receipt =
            TenantTerminationTerminalReceipt.Seal(
                persisted.ReceiptId,
                process.ScopeId,
                Guid.NewGuid(),
                process.Id,
                process.CaseId,
                process.ApprovalRevision,
                process.TerminationEpoch,
                process.DestroyCompletedOperationRevision!.Value,
                process.OperationRevision,
                Guid.NewGuid(),
                verificationTaskAttempt: 1,
                process.PolicyEvidenceSha256,
                process.FrozenRevisionSha256!,
                exportRequested: false,
                exportArtifactId: null,
                exportArtifactVersion: null,
                exportFragmentSetSha256: null,
                ownerCount: 1,
                Digest,
                "workspaces",
                terminalOwnerSelectedProofRevision: 2,
                terminalOwnerResultingProofRevision: 3,
                replayCheckpointSequence: 2,
                Digest,
                replayIntegrityKeyVersion: 1,
                Digest,
                Now.AddMinutes(6),
                Executor,
                Now.AddMinutes(7)).Value;

        Assert.True(process.ConfirmVerification(
            process.OperationRevision,
            receipt.Id,
            receipt.Version,
            receipt.OwnerProofSetSha256,
            process.Version,
            Executor,
            Now.AddMinutes(7)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Verify,
            process.OperationRevision,
            process.Version,
            Executor,
            Now.AddMinutes(7)).IsSuccess);
        finisher.TenantTerminationTerminalReceipts.Add(receipt);

        await finisher.SaveChangesAsync();
    }

    private static TenantTerminationProcess PrepareVerifyProcess(
        string tenantId,
        DataRightsCase dataRightsCase)
    {
        TenantTerminationProcess process = TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            dataRightsCase.Id,
            dataRightsCase.DecisionRevision!.Value,
            Guid.NewGuid(),
            exportRequested: false,
            dataRightsCase.TenantTerminationPolicyEvidenceSha256!,
            dataRightsCase.DecidedBy!,
            dataRightsCase.DecidedAtUtc!.Value,
            Executor,
            dataRightsCase.ExecutionStartedAtUtc!.Value).Value;
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Executor,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.CompleteFreeze(
            process.OperationRevision,
            workspaceFenceRevision: 1,
            Digest,
            [
                new TenantTerminationFrozenOwnerDescriptor(
                    "workspaces",
                    ContractVersion: 1,
                    CatalogVersion: 1,
                    Digest)
            ],
            process.Version,
            Executor,
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Destroy,
            process.Version,
            Executor,
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Destroy,
            process.OperationRevision,
            process.Version,
            Executor,
            Now.AddMinutes(4)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Verify,
            process.Version,
            Executor,
            Now.AddMinutes(5)).IsSuccess);
        return process;
    }

    private static DataRightsCase CreateCase(string tenantId)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.TenantOwner).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            tenantId,
            request,
            "owner:requester",
            Now).Value;
        Assert.True(dataRightsCase.PrepareTenantTerminationReview(
            exportRequested: false,
            dataRightsCase.Version,
            "owner:requester",
            Now).IsSuccess);
        Assert.True(dataRightsCase.RecordTenantTerminationDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            Digest,
            dataRightsCase.Version,
            "owner:approver",
            Now.AddSeconds(1)).IsSuccess);
        Assert.True(dataRightsCase.BeginTenantTerminationExecution(
            dataRightsCase.Version,
            Executor,
            Now.AddSeconds(2)).IsSuccess);
        return dataRightsCase;
    }

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

    private sealed record PersistedTermination(
        Guid ProcessId,
        Guid ReceiptId);

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}

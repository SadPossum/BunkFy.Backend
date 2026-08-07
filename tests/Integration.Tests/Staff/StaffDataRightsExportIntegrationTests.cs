namespace Integration.Tests;

using System.Data;
using System.Data.Common;
using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Governance;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Scoping;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class StaffDataRightsExportIntegrationTests
{
    private const string LegacyStaffMigration =
        "20260729101543_AddStaffDataRightsCorrectionReceipts";
    private const string FoundationPreviousMigration =
        "20260729131820_AddStaffProcessingRestrictions";
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

        using ServiceProvider provider =
            CreatePersistenceProvider(postgreSql.GetConnectionString());
        using (IServiceScope legacyScope = provider.CreateScope())
        {
            StaffDbContext legacy = legacyScope.ServiceProvider
                .GetRequiredService<StaffDbContext>();
            await legacy.Database.GetService<IMigrator>()
                .MigrateAsync(LegacyStaffMigration);
            await LegacyStaffPersistenceTestData.InsertMemberAsync(
                legacy,
                member);
        }

        using (IServiceScope migratedScope = provider.CreateScope())
        {
            StaffDbContext migrated = migratedScope.ServiceProvider
                .GetRequiredService<StaffDbContext>();
            await migrated.Database.MigrateAsync();
            Assert.Equal(
                1,
                await GetOperationLockRevisionAsync(
                    migrated,
                    member.Id));

            StaffEmploymentGovernanceBinding binding =
                StaffEmploymentGovernanceBinding.Create(
                    "GB",
                    "development-hostel-example",
                    1,
                    "eu-west-2",
                    "uk-no-transfer",
                    "development-staff-employment",
                    1,
                    new string('a', 64),
                    Now.AddDays(-1),
                    Now.AddYears(1),
                    Now.AddMinutes(2)).Value;
            StaffEmploymentGovernance governance =
                StaffEmploymentGovernance.Configure(
                    "tenant-a",
                    member.Id,
                    member.Version,
                    binding,
                    [
                        StaffEmploymentGovernanceAcknowledgement.Create(
                            "operator-notice",
                            1).Value
                    ],
                    "user:privacy",
                    Now.AddMinutes(3)).Value;
            StaffDataHold hold = StaffDataHold.Place(
                Guid.NewGuid(),
                "tenant-a",
                member.Id,
                StaffDataHoldReasonCodes.RegulatoryRequest,
                "user:privacy",
                Now.AddMinutes(4)).Value;
            migrated.EmploymentGovernance.Add(governance);
            migrated.DataHolds.Add(hold);
            await migrated.SaveChangesAsync();

            IDataRightsSubjectDiscoveryContributor discovery =
                migratedScope.ServiceProvider
                    .GetServices<
                        IDataRightsSubjectDiscoveryContributor>()
                    .Single(candidate =>
                        candidate.OwnerKey == "staff");
            DataRightsSubjectDiscoveryResult discovered =
                await discovery.DiscoverAsync(
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
                        DataRightsSubjectDiscoveryLimits
                            .MaxCandidates),
                    CancellationToken.None);
            DataRightsSubjectCandidate candidate =
                Assert.Single(discovered.Candidates);

            IDataRightsSubjectExportContributor exporter =
                migratedScope.ServiceProvider
                    .GetServices<
                        IDataRightsSubjectExportContributor>()
                    .Single(contributor =>
                        contributor.OwnerKey == "staff");
            CollectingSink sink = new();
            DataRightsSubjectExportResult exported =
                await exporter.ExportAsync(
                    new(
                        "tenant-a",
                        DataRightsCaseType.StaffRights,
                        PropertyId: null,
                        candidate.Coordinate),
                    sink,
                    CancellationToken.None);

            Assert.Equal(
                DataRightsSubjectDiscoveryStatus.Succeeded,
                discovered.Status);
            Assert.Equal(member.Id, candidate.Coordinate.RecordId);
            Assert.Equal(
                DataRightsSubjectExportStatus.Succeeded,
                exported.Status);
            Assert.Equal(4, exported.RecordCount);
            DataRightsExportRecord profile = Assert.Single(
                sink.Records,
                record => record.RecordType == "staff-member");
            Assert.Equal(
                "maya.chen@example.test",
                Field(profile, "staff.work-email").GetString());
            DataRightsExportRecord assignment = Assert.Single(
                sink.Records,
                record =>
                    record.RecordType ==
                        "staff-property-assignment");
            Assert.Equal(
                propertyId,
                Field(assignment, "staff.property-id").GetGuid());
            DataRightsExportRecord governanceRecord = Assert.Single(
                sink.Records,
                record =>
                    record.RecordType ==
                        "staff-employment-governance");
            Assert.Equal(
                "operator-notice:1",
                Assert.Single(
                    Field(
                            governanceRecord,
                            "staff.governance.accepted-acknowledgements")
                        .EnumerateArray()).GetString());
            DataRightsExportRecord holdRecord = Assert.Single(
                sink.Records,
                record =>
                    record.RecordType == "staff-data-hold");
            Assert.Equal(
                StaffDataHoldReasonCodes.RegulatoryRequest,
                Field(
                        holdRecord,
                        "staff.data-hold.reason-code")
                    .GetString());
        }

        await AssertOperationLockSerializesAsync(provider, member.Id);
        await AssertCreationOperationLockSerializesAsync(provider);
        await AssertEvidenceBlocksDowngradeAsync(provider);
    }

    private static async Task AssertCreationOperationLockSerializesAsync(
        ServiceProvider provider)
    {
        Guid operationId = Guid.NewGuid();
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        StaffDbContext firstDb = firstScope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        StaffDbContext secondDb = secondScope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        IStaffCreationOperationLock firstLock = firstScope.ServiceProvider
            .GetRequiredService<IStaffCreationOperationLock>();
        IStaffCreationOperationLock secondLock = secondScope.ServiceProvider
            .GetRequiredService<IStaffCreationOperationLock>();
        IStaffMemberRepository firstMembers = firstScope.ServiceProvider
            .GetRequiredService<IStaffMemberRepository>();
        IStaffMemberRepository secondMembers = secondScope.ServiceProvider
            .GetRequiredService<IStaffMemberRepository>();

        await using var firstTransaction =
            await firstDb.Database.BeginTransactionAsync();
        await firstLock.AcquireAsync(
            "tenant-a",
            operationId,
            CancellationToken.None);
        Assert.Null(await firstMembers.GetForSafetyTransitionAsync(
            operationId,
            CancellationToken.None));
        StaffMember created = StaffMember.Create(
            operationId,
            "tenant-a",
            "Concurrent creation",
            legalName: null,
            "concurrent.creation@example.test",
            workPhone: null,
            "EMP-CREATE-RETRY",
            "Manager",
            "Operations",
            authSubjectId: null,
            "user:operator",
            Guid.NewGuid(),
            Now.AddMinutes(10)).Value;
        await firstMembers.AddAsync(created, CancellationToken.None);
        await firstDb.SaveChangesAsync();

        TaskCompletionSource secondStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<StaffMember?> secondAttempt = AcquireCreationAndReadAsync(
            secondDb,
            secondLock,
            secondMembers,
            operationId,
            secondStarted);
        await secondStarted.Task;
        Task winner = await Task.WhenAny(
            secondAttempt,
            Task.Delay(TimeSpan.FromMilliseconds(400)));
        Assert.NotSame(secondAttempt, winner);

        await firstTransaction.CommitAsync();
        StaffMember? replay = await secondAttempt.WaitAsync(
            TimeSpan.FromSeconds(10));
        Assert.NotNull(replay);
        Assert.Equal(operationId, replay.Id);

        using IServiceScope verificationScope = provider.CreateScope();
        StaffDbContext verification = verificationScope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        Assert.Equal(
            1,
            await verification.StaffMembers.CountAsync(
                candidate => candidate.Id == operationId));
        Assert.Equal(
            1,
            await verification.ProcessingRestrictionProjections.CountAsync(
                projection => projection.StaffMemberId == operationId));
        Assert.Equal(1, await GetOperationLockRevisionAsync(
            verification,
            operationId));
    }

    private static async Task<StaffMember?> AcquireCreationAndReadAsync(
        StaffDbContext dbContext,
        IStaffCreationOperationLock creationLock,
        IStaffMemberRepository members,
        Guid operationId,
        TaskCompletionSource started)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();
        started.SetResult();
        await creationLock.AcquireAsync(
            "tenant-a",
            operationId,
            CancellationToken.None);
        StaffMember? member = await members.GetAsync(
            operationId,
            CancellationToken.None);
        await transaction.CommitAsync();
        return member;
    }

    private static async Task AssertEvidenceBlocksDowngradeAsync(
        ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        StaffDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<StaffDbContext>();

        Exception? failure = await Record.ExceptionAsync(() =>
            dbContext.Database.GetService<IMigrator>()
                .MigrateAsync(FoundationPreviousMigration));

        Assert.NotNull(failure);
        Assert.Contains(
            "Cannot downgrade while Staff employment-governance " +
            "or data-hold records exist.",
            failure.ToString(),
            StringComparison.Ordinal);
    }

    private static async Task AssertOperationLockSerializesAsync(
        ServiceProvider provider,
        Guid staffMemberId)
    {
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        StaffDbContext firstDb = firstScope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        StaffDbContext secondDb = secondScope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        IStaffOperationLock firstLock = firstScope.ServiceProvider
            .GetRequiredService<IStaffOperationLock>();
        IStaffOperationLock secondLock = secondScope.ServiceProvider
            .GetRequiredService<IStaffOperationLock>();

        await using var firstTransaction =
            await firstDb.Database.BeginTransactionAsync();
        Assert.True(await firstLock.TryAcquireStaffMemberAsync(
            "tenant-a",
            staffMemberId,
            CancellationToken.None));

        TaskCompletionSource secondStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> secondAttempt = AcquireAndCommitAsync(
            secondDb,
            secondLock,
            staffMemberId,
            secondStarted);
        await secondStarted.Task;
        Task winner = await Task.WhenAny(
            secondAttempt,
            Task.Delay(TimeSpan.FromMilliseconds(400)));
        Assert.NotSame(secondAttempt, winner);

        await firstTransaction.CommitAsync();
        Assert.True(await secondAttempt.WaitAsync(
            TimeSpan.FromSeconds(10)));
        Assert.Equal(
            3,
            await GetOperationLockRevisionAsync(
                firstDb,
                staffMemberId));
    }

    private static async Task<bool> AcquireAndCommitAsync(
        StaffDbContext dbContext,
        IStaffOperationLock operationLock,
        Guid staffMemberId,
        TaskCompletionSource started)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();
        started.SetResult();
        bool acquired =
            await operationLock.TryAcquireStaffMemberAsync(
                "tenant-a",
                staffMemberId,
                CancellationToken.None);
        await transaction.CommitAsync();
        return acquired;
    }

    private static async Task<long> GetOperationLockRevisionAsync(
        StaffDbContext dbContext,
        Guid staffMemberId)
    {
        DbConnection connection =
            dbContext.Database.GetDbConnection();
        bool close = connection.State != ConnectionState.Open;
        if (close)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT "Revision"
                FROM "staff"."staff_operation_locks"
                WHERE "ScopeId" = @scope
                  AND "StaffMemberId" = @staffMemberId
                """;
            DbParameter scope = command.CreateParameter();
            scope.ParameterName = "scope";
            scope.Value = "tenant-a";
            command.Parameters.Add(scope);
            DbParameter staff = command.CreateParameter();
            staff.ParameterName = "staffMemberId";
            staff.Value = staffMemberId;
            command.Parameters.Add(staff);
            object? value = await command.ExecuteScalarAsync();
            return Assert.IsType<long>(value);
        }
        finally
        {
            if (close)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static ServiceProvider CreatePersistenceProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = connectionString;
        builder.Services.AddSingleton<IScopeContext>(new TestScopeContext("tenant-a"));
        builder.Services.AddSingleton<IWorkspaceTerminationFenceReader>(
            OpenWorkspaceTerminationFenceReader.Instance);
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

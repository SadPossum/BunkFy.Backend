namespace Integration.Tests;

using System.Data;
using System.Data.Common;
using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class WorkspacesDataRightsExportIntegrationTests
{
    private const string PreviousMigration =
        "20260730070545_AddWorkspaceStaffRetentionCorrelationScrub";
    private const string TenantA =
        "10000000-0000-0000-0000-000000000001";
    private const string TenantB =
        "10000000-0000-0000-0000-000000000002";
    private const string SubjectId = "account-subject-a";
    private static readonly Guid StaffMemberId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid PropertyA =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid PropertyB =
        Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Workspaces_discovery_and_export_use_authoritative_postgresql_records()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_workspaces_data_rights_tests")
                .Build();
        await postgreSql.StartAsync();

        using ServiceProvider tenantAProvider =
            CreatePersistenceProvider(
                postgreSql.GetConnectionString(),
                TenantA);
        SeededGraph graph;
        using (IServiceScope scope = tenantAProvider.CreateScope())
        {
            WorkspacesDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<WorkspacesDbContext>();
            await dbContext.Database.GetService<IMigrator>()
                .MigrateAsync(PreviousMigration);
            graph = SeedGraph(dbContext, TenantA, SubjectId);
            await dbContext.SaveChangesAsync();
        }

        using ServiceProvider tenantBProvider =
            CreatePersistenceProvider(
                postgreSql.GetConnectionString(),
                TenantB);
        using (IServiceScope scope = tenantBProvider.CreateScope())
        {
            WorkspacesDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<WorkspacesDbContext>();
            dbContext.StaffOnboardingApplications.Add(
                WorkspaceStaffOnboarding.Create(
                    Guid.Parse(
                        "50000000-0000-0000-0000-000000000099"),
                    TenantB,
                    WorkspaceStaffOnboardingSource.Invitation,
                    Guid.Parse(
                        "40000000-0000-0000-0000-000000000099"),
                    SubjectId,
                    "other@example.test",
                    "Other tenant",
                    legalName: null,
                    "other@example.test",
                    workPhone: null,
                    employeeNumber: null,
                    jobTitle: null,
                    department: null,
                    Now).Value);
            await dbContext.SaveChangesAsync();
        }

        using (IServiceScope scope = tenantAProvider.CreateScope())
        {
            WorkspacesDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<WorkspacesDbContext>();
            await dbContext.Database.MigrateAsync();
            await AssertPartialIndexAsync(dbContext);
            StaffDbContext staffDbContext = scope.ServiceProvider
                .GetRequiredService<StaffDbContext>();
            await staffDbContext.Database.MigrateAsync();
            staffDbContext.StaffMembers.Add(
                StaffMember.Create(
                    StaffMemberId,
                    TenantA,
                    "Artem",
                    "Artem Prokudanov",
                    "artem@example.test",
                    "+44 20 5555 0123",
                    "E-42",
                    "Manager",
                    "Operations",
                    SubjectId,
                    "user:owner",
                    Guid.Parse(
                        "91000000-0000-0000-0000-000000000001"),
                    Now).Value);
            await staffDbContext.SaveChangesAsync();

            IDataRightsSubjectDiscoveryContributor[] discoveries =
                scope.ServiceProvider.GetServices<
                        IDataRightsSubjectDiscoveryContributor>()
                    .Where(contributor =>
                        contributor.SupportedCaseTypes.Contains(
                            DataRightsCaseType.StaffRights))
                    .OrderBy(
                        contributor => contributor.OwnerKey,
                        StringComparer.Ordinal)
                    .ToArray();
            Assert.Equal(
                [
                    StaffDataRightsCoordinates.Owner,
                    WorkspacesDataRightsCoordinates.Owner
                ],
                discoveries
                    .Select(contributor => contributor.OwnerKey)
                    .ToArray());
            IDataRightsSubjectDiscoveryContributor discovery =
                discoveries
                    .Single(contributor =>
                        contributor.OwnerKey ==
                        WorkspacesDataRightsCoordinates.Owner);
            IDataRightsSubjectDiscoveryContributor staffDiscovery =
                discoveries
                    .Single(contributor =>
                        contributor.OwnerKey ==
                        StaffDataRightsCoordinates.Owner);
            IDataRightsSubjectExportContributor exporter =
                scope.ServiceProvider
                    .GetServices<IDataRightsSubjectExportContributor>()
                    .Single(contributor =>
                        contributor.OwnerKey ==
                        WorkspacesDataRightsCoordinates.Owner);

            DataRightsSubjectDiscoveryResult bySubject =
                await discovery.DiscoverAsync(
                    DiscoveryRequest(
                        TenantA,
                        AccountSubjectId: SubjectId),
                    CancellationToken.None);
            Assert.Equal(
                DataRightsSubjectDiscoveryStatus.Succeeded,
                bySubject.Status);
            Assert.Equal(
                [
                    WorkspacesDataRightsCoordinates
                        .StaffAccessPlanRecordType,
                    WorkspacesDataRightsCoordinates
                        .StaffAccessProcessRecordType,
                    WorkspacesDataRightsCoordinates
                        .StaffOnboardingRecordType
                ],
                bySubject.Candidates
                    .Select(candidate =>
                        candidate.Coordinate.RecordType)
                    .ToArray());
            DataRightsSubjectDiscoveryResult staffBySubject =
                await staffDiscovery.DiscoverAsync(
                    DiscoveryRequest(
                        TenantA,
                        AccountSubjectId: SubjectId),
                    CancellationToken.None);
            DataRightsSubjectCandidate staffCandidate =
                Assert.Single(staffBySubject.Candidates);
            Assert.Equal(
                StaffDataRightsCoordinates.StaffMemberRecordType,
                staffCandidate.Coordinate.RecordType);

            DataRightsSubjectDiscoveryResult byStaff =
                await discovery.DiscoverAsync(
                    DiscoveryRequest(
                        TenantA,
                        RecordId: StaffMemberId),
                    CancellationToken.None);
            Assert.Equal(
                [
                    WorkspacesDataRightsCoordinates
                        .StaffAccessProcessRecordType,
                    WorkspacesDataRightsCoordinates
                        .StaffOnboardingRecordType,
                    WorkspacesDataRightsCoordinates
                        .StaffRetentionCorrelationReceiptRecordType
                ],
                byStaff.Candidates
                    .Select(candidate =>
                        candidate.Coordinate.RecordType)
                    .ToArray());

            DataRightsSubjectDiscoveryResult crossTenant =
                await discovery.DiscoverAsync(
                    DiscoveryRequest(
                        TenantB,
                        AccountSubjectId: SubjectId),
                    CancellationToken.None);
            Assert.Equal(
                DataRightsSubjectDiscoveryStatus.ScopeUnavailable,
                crossTenant.Status);

            DataRightsSubjectCoordinate onboardingCoordinate =
                bySubject.Candidates.Single(candidate =>
                    candidate.Coordinate.RecordType ==
                    WorkspacesDataRightsCoordinates
                        .StaffOnboardingRecordType).Coordinate;
            DataRightsSubjectSelectionValidation validSelection =
                await discovery.ValidateSelectionAsync(
                    new(
                        TenantA,
                        DataRightsCaseType.StaffRights,
                        PropertyId: null,
                        onboardingCoordinate),
                    CancellationToken.None);
            DataRightsSubjectSelectionValidation staleSelection =
                await discovery.ValidateSelectionAsync(
                    new(
                        TenantA,
                        DataRightsCaseType.StaffRights,
                        PropertyId: null,
                        onboardingCoordinate with
                        {
                            RecordVersion =
                                onboardingCoordinate.RecordVersion + 1
                        }),
                    CancellationToken.None);
            Assert.Equal(
                DataRightsSubjectSelectionValidationStatus.Valid,
                validSelection.Status);
            Assert.Equal(
                DataRightsSubjectSelectionValidationStatus.Stale,
                staleSelection.Status);

            await AssertExportAsync(
                exporter,
                onboardingCoordinate,
                expectedCount: 1,
                expectedRecordTypes:
                [
                    WorkspacesDataRightsCoordinates
                        .StaffOnboardingRecordType
                ]);
            CollectingSink processSink = await AssertExportAsync(
                exporter,
                bySubject.Candidates.Single(candidate =>
                    candidate.Coordinate.RecordType ==
                    WorkspacesDataRightsCoordinates
                        .StaffAccessProcessRecordType).Coordinate,
                expectedCount: 3,
                expectedRecordTypes:
                [
                    WorkspacesDataRightsCoordinates
                        .StaffAccessProcessRecordType,
                    "staff-access-profile-snapshot",
                    "staff-access-profile-snapshot"
                ]);
            Assert.Equal(
                [PropertyA, PropertyB],
                processSink.Records
                    .Skip(1)
                    .Select(record =>
                        ParsePropertyId(
                            Field(
                                record,
                                "workspaces.assignment-scope")
                            .GetString()))
                    .ToArray());

            CollectingSink planSink = await AssertExportAsync(
                exporter,
                bySubject.Candidates.Single(candidate =>
                    candidate.Coordinate.RecordType ==
                    WorkspacesDataRightsCoordinates
                        .StaffAccessPlanRecordType).Coordinate,
                expectedCount: 3,
                expectedRecordTypes:
                [
                    WorkspacesDataRightsCoordinates
                        .StaffAccessPlanRecordType,
                    "staff-access-plan-property",
                    "staff-access-plan-property"
                ]);
            Assert.Equal(
                [PropertyA, PropertyB],
                planSink.Records
                    .Skip(1)
                    .Select(record =>
                        Field(
                            record,
                            "workspaces.property-assignment-id")
                        .GetGuid())
                    .ToArray());

            DataRightsSubjectCoordinate receiptCoordinate =
                byStaff.Candidates.Single(candidate =>
                    candidate.Coordinate.RecordType ==
                    WorkspacesDataRightsCoordinates
                        .StaffRetentionCorrelationReceiptRecordType)
                .Coordinate;
            CollectingSink receiptSink = await AssertExportAsync(
                exporter,
                receiptCoordinate,
                expectedCount: 1,
                expectedRecordTypes:
                [
                    WorkspacesDataRightsCoordinates
                        .StaffRetentionCorrelationReceiptRecordType
                ]);
            Assert.Equal(
                graph.Receipt.CanonicalSha256,
                Field(
                        receiptSink.Records[0],
                        "workspaces.retention-correlation-proof")
                    .GetProperty("canonicalSha256")
                    .GetString());

            CollectingSink replay =
                await AssertExportAsync(
                    exporter,
                    bySubject.Candidates.Single(candidate =>
                        candidate.Coordinate.RecordType ==
                        WorkspacesDataRightsCoordinates
                            .StaffAccessProcessRecordType).Coordinate,
                    expectedCount: 3,
                    expectedRecordTypes:
                    [
                        WorkspacesDataRightsCoordinates
                            .StaffAccessProcessRecordType,
                        "staff-access-profile-snapshot",
                        "staff-access-profile-snapshot"
                    ]);
            Assert.Equal(
                processSink.Records
                    .Select(record => record.RecordId)
                    .ToArray(),
                replay.Records
                    .Select(record => record.RecordId)
                    .ToArray());
        }
    }

    private static async Task<CollectingSink> AssertExportAsync(
        IDataRightsSubjectExportContributor exporter,
        DataRightsSubjectCoordinate coordinate,
        int expectedCount,
        string[] expectedRecordTypes)
    {
        CollectingSink sink = new();
        DataRightsSubjectExportResult result =
            await exporter.ExportAsync(
                new(
                    TenantA,
                    DataRightsCaseType.StaffRights,
                    PropertyId: null,
                    coordinate),
                sink,
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectExportStatus.Succeeded,
            result.Status);
        Assert.Equal(expectedCount, result.RecordCount);
        Assert.Equal(
            expectedRecordTypes,
            sink.Records
                .Select(record => record.RecordType)
                .ToArray());
        return sink;
    }

    private static async Task AssertPartialIndexAsync(
        WorkspacesDbContext dbContext)
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
                SELECT indexdef
                FROM pg_indexes
                WHERE schemaname = 'workspaces'
                  AND indexname =
                    'IX_staff_onboarding_applications_ScopeId_StaffMemberId_Id'
                """;
            string definition = Assert.IsType<string>(
                await command.ExecuteScalarAsync());
            Assert.Contains(
                "WHERE (\"StaffMemberId\" IS NOT NULL)",
                definition,
                StringComparison.Ordinal);
        }
        finally
        {
            if (close)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static SeededGraph SeedGraph(
        WorkspacesDbContext dbContext,
        string tenantId,
        string subjectId)
    {
        Guid sourceId =
            Guid.Parse("40000000-0000-0000-0000-000000000001");
        WorkspaceStaffOnboarding onboarding =
            WorkspaceStaffOnboarding.Create(
                Guid.Parse(
                    "50000000-0000-0000-0000-000000000001"),
                tenantId,
                WorkspaceStaffOnboardingSource.Invitation,
                sourceId,
                subjectId,
                "artem@example.test",
                "Artem",
                "Artem Prokudanov",
                "artem@example.test",
                "+44 20 5555 0123",
                "E-42",
                "Manager",
                "Operations",
                Now).Value;
        Assert.True(
            onboarding.ObserveInvitationAccepted(
                Now.AddMinutes(1)).IsSuccess);
        Assert.True(
            onboarding.MarkStaffReady(
                StaffMemberId,
                Now.AddMinutes(2)).IsSuccess);
        Assert.True(
            onboarding.Complete(
                Now.AddMinutes(3)).IsSuccess);

        WorkspaceStaffAccessProcess process =
            WorkspaceStaffAccessProcess.Create(
                Guid.Parse(
                    "60000000-0000-0000-0000-000000000001"),
                tenantId,
                StaffMemberId,
                subjectId,
                WorkspaceStaffAccessTargetState.Departed,
                targetStaffVersion: 2,
                new DateOnly(2026, 7, 30),
                subjectId,
                [
                    new WorkspaceStaffAccessProfileTarget(
                        Guid.Parse(
                            "70000000-0000-0000-0000-000000000002"),
                        $"property:{PropertyB:N}"),
                    new WorkspaceStaffAccessProfileTarget(
                        Guid.Parse(
                            "70000000-0000-0000-0000-000000000001"),
                        $"property:{PropertyA:N}")
                ],
                Now).Value;
        Assert.True(
            process.MarkAwaitingStaffCommit(
                Now.AddMinutes(1)).IsSuccess);
        Assert.True(
            process.ObserveStaffCommit(
                Now.AddMinutes(2)).IsSuccess);

        WorkspaceStaffAccessPlan plan =
            WorkspaceStaffAccessPlan.Create(
                sourceId,
                tenantId,
                WorkspaceStaffOnboardingSource.Invitation,
                Guid.Parse(
                    "70000000-0000-0000-0000-000000000003"),
                "workspace-manager",
                [PropertyB, PropertyA],
                subjectId,
                Now).Value;
        Assert.True(plan.Activate(Now.AddMinutes(1)).IsSuccess);

        WorkspaceStaffRetentionCorrelationReceipt receipt =
            WorkspaceStaffRetentionCorrelationReceipt.Create(
                Guid.Parse(
                    "80000000-0000-0000-0000-000000000001"),
                tenantId,
                Guid.Parse(
                    "90000000-0000-0000-0000-000000000001"),
                StaffMemberId,
                selectedStaffVersion: 2,
                onboardingRecordsScrubbed: 1,
                accessProcessRecordsScrubbed: 1,
                accessPlanRecordsScrubbed: 1,
                Now.AddDays(1)).Value;

        dbContext.StaffOnboardingApplications.Add(onboarding);
        dbContext.StaffAccessProcesses.Add(process);
        dbContext.StaffAccessPlans.Add(plan);
        dbContext.StaffRetentionCorrelationReceipts.Add(receipt);
        return new SeededGraph(receipt);
    }

    private static DataRightsSubjectDiscoveryRequest DiscoveryRequest(
        string tenantId,
        Guid? RecordId = null,
        string? AccountSubjectId = null) =>
        new(
            tenantId,
            DataRightsCaseType.StaffRights,
            PropertyId: null,
            new DataRightsSubjectLookup(
                RecordId,
                Email: null,
                Phone: null,
                Name: null,
                DateOfBirth: null,
                AccountSubjectId),
            DataRightsSubjectDiscoveryLimits.MaxCandidates);

    private static Guid ParsePropertyId(string? assignmentScope)
    {
        Assert.NotNull(assignmentScope);
        Assert.StartsWith(
            "property:",
            assignmentScope,
            StringComparison.Ordinal);
        return Guid.ParseExact(
            assignmentScope["property:".Length..],
            "N");
    }

    private static JsonElement Field(
        DataRightsExportRecord record,
        string fieldId) =>
        Assert.Single(
            record.Fields,
            field => field.FieldId == fieldId).Value;

    private static ServiceProvider CreatePersistenceProvider(
        string connectionString,
        string tenantId)
    {
        HostApplicationBuilder builder =
            Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] =
            "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            connectionString;
        builder.Services.AddSingleton<IScopeContext>(
            new TestScopeContext(tenantId));
        builder.AddStaffPersistence();
        builder.AddWorkspacesPersistence();
        return builder.Services.BuildServiceProvider();
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

    private sealed class TestScopeContext(string scopeId)
        : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }

    private sealed record SeededGraph(
        WorkspaceStaffRetentionCorrelationReceipt Receipt);
}

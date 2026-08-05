namespace Integration.Tests.DataRights;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class
    DataRightsResponseDeadlineAlertPersistenceIntegrationTests
{
    private const string TenantId = "tenant-a";
    private const string PreviousMigration =
        "20260805034818_AddGuestRightsResponseDeadlinePolicy";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
    private const string DeadlineOutboxEventType =
        "BunkFy.Modules.DataRights.Contracts." +
        "DataRightsResponseDeadlineAlertDueIntegrationEvent";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Latest_migration_dispatches_atomically_and_deduplicates_current_state()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_data_rights_deadline_alert_tests")
                .Build();
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();
        Assert.Equal(
            DeadlineOutboxEventType,
            typeof(DataRightsResponseDeadlineAlertDueIntegrationEvent)
                .FullName);

        Guid propertyId = Guid.Parse(
            "21000000-0000-0000-0000-000000000010");
        Guid dueSoonCaseId = Guid.Parse(
            "11000000-0000-0000-0000-000000000010");
        Guid overdueCaseId = Guid.Parse(
            "11000000-0000-0000-0000-000000000020");
        Guid canceledCaseId = Guid.Parse(
            "11000000-0000-0000-0000-000000000030");
        DateTimeOffset dueSoonAtUtc = Now.AddHours(24);
        DateTimeOffset overdueAtUtc = Now.AddHours(-1);

        await using (DataRightsDbContext migration = CreateDbContext(
                         connectionString))
        {
            await migration.Database.MigrateAsync();
            Assert.Empty(
                await migration.Database.GetPendingMigrationsAsync());
        }

        await using (DataRightsDbContext seed = CreateDbContext(
                         connectionString))
        {
            DataRightsCase canceled = CreateCase(
                canceledCaseId,
                propertyId,
                Now.AddDays(-1),
                Now.AddHours(12));
            Assert.True(canceled.Cancel(
                canceled.Version,
                "staff:privacy",
                Now.AddMinutes(-1)).IsSuccess);
            seed.Cases.AddRange(
                CreateCase(
                    dueSoonCaseId,
                    propertyId,
                    Now.AddDays(-1),
                    dueSoonAtUtc),
                CreateCase(
                    overdueCaseId,
                    propertyId,
                    Now.AddDays(-5),
                    overdueAtUtc),
                canceled);
            await seed.SaveChangesAsync();
            await InstallOutboxRejectionAsync(seed);
        }

        var clock = new MutableClock(Now);
        using ServiceProvider provider = CreateProvider(
            connectionString,
            clock);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            DispatchAsync(provider));
        await using (DataRightsDbContext rolledBack = CreateDbContext(
                         connectionString))
        {
            Assert.Empty(
                await rolledBack.ResponseDeadlineAlertDispatches.ToArrayAsync());
            Assert.Empty(await DeadlineOutboxMessages(rolledBack).ToArrayAsync());
            await RemoveOutboxRejectionAsync(rolledBack);
        }

        Result<DataRightsResponseDeadlineAlertDispatchBatchResult> first =
            await DispatchAsync(provider);
        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Equal(2, first.Value.ProcessedCount);
        Assert.Equal(2, first.Value.DispatchedCount);

        Result<DataRightsResponseDeadlineAlertDispatchBatchResult> replay =
            await DispatchAsync(provider);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(0, replay.Value.ProcessedCount);

        clock.UtcNow = Now.AddHours(25);
        Result<DataRightsResponseDeadlineAlertDispatchBatchResult> transition =
            await DispatchAsync(provider);
        Assert.True(transition.IsSuccess, transition.Error.Code);
        Assert.Equal(1, transition.Value.DispatchedCount);

        await using (DataRightsDbContext reader = CreateDbContext(
                         connectionString))
        {
            DataRightsResponseDeadlineAlertDispatchReceipt[] receipts =
                await reader.ResponseDeadlineAlertDispatches
                    .OrderBy(item => item.DispatchedAtUtc)
                    .ThenBy(item => item.CaseId)
                    .ToArrayAsync();
            OutboxMessage[] messages = await DeadlineOutboxMessages(reader)
                .OrderBy(item => item.CreatedAtUtc)
                .ThenBy(item => item.Id)
                .ToArrayAsync();

            Assert.Equal(3, receipts.Length);
            Assert.Equal(3, messages.Length);
            Assert.All(receipts, receipt =>
            {
                Assert.Contains(messages, message => message.Id == receipt.Id);
                Assert.DoesNotContain(
                    canceledCaseId.ToString("D"),
                    messages.Single(message => message.Id == receipt.Id).Payload,
                    StringComparison.OrdinalIgnoreCase);
            });
            Assert.Collection(
                receipts.Where(item => item.CaseId == dueSoonCaseId)
                    .OrderBy(item => item.AlertKind),
                receipt => Assert.Equal(
                    DataRightsResponseDeadlineAlertKind.DueSoon,
                    receipt.AlertKind),
                receipt => Assert.Equal(
                    DataRightsResponseDeadlineAlertKind.Overdue,
                    receipt.AlertKind));
            Assert.Single(
                receipts,
                item => item.CaseId == overdueCaseId &&
                    item.AlertKind ==
                        DataRightsResponseDeadlineAlertKind.Overdue);
            Assert.DoesNotContain(
                receipts,
                item => item.CaseId == canceledCaseId);

            PostgresException unsafeDowngrade =
                await Assert.ThrowsAsync<PostgresException>(() =>
                    reader.Database.GetService<IMigrator>()
                        .MigrateAsync(PreviousMigration));
            Assert.Equal(
                PostgresErrorCodes.RaiseException,
                unsafeDowngrade.SqlState);
            Assert.Contains(
                "Cannot downgrade Data Rights while response-deadline alert dispatch receipts exist",
                unsafeDowngrade.MessageText,
                StringComparison.Ordinal);
        }
    }

    private static async Task<
        Result<DataRightsResponseDeadlineAlertDispatchBatchResult>>
        DispatchAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(
            new DispatchDataRightsResponseDeadlineAlertsCommand(100),
            CancellationToken.None);
    }

    private static IQueryable<OutboxMessage> DeadlineOutboxMessages(
        DataRightsDbContext dbContext) => dbContext.OutboxMessages.Where(
        item => item.EventType == DeadlineOutboxEventType);

    private static async Task InstallOutboxRejectionAsync(
        DataRightsDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE OR REPLACE FUNCTION "data-rights".reject_deadline_alert_outbox()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                IF NEW."EventType" = 'BunkFy.Modules.DataRights.Contracts.DataRightsResponseDeadlineAlertDueIntegrationEvent' THEN
                    RAISE EXCEPTION 'deadline alert outbox rejected for atomicity test';
                END IF;
                RETURN NEW;
            END;
            $function$;

            CREATE TRIGGER reject_deadline_alert_outbox
            BEFORE INSERT ON "data-rights"."outbox_messages"
            FOR EACH ROW
            EXECUTE FUNCTION "data-rights".reject_deadline_alert_outbox();
            """);
    }

    private static async Task RemoveOutboxRejectionAsync(
        DataRightsDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DROP TRIGGER IF EXISTS reject_deadline_alert_outbox
                ON "data-rights"."outbox_messages";
            DROP FUNCTION IF EXISTS
                "data-rights".reject_deadline_alert_outbox();
            """);
    }

    private static ServiceProvider CreateProvider(
        string connectionString,
        MutableClock clock)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Development
            });
        builder.Configuration["ApplicationIdentity:Namespace"] =
            "bunkfy-deadline-alert-test";
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            connectionString;

        builder.Services.AddSingleton<IScopeContext>(
            new TestScopeContext());
        builder.Services.AddSingleton<ISystemClock>(clock);
        builder.AddApplicationEventsInfrastructure();
        builder.AddMessagingInfrastructure();
        builder.AddTenancyInfrastructure();
        builder.AddTenantAwareMessaging();
        builder.AddCqrsInfrastructure();
        builder.Services.AddDataRightsApplication();
        builder.AddDataRightsPersistence();
        builder.Services.AddSingleton<IIdGenerator, TestIdGenerator>();

        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
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
        return new(options, new TestScopeContext());
    }

    private static DataRightsCase CreateCase(
        Guid caseId,
        Guid propertyId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset dueAtUtc)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.DataSubject).Value;
        DataRightsResponseDeadlinePolicyEvidence evidence =
            DataRightsResponseDeadlinePolicyEvidence.Create(
                propertyId,
                propertyTopologySourceVersion: 7,
                propertyPolicySourceVersion: 8,
                "GB",
                "integration-hostel-policy",
                policyVersion: 2,
                new string('a', 64),
                DataRightsResponseRight.Export,
                "rights-response-access-export",
                periodYears: 0,
                periodMonths: 0,
                periodDays: 2,
                "Europe/London",
                new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
                createdAtUtc,
                createdAtUtc,
                dueAtUtc).Value;
        return DataRightsCase.Create(
            caseId,
            TenantId,
            request,
            "staff:privacy",
            createdAtUtc,
            evidence).Value;
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }
}

namespace Integration.Tests;

using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class StaffProfileUpdateOperationIntegrationTests
{
    private const string TenantId =
        "a9000000-0000-0000-0000-000000000001";
    private const string OtherTenantId =
        "a9000000-0000-0000-0000-000000000002";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Profile_update_receipt_is_atomic_replayable_scoped_and_immutable()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_staff_profile_update_operation_tests")
                .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);

        MutableScopeContext scopeContext = new(TenantId);
        await using ServiceProvider services = CreateProvider(
            postgreSql.GetConnectionString(),
            scopeContext);
        await MigrateAsync(services).ConfigureAwait(false);

        Guid staffMemberId = Guid.NewGuid();
        Result<StaffDirectoryMemberDto> created = await SendAsync(
            services,
            new CreateStaffMemberCommand(
                staffMemberId,
                "Maya Chen",
                "Maya Q. Chen",
                "maya@example.test",
                "+44 20 1234 5678",
                "EMP-42",
                "Manager",
                "Operations",
                "account-maya",
                "user:operator")).ConfigureAwait(false);
        Assert.True(created.IsSuccess, created.Error.Code);

        Guid operationId = Guid.NewGuid();
        UpdateStaffMemberCommand update = new(
            operationId,
            staffMemberId,
            "Maya Chen Updated",
            "Maya Q. Chen",
            "MAYA@EXAMPLE.TEST",
            "+44 20 1234 5678",
            "EMP-42",
            "Operations Lead",
            "Operations",
            created.Value.Version,
            "user:operator");
        Result<StaffProfileMutationReceiptDto>[] concurrent =
            await Task.WhenAll(
                SendAsync(services, update),
                SendAsync(services, update)).ConfigureAwait(false);

        Assert.All(
            concurrent,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(concurrent[0].Value, concurrent[1].Value);
        StaffProfileMutationReceiptDto receipt = concurrent[0].Value;
        Assert.Equal(created.Value.Version + 1, receipt.Version);

        Result<StaffProfileMutationReceiptDto> conflictingReuse =
            await SendAsync(
                services,
                update with { DisplayName = "Different reuse" })
                .ConfigureAwait(false);
        Assert.Equal(
            StaffApplicationErrors.ProfileUpdateOperationConflict,
            conflictingReuse.Error);

        Result<StaffProfileMutationReceiptDto> stale = await SendAsync(
            services,
            update with
            {
                OperationId = Guid.NewGuid(),
                DisplayName = "Stale update"
            }).ConfigureAwait(false);
        Assert.Equal(StaffApplicationErrors.VersionConflict, stale.Error);

        await VerifyOwnerStateAsync(
            services,
            staffMemberId,
            operationId,
            receipt).ConfigureAwait(false);

        scopeContext.ScopeId = OtherTenantId;
        using (IServiceScope isolatedScope = services.CreateScope())
        {
            StaffDbContext isolated = isolatedScope.ServiceProvider
                .GetRequiredService<StaffDbContext>();
            IStaffProfileUpdateOperationRepository operations =
                isolatedScope.ServiceProvider.GetRequiredService<
                    IStaffProfileUpdateOperationRepository>();
            Assert.False(await isolated.StaffMembers.AnyAsync()
                .ConfigureAwait(false));
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                operationId,
                CancellationToken.None).ConfigureAwait(false));
        }

        scopeContext.ScopeId = TenantId;
        await AssertDirectUpdateRejectedAsync(
            postgreSql.GetConnectionString(),
            staffMemberId,
            operationId).ConfigureAwait(false);
        await AssertFailedOutboxWriteRollsBackAsync(
            services,
            postgreSql.GetConnectionString(),
            staffMemberId,
            receipt.Version).ConfigureAwait(false);
        await DeleteAndVerifyAsync(
            services,
            staffMemberId,
            operationId).ConfigureAwait(false);
    }

    private static async Task VerifyOwnerStateAsync(
        ServiceProvider services,
        Guid staffMemberId,
        Guid operationId,
        StaffProfileMutationReceiptDto receipt)
    {
        using IServiceScope scope = services.CreateScope();
        StaffDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        StaffMember member = await dbContext.StaffMembers
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == staffMemberId)
            .ConfigureAwait(false);
        StaffProfileUpdateOperationRecord operation = Assert.IsType<
            StaffProfileUpdateOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffProfileUpdateOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    operationId,
                    CancellationToken.None)
                .ConfigureAwait(false));

        Assert.Equal("Maya Chen Updated", member.DisplayName);
        Assert.Equal(receipt.Version, member.Version);
        Assert.Equal(receipt, operation.ToReceipt());
        Assert.Single(
            dbContext.OutboxMessages,
            message => message.EventType.Contains(
                nameof(StaffMemberUpdatedIntegrationEvent),
                StringComparison.Ordinal));
    }

    private static async Task AssertDirectUpdateRejectedAsync(
        string connectionString,
        Guid staffMemberId,
        Guid operationId)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            UPDATE "staff"."profile_update_operations"
            SET "ResultVersion" = "ResultVersion" + 1
            WHERE "ScopeId" = @scope
              AND "StaffMemberId" = @staffMemberId
              AND "Id" = @operationId
            """,
            connection);
        command.Parameters.AddWithValue("scope", TenantId);
        command.Parameters.AddWithValue("staffMemberId", staffMemberId);
        command.Parameters.AddWithValue("operationId", operationId);

        PostgresException exception = await Assert.ThrowsAsync<
            PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
        Assert.Contains(
            "immutable",
            exception.MessageText,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertFailedOutboxWriteRollsBackAsync(
        ServiceProvider services,
        string connectionString,
        Guid staffMemberId,
        long expectedVersion)
    {
        const string functionName =
            "staff.fail_profile_update_outbox_insert";
        await ExecuteSqlAsync(
            connectionString,
            $$"""
            CREATE FUNCTION {{functionName}}()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'Profile update outbox failure';
            END;
            $function$;

            CREATE TRIGGER "TR_staff_profile_update_outbox_failure"
            BEFORE INSERT ON "staff"."outbox_messages"
            FOR EACH ROW
            EXECUTE FUNCTION {{functionName}}();
            """).ConfigureAwait(false);

        Guid failedOperationId = Guid.NewGuid();
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => SendAsync(
                services,
                new UpdateStaffMemberCommand(
                    failedOperationId,
                    staffMemberId,
                    "Must roll back",
                    "Maya Q. Chen",
                    "maya@example.test",
                    "+44 20 1234 5678",
                    "EMP-42",
                    "Operations Lead",
                    "Operations",
                    expectedVersion,
                    "user:operator")));
        }
        finally
        {
            await ExecuteSqlAsync(
                connectionString,
                $$"""
                DROP TRIGGER IF EXISTS
                    "TR_staff_profile_update_outbox_failure"
                    ON "staff"."outbox_messages";
                DROP FUNCTION IF EXISTS {{functionName}}();
                """).ConfigureAwait(false);
        }

        using IServiceScope scope = services.CreateScope();
        StaffMember persisted = await scope.ServiceProvider
            .GetRequiredService<StaffDbContext>()
            .StaffMembers.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == staffMemberId)
            .ConfigureAwait(false);
        StaffProfileUpdateOperationRecord? operation =
            await scope.ServiceProvider.GetRequiredService<
                    IStaffProfileUpdateOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    failedOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false);

        Assert.Equal("Maya Chen Updated", persisted.DisplayName);
        Assert.Equal(expectedVersion, persisted.Version);
        Assert.Null(operation);
    }

    private static async Task DeleteAndVerifyAsync(
        ServiceProvider services,
        Guid staffMemberId,
        Guid operationId)
    {
        using IServiceScope scope = services.CreateScope();
        IStaffProfileUpdateOperationRepository operations =
            scope.ServiceProvider.GetRequiredService<
                IStaffProfileUpdateOperationRepository>();
        await operations.DeleteForStaffMemberAsync(
            staffMemberId,
            CancellationToken.None).ConfigureAwait(false);

        Assert.Null(await operations.GetAsync(
            staffMemberId,
            operationId,
            CancellationToken.None).ConfigureAwait(false));
    }

    private static async Task<Result<TResponse>> SendAsync<TResponse>(
        ServiceProvider services,
        ICommand<TResponse> command)
    {
        using IServiceScope scope = services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>()
            .SendAsync(command, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task MigrateAsync(ServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StaffDbContext>()
            .Database.MigrateAsync()
            .ConfigureAwait(false);
    }

    private static async Task ExecuteSqlAsync(
        string connectionString,
        string commandText)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static ServiceProvider CreateProvider(
        string connectionString,
        MutableScopeContext scopeContext)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            connectionString;
        builder.Services.AddSingleton<IScopeContext>(scopeContext);
        builder.Services.AddSingleton<IWorkspaceTerminationFenceReader>(
            OpenWorkspaceTerminationFenceReader.Instance);
        builder.AddApplicationEventsInfrastructure();
        builder.AddCqrsInfrastructure();
        builder.AddMessagingInfrastructure();
        builder.Services.AddStaffApplication();
        builder.AddStaffPersistence();
        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class MutableScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId { get; set; } = scopeId;
    }
}

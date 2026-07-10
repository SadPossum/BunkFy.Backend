namespace Integration.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Tenancy;
using Gma.Modules.Auth.Contracts;
using Integration.Tests.Support;
using Inventory.Contracts;
using Inventory.Domain.Aggregates;
using Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Properties.Contracts;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class InventoryAuthorizationIntegrationTests
{
    private const string TenantA = "tenant-inventory-a";
    private const string TenantHeader = "X-Tenant-Id";
    private static readonly Guid PropertyA = Guid.Parse("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid PropertyB = Guid.Parse("10000000-0000-0000-0000-00000000000b");
    private static readonly Guid RoomA = Guid.Parse("20000000-0000-0000-0000-00000000000a");
    private static readonly Guid RoomB = Guid.Parse("20000000-0000-0000-0000-00000000000b");
    private static readonly Guid BedB = Guid.Parse("30000000-0000-0000-0000-00000000000b");
    private static readonly Guid PropertyC = Guid.Parse("10000000-0000-0000-0000-00000000000c");
    private static readonly Guid RoomC = Guid.Parse("20000000-0000-0000-0000-00000000000c");
    private static readonly Guid BedC = Guid.Parse("30000000-0000-0000-0000-00000000000c");

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Real_tokens_and_persisted_grants_protect_manual_inventory_availability()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();

        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_inventory_authorization_tests")
            .Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            AuthTestContainers.GetNatsConnectionString(nats));
        await api.MigrateInventoryAuthorizationDatabaseAsync().ConfigureAwait(false);
        await SeedInventoryAsync(api).ConfigureAwait(false);

        await using AdminCliTestApplication admin = new("PostgreSql", connectionString);
        await admin.MigrateAsync().ConfigureAwait(false);
        using HttpClient client = api.CreateClient();

        using (HttpResponseMessage unauthenticated = await SendAsync(
                   client,
                   HttpMethod.Get,
                   $"/api/inventory/properties/{PropertyA:D}/rooms").ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Unauthorized, unauthenticated).ConfigureAwait(false);
        }

        AuthTokensResponse operatorTokens = await AuthApiClient.RegisterAsync(
            client,
            TenantA,
            "operator@inventory.test").ConfigureAwait(false);
        Guid operatorId = GetSubjectId(operatorTokens.AccessToken);

        using (HttpResponseMessage noGrant = await SendAsync(
                   client,
                   HttpMethod.Get,
                   $"/api/inventory/properties/{PropertyA:D}/rooms",
                   operatorTokens.AccessToken).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Forbidden, noGrant).ConfigureAwait(false);
        }

        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "bootstrap", "--actor", "owner", "--yes"));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "create",
            "--actor", "owner",
            "--name", "inventory-operator"));
        foreach (string permission in new[]
                 {
                     InventoryAdminPermissionCodes.Read,
                     InventoryAdminPermissionCodes.Configure,
                     InventoryAdminPermissionCodes.BlocksManage
                 })
        {
            await AssertAdminSuccessAsync(admin.ExecuteAsync(
                "admin", "roles", "grant",
                "--actor", "owner",
                "--role", "inventory-operator",
                "--permission", permission));
        }

        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "assign",
            "--actor", "owner",
            "--target-kind", "user",
            "--target-id", operatorId.ToString("D"),
            "--role", "inventory-operator",
            "--scope", $"tenant:{TenantA}/property:{PropertyA:D}"));

        using (HttpResponseMessage roomsResponse = await SendAsync(
                   client,
                   HttpMethod.Get,
                   $"/api/inventory/properties/{PropertyA:D}/rooms",
                   operatorTokens.AccessToken).ConfigureAwait(false))
        {
            RoomInventoryListResponse rooms = await ReadSuccessAsync<RoomInventoryListResponse>(roomsResponse).ConfigureAwait(false);
            RoomInventoryDto room = Assert.Single(rooms.Rooms);
            Assert.Equal(InventorySalesMode.Unconfigured, room.SalesMode);
            Assert.False(Assert.Single(room.Units).IsSellable);
        }

        using (HttpResponseMessage configureBedWithoutBeds = await SendAsync(
                   client,
                   HttpMethod.Put,
                   $"/api/inventory/properties/{PropertyA:D}/rooms/{RoomA:D}/sales-mode",
                   operatorTokens.AccessToken,
                   new { salesMode = InventorySalesMode.BedLevel, expectedVersion = 1 }).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Conflict, configureBedWithoutBeds).ConfigureAwait(false);
        }

        using (HttpResponseMessage configureRoom = await SendAsync(
                   client,
                   HttpMethod.Put,
                   $"/api/inventory/properties/{PropertyA:D}/rooms/{RoomA:D}/sales-mode",
                   operatorTokens.AccessToken,
                   new { salesMode = InventorySalesMode.RoomLevel, expectedVersion = 1 }).ConfigureAwait(false))
        {
            RoomInventoryDto room = await ReadSuccessAsync<RoomInventoryDto>(configureRoom).ConfigureAwait(false);
            Assert.Equal(InventorySalesMode.RoomLevel, room.SalesMode);
            Assert.Equal(2, room.Version);
            Assert.True(Assert.Single(room.Units).IsSellable);
        }

        await ExerciseAllocationAuthorityAsync(api).ConfigureAwait(false);

        ManualInventoryBlockDto block;
        using (HttpResponseMessage createBlock = await SendAsync(
                   client,
                   HttpMethod.Post,
                   $"/api/inventory/properties/{PropertyA:D}/blocks",
                   operatorTokens.AccessToken,
                   new
                   {
                       inventoryUnitId = RoomA,
                       arrival = "2026-08-01",
                       departure = "2026-08-03",
                       reason = "Maintenance"
                   }).ConfigureAwait(false))
        {
            block = await ReadSuccessAsync<ManualInventoryBlockDto>(createBlock).ConfigureAwait(false);
            Assert.Equal(1, block.Version);
            Assert.Equal(ManualInventoryBlockStatus.Active, block.Status);
        }

        InventoryAvailabilityResponse blocked = await GetAvailabilityAsync(
            client,
            operatorTokens.AccessToken,
            "2026-08-02",
            "2026-08-03").ConfigureAwait(false);
        InventoryUnitAvailabilityDto blockedUnit = Assert.Single(blocked.Units);
        Assert.False(blockedUnit.IsAvailable);
        Assert.Equal(block.BlockId, Assert.Single(blockedUnit.ActiveBlockIds));

        InventoryAvailabilityResponse adjacent = await GetAvailabilityAsync(
            client,
            operatorTokens.AccessToken,
            "2026-08-03",
            "2026-08-04").ConfigureAwait(false);
        Assert.True(Assert.Single(adjacent.Units).IsAvailable);

        using (HttpResponseMessage overlap = await SendAsync(
                   client,
                   HttpMethod.Post,
                   $"/api/inventory/properties/{PropertyA:D}/blocks",
                   operatorTokens.AccessToken,
                   new
                   {
                       inventoryUnitId = RoomA,
                       arrival = "2026-08-02",
                       departure = "2026-08-04",
                       reason = "Duplicate"
                   }).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Conflict, overlap).ConfigureAwait(false);
        }

        using (HttpResponseMessage otherProperty = await SendAsync(
                   client,
                   HttpMethod.Get,
                   $"/api/inventory/properties/{PropertyB:D}/rooms",
                   operatorTokens.AccessToken).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Forbidden, otherProperty).ConfigureAwait(false);
        }

        using (HttpResponseMessage staleRelease = await SendAsync(
                   client,
                   HttpMethod.Post,
                   $"/api/inventory/properties/{PropertyA:D}/blocks/{block.BlockId:D}/release",
                   operatorTokens.AccessToken,
                   new { expectedVersion = 99 }).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Conflict, staleRelease).ConfigureAwait(false);
        }

        using (HttpResponseMessage release = await SendAsync(
                   client,
                   HttpMethod.Post,
                   $"/api/inventory/properties/{PropertyA:D}/blocks/{block.BlockId:D}/release",
                   operatorTokens.AccessToken,
                   new { expectedVersion = 1 }).ConfigureAwait(false))
        {
            ManualInventoryBlockDto released = await ReadSuccessAsync<ManualInventoryBlockDto>(release).ConfigureAwait(false);
            Assert.Equal(ManualInventoryBlockStatus.Released, released.Status);
            Assert.Equal(2, released.Version);
        }

        InventoryAvailabilityResponse available = await GetAvailabilityAsync(
            client,
            operatorTokens.AccessToken,
            "2026-08-02",
            "2026-08-03").ConfigureAwait(false);
        Assert.True(Assert.Single(available.Units).IsAvailable);

        using IServiceScope verificationScope = api.Services.CreateScope();
        ITenantContextAccessor tenantContext = verificationScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        tenantContext.SetTenant(TenantA);
        InventoryDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var blockMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.EventType == typeof(ManualInventoryBlockCreatedIntegrationEvent).FullName ||
                message.EventType == typeof(ManualInventoryBlockReleasedIntegrationEvent).FullName)
            .OrderBy(message => message.OccurredAtUtc)
            .ToListAsync()
            .ConfigureAwait(false);
        Assert.Equal(2, blockMessages.Count);
        Assert.All(blockMessages, message => Assert.Equal(TenantA, message.ScopeId));

        IInventoryAvailabilityProjectionExportSource exportSource = verificationScope.ServiceProvider
            .GetRequiredService<IInventoryAvailabilityProjectionExportSource>();
        ProjectionReadBatch<InventoryAvailabilityProjectionExport> export = await exportSource.ReadAsync(
            new ProjectionRebuildRequest("reservations-availability", projectionVersion: 1, batchSize: 10),
            cursor: null,
            CancellationToken.None).ConfigureAwait(false);
        InventoryAvailabilityProjectionExport propertyExport = Assert.Single(
            export.Snapshots,
            snapshot => snapshot.PropertyId == PropertyA);
        InventoryUnitProjectionExport unitExport = Assert.Single(propertyExport.Units);
        Assert.True(unitExport.IsSellable);
        Assert.Equal(ManualInventoryBlockStatus.Released, Assert.Single(unitExport.Blocks).Status);
    }

    private static async Task SeedInventoryAsync(AuthTestApplication api)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantA);
        InventoryDbContext dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IIntegrationEventHandler<PropertyCreatedIntegrationEvent> propertyHandler =
            ResolveInventoryHandler<PropertyCreatedIntegrationEvent>(scope.ServiceProvider);
        IIntegrationEventHandler<RoomCreatedIntegrationEvent> roomHandler =
            ResolveInventoryHandler<RoomCreatedIntegrationEvent>(scope.ServiceProvider);
        IIntegrationEventHandler<BedAddedIntegrationEvent> bedHandler =
            ResolveInventoryHandler<BedAddedIntegrationEvent>(scope.ServiceProvider);
        await propertyHandler.HandleAsync(
            new(Guid.NewGuid(), TenantA, now, PropertyA, "Alpha House", "alpha", "UTC", PropertyStatus.Active, 1),
            CancellationToken.None).ConfigureAwait(false);
        await roomHandler.HandleAsync(
            new(Guid.NewGuid(), TenantA, now, PropertyA, RoomA, "101", "Main", "1", RoomStatus.Active, 1),
            CancellationToken.None).ConfigureAwait(false);
        await propertyHandler.HandleAsync(
            new(Guid.NewGuid(), TenantA, now, PropertyC, "Out of Order House", "out-of-order", "UTC", PropertyStatus.Active, 1),
            CancellationToken.None).ConfigureAwait(false);
        await bedHandler.HandleAsync(
            new(Guid.NewGuid(), TenantA, now, PropertyC, RoomC, BedC, "A", BedStatus.Active, roomVersion: 2, bedVersion: 1),
            CancellationToken.None).ConfigureAwait(false);
        await roomHandler.HandleAsync(
            new(Guid.NewGuid(), TenantA, now, PropertyC, RoomC, "301", "Main", "3", RoomStatus.Active, 1),
            CancellationToken.None).ConfigureAwait(false);
        IProjectionRebuildWriter<PropertyTopologyProjectionExport> rebuildWriter = scope.ServiceProvider
            .GetRequiredService<IProjectionRebuildWriter<PropertyTopologyProjectionExport>>();
        ProjectionWriteResult rebuild = await rebuildWriter.WriteAsync(
            new ProjectionRebuildRequest(
                InventoryModuleMetadata.TopologyProjectionName,
                InventoryModuleMetadata.TopologyProjectionVersion,
                batchSize: 10),
            [
                new PropertyTopologyProjectionExport(
                    TenantA,
                    PropertyB,
                    "Beta House",
                    "beta",
                    "UTC",
                    PropertyStatus.Active,
                    1,
                    [
                        new RoomTopologyProjectionExport(
                            PropertyB,
                            RoomB,
                            "201",
                            "Main",
                            "2",
                            RoomStatus.Active,
                            2,
                            [new BedTopologyProjectionExport(PropertyB, RoomB, BedB, "A", BedStatus.Active, 1)])
                    ])
            ],
            CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(1, rebuild.WrittenCount);
        Assert.Equal(5, await dbContext.InventoryUnits.CountAsync().ConfigureAwait(false));
        InventoryRoomTopology outOfOrderRoom = await dbContext.RoomTopology
            .AsNoTracking()
            .SingleAsync(room => room.Id == RoomC)
            .ConfigureAwait(false);
        Assert.Equal(RoomStatus.Active, outOfOrderRoom.Status);
        Assert.Equal(1, outOfOrderRoom.SourceVersion);
    }

    private static async Task ExerciseAllocationAuthorityAsync(AuthTestApplication api)
    {
        Guid reservationOne = Guid.Parse("50000000-0000-0000-0000-000000000001");
        Guid requestOne = Guid.Parse("51000000-0000-0000-0000-000000000001");
        Guid reservationTwo = Guid.Parse("50000000-0000-0000-0000-000000000002");
        Guid requestTwo = Guid.Parse("51000000-0000-0000-0000-000000000002");
        Guid reservationThree = Guid.Parse("50000000-0000-0000-0000-000000000003");
        Guid requestThree = Guid.Parse("51000000-0000-0000-0000-000000000003");

        await HandleAllocationRequestAsync(
            api,
            reservationOne,
            requestOne,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3)).ConfigureAwait(false);
        await HandleAllocationRequestAsync(
            api,
            reservationTwo,
            requestTwo,
            new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 4)).ConfigureAwait(false);
        await HandleAllocationRequestAsync(
            api,
            reservationThree,
            requestThree,
            new DateOnly(2026, 9, 3),
            new DateOnly(2026, 9, 4)).ConfigureAwait(false);

        Guid allocationOne;
        using (IServiceScope verificationScope = api.Services.CreateScope())
        {
            verificationScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantA);
            InventoryDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            InventoryAllocation first = await dbContext.Allocations
                .AsNoTracking()
                .SingleAsync(allocation => allocation.AllocationRequestId == requestOne)
                .ConfigureAwait(false);
            InventoryAllocation overlapping = await dbContext.Allocations
                .AsNoTracking()
                .SingleAsync(allocation => allocation.AllocationRequestId == requestTwo)
                .ConfigureAwait(false);
            InventoryAllocation adjacent = await dbContext.Allocations
                .AsNoTracking()
                .SingleAsync(allocation => allocation.AllocationRequestId == requestThree)
                .ConfigureAwait(false);
            Assert.Equal(InventoryAllocationState.Active, first.Status);
            Assert.Equal(InventoryAllocationState.Rejected, overlapping.Status);
            Assert.Equal(InventoryAllocationRejection.AllocationConflict, overlapping.Rejection);
            Assert.Equal(InventoryAllocationState.Active, adjacent.Status);
            allocationOne = first.Id;
        }

        using (IServiceScope releaseScope = api.Services.CreateScope())
        {
            releaseScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantA);
            IIntegrationEventHandler<InventoryAllocationReleaseRequestedIntegrationEvent> releaseHandler =
                ResolveInventoryHandler<InventoryAllocationReleaseRequestedIntegrationEvent>(releaseScope.ServiceProvider);
            await releaseHandler.HandleAsync(
                new(
                    Guid.NewGuid(),
                    TenantA,
                    DateTimeOffset.UtcNow,
                    reservationOne,
                    allocationOne,
                    Guid.NewGuid(),
                    expectedAllocationVersion: 1),
                CancellationToken.None).ConfigureAwait(false);
            await releaseScope.ServiceProvider.GetRequiredService<InventoryDbContext>()
                .SaveChangesAsync()
                .ConfigureAwait(false);
        }

        Guid raceReservationOne = Guid.Parse("50000000-0000-0000-0000-000000000011");
        Guid raceRequestOne = Guid.Parse("51000000-0000-0000-0000-000000000011");
        Guid raceReservationTwo = Guid.Parse("50000000-0000-0000-0000-000000000012");
        Guid raceRequestTwo = Guid.Parse("51000000-0000-0000-0000-000000000012");
        using IServiceScope raceScopeOne = api.Services.CreateScope();
        using IServiceScope raceScopeTwo = api.Services.CreateScope();
        raceScopeOne.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantA);
        raceScopeTwo.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantA);
        IIntegrationEventHandler<InventoryAllocationRequestedIntegrationEvent> raceHandlerOne =
            ResolveInventoryHandler<InventoryAllocationRequestedIntegrationEvent>(raceScopeOne.ServiceProvider);
        IIntegrationEventHandler<InventoryAllocationRequestedIntegrationEvent> raceHandlerTwo =
            ResolveInventoryHandler<InventoryAllocationRequestedIntegrationEvent>(raceScopeTwo.ServiceProvider);
        InventoryAllocationRequestedIntegrationEvent raceOne = CreateAllocationRequest(
            raceReservationOne,
            raceRequestOne,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 3));
        InventoryAllocationRequestedIntegrationEvent raceTwo = CreateAllocationRequest(
            raceReservationTwo,
            raceRequestTwo,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 3));
        await raceHandlerOne.HandleAsync(raceOne, CancellationToken.None).ConfigureAwait(false);
        await raceHandlerTwo.HandleAsync(raceTwo, CancellationToken.None).ConfigureAwait(false);
        await raceScopeOne.ServiceProvider.GetRequiredService<InventoryDbContext>()
            .SaveChangesAsync()
            .ConfigureAwait(false);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            raceScopeTwo.ServiceProvider.GetRequiredService<InventoryDbContext>().SaveChangesAsync());

        await HandleAllocationRequestAsync(
            api,
            raceReservationTwo,
            raceRequestTwo,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 3)).ConfigureAwait(false);
        using IServiceScope raceVerificationScope = api.Services.CreateScope();
        raceVerificationScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantA);
        InventoryDbContext raceDbContext = raceVerificationScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(
            InventoryAllocationState.Active,
            (await raceDbContext.Allocations.AsNoTracking()
                .SingleAsync(allocation => allocation.AllocationRequestId == raceRequestOne)
                .ConfigureAwait(false)).Status);
        Assert.Equal(
            InventoryAllocationState.Rejected,
            (await raceDbContext.Allocations.AsNoTracking()
                .SingleAsync(allocation => allocation.AllocationRequestId == raceRequestTwo)
                .ConfigureAwait(false)).Status);
    }

    private static async Task HandleAllocationRequestAsync(
        AuthTestApplication api,
        Guid reservationId,
        Guid requestId,
        DateOnly arrival,
        DateOnly departure)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantA);
        IIntegrationEventHandler<InventoryAllocationRequestedIntegrationEvent> handler =
            ResolveInventoryHandler<InventoryAllocationRequestedIntegrationEvent>(scope.ServiceProvider);
        await handler.HandleAsync(
            CreateAllocationRequest(reservationId, requestId, arrival, departure),
            CancellationToken.None).ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>()
            .SaveChangesAsync()
            .ConfigureAwait(false);
    }

    private static InventoryAllocationRequestedIntegrationEvent CreateAllocationRequest(
        Guid reservationId,
        Guid requestId,
        DateOnly arrival,
        DateOnly departure) =>
        new(
            Guid.NewGuid(),
            TenantA,
            DateTimeOffset.UtcNow,
            reservationId,
            requestId,
            PropertyA,
            arrival,
            departure,
            [RoomA]);

    private static IIntegrationEventHandler<TEvent> ResolveInventoryHandler<TEvent>(IServiceProvider services)
        where TEvent : IIntegrationEvent
    {
        IntegrationEventSubscription subscription = services
            .GetRequiredService<IIntegrationEventSubscriptionRegistry>()
            .Subscriptions
            .Single(item =>
                item.ConsumerModule == InventoryModuleMetadata.Name &&
                item.EventType == typeof(TEvent));
        return (IIntegrationEventHandler<TEvent>)services.GetRequiredService(subscription.HandlerType);
    }

    private static async Task<InventoryAvailabilityResponse> GetAvailabilityAsync(
        HttpClient client,
        string accessToken,
        string arrival,
        string departure)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/inventory/properties/{PropertyA:D}/availability?arrival={arrival}&departure={departure}",
            accessToken).ConfigureAwait(false);
        return await ReadSuccessAsync<InventoryAvailabilityResponse>(response).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? accessToken = null,
        object? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Add(TenantHeader, TenantA);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success but received {(int)response.StatusCode}. Body: {body}");
        T? value = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        Assert.NotNull(value);
        return value;
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(
            response.StatusCode == expected,
            $"Expected {(int)expected} but received {(int)response.StatusCode}. Body: {body}");
    }

    private static async Task<AdminCliResult> AssertAdminSuccessAsync(Task<AdminCliResult> resultTask)
    {
        AdminCliResult result = await resultTask.ConfigureAwait(false);
        Assert.True(
            result.ExitCode == AdminExitCodes.Success,
            $"ExitCode={result.ExitCode}{Environment.NewLine}Output:{Environment.NewLine}{result.Output}{Environment.NewLine}Error:{Environment.NewLine}{result.Error}");
        return result;
    }

    private static Guid GetSubjectId(string accessToken)
    {
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string? subjectId = token.Claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, ClaimTypes.NameIdentifier, StringComparison.Ordinal) ||
            string.Equals(claim.Type, "nameid", StringComparison.Ordinal) ||
            string.Equals(claim.Type, "sub", StringComparison.Ordinal))?.Value;
        Assert.True(Guid.TryParse(subjectId, out Guid parsedSubjectId));
        return parsedSubjectId;
    }
}

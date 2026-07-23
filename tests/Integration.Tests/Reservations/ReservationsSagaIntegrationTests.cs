namespace Integration.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using BunkFy.Host.Worker;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Results;
using Gma.Framework.Tenancy;
using Gma.Modules.Auth.Contracts;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Persistence;
using Integration.Tests.Support;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class ReservationsSagaIntegrationTests
{
    private const string TenantId = "a3000000-0000-0000-0000-000000000001";
    private const string TenantHeader = "X-Tenant-Id";
    private static readonly Guid PropertyId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherPropertyId = Guid.Parse("71000000-0000-0000-0000-000000000002");
    private static readonly Guid RoomId = Guid.Parse("72000000-0000-0000-0000-000000000001");

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Real_worker_coordinates_reservation_and_stay_lifecycles_through_jetstream()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_reservations_saga_tests")
            .Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        string natsConnectionString = AuthTestContainers.GetNatsConnectionString(nats);
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            natsConnectionString,
            disableOutboxPublisher: false);
        await api.MigrateGuestRecordsAuthorizationDatabaseAsync().ConfigureAwait(false);
        await using AdminCliTestApplication admin = new("PostgreSql", connectionString);
        await admin.MigrateAsync().ConfigureAwait(false);
        using HttpClient client = api.CreateClient();

        using IHost worker = CreateWorker(connectionString, natsConnectionString);
        await worker.StartAsync().ConfigureAwait(false);
        try
        {
            await SeedInventoryAsync(api).ConfigureAwait(false);
            await SeedGovernedPropertyProjectionsAsync(api).ConfigureAwait(false);
            await WaitForSellableProjectionAsync(api, TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            AuthTokensResponse tokens = await AuthApiClient.RegisterAsync(
                client,
                TenantId,
                "operator@reservations.test").ConfigureAwait(false);
            Guid operatorId = GetSubjectId(tokens.AccessToken);
            await api.SeedOrganizationMembershipAsync(TenantId, operatorId).ConfigureAwait(false);
            await GrantReservationsAccessAsync(admin, operatorId).ConfigureAwait(false);

            GuestProfileDto canonicalGuest = await CreateGuestAsync(
                client,
                tokens.AccessToken,
                "Canonical Replacement Guest").ConfigureAwait(false);
            await WaitForGuestEligibilityProjectionAsync(
                api,
                canonicalGuest.GuestId,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            ReservationDto first = await CreateReservationAsync(
                client,
                tokens.AccessToken,
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 3),
                "First Guest").ConfigureAwait(false);
            Assert.Equal(ReservationStatus.PendingAllocation, first.Status);
            ReservationDto confirmed = await WaitForStatusAsync(
                client,
                tokens.AccessToken,
                first.ReservationId,
                ReservationStatus.Confirmed,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            Assert.NotNull(confirmed.AllocationId);

            ReservationDto overlapping = await CreateReservationAsync(
                client,
                tokens.AccessToken,
                new DateOnly(2026, 10, 2),
                new DateOnly(2026, 10, 4),
                "Overlapping Guest").ConfigureAwait(false);
            await WaitForStatusAsync(
                client,
                tokens.AccessToken,
                overlapping.ReservationId,
                ReservationStatus.AllocationRejected,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            using (HttpResponseMessage crossScope = await SendAsync(
                       client,
                       HttpMethod.Get,
                       $"/api/reservations/properties/{OtherPropertyId:D}/{first.ReservationId:D}",
                       tokens.AccessToken).ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Forbidden, crossScope).ConfigureAwait(false);
            }

            ReservationDto cancellationPending;
            using (HttpResponseMessage cancel = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/reservations/properties/{PropertyId:D}/{first.ReservationId:D}/cancel",
                       tokens.AccessToken,
                       new { expectedVersion = confirmed.Version }).ConfigureAwait(false))
            {
                cancellationPending = await ReadSuccessAsync<ReservationDto>(cancel).ConfigureAwait(false);
            }

            Assert.Equal(ReservationStatus.CancellationPending, cancellationPending.Status);
            await WaitForStatusAsync(
                client,
                tokens.AccessToken,
                first.ReservationId,
                ReservationStatus.Cancelled,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            ReservationDto replacement = await CreateReservationAsync(
                client,
                tokens.AccessToken,
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 3),
                "Replacement Guest").ConfigureAwait(false);
            ReservationDto replacementConfirmed = await WaitForStatusAsync(
                client,
                tokens.AccessToken,
                replacement.ReservationId,
                ReservationStatus.Confirmed,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            ReservationDto linked;
            using (HttpResponseMessage link = await SendAsync(
                       client,
                       HttpMethod.Put,
                       $"/api/reservations/properties/{PropertyId:D}/{replacement.ReservationId:D}/guests",
                       tokens.AccessToken,
                       new
                       {
                           guestId = canonicalGuest.GuestId,
                           role = ReservationGuestRoleKind.Primary,
                           replaceExistingRole = false,
                           expectedVersion = replacementConfirmed.Version
                       }).ConfigureAwait(false))
            {
                linked = await ReadSuccessAsync<ReservationDto>(link).ConfigureAwait(false);
            }

            Assert.Equal(canonicalGuest.GuestId, Assert.Single(linked.Guests).GuestId);

            using (HttpResponseMessage idempotentRetry = await SendAsync(
                       client,
                       HttpMethod.Put,
                       $"/api/reservations/properties/{PropertyId:D}/{replacement.ReservationId:D}/guests",
                       tokens.AccessToken,
                       new
                       {
                           guestId = canonicalGuest.GuestId,
                           role = ReservationGuestRoleKind.Primary,
                           replaceExistingRole = false,
                           expectedVersion = replacementConfirmed.Version
                       }).ConfigureAwait(false))
            {
                ReservationDto retried = await ReadSuccessAsync<ReservationDto>(idempotentRetry).ConfigureAwait(false);
                Assert.Equal(linked.Version, retried.Version);
            }

            using (HttpResponseMessage deniedCheckIn = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/reservations/properties/{OtherPropertyId:D}/{replacement.ReservationId:D}/check-in",
                       tokens.AccessToken,
                       new { businessDate = new DateOnly(2026, 10, 1), expectedVersion = linked.Version })
                       .ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Forbidden, deniedCheckIn).ConfigureAwait(false);
            }

            ReservationDto checkedIn;
            using (HttpResponseMessage checkIn = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/reservations/properties/{PropertyId:D}/{replacement.ReservationId:D}/check-in",
                       tokens.AccessToken,
                       new { businessDate = new DateOnly(2026, 10, 1), expectedVersion = linked.Version })
                       .ConfigureAwait(false))
            {
                checkedIn = await ReadSuccessAsync<ReservationDto>(checkIn).ConfigureAwait(false);
            }

            Assert.Equal(ReservationStatus.CheckedIn, checkedIn.Status);
            Assert.Equal(new DateOnly(2026, 10, 1), checkedIn.CheckedInBusinessDate);
            Assert.StartsWith("user:", checkedIn.CheckedInBy, StringComparison.Ordinal);

            using (HttpResponseMessage staleCheckIn = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/reservations/properties/{PropertyId:D}/{replacement.ReservationId:D}/check-in",
                       tokens.AccessToken,
                       new { businessDate = new DateOnly(2026, 10, 1), expectedVersion = replacementConfirmed.Version })
                       .ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Conflict, staleCheckIn).ConfigureAwait(false);
            }

            ReservationDto checkoutPending;
            using (HttpResponseMessage checkOut = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/reservations/properties/{PropertyId:D}/{replacement.ReservationId:D}/check-out",
                       tokens.AccessToken,
                       new { businessDate = new DateOnly(2026, 10, 3), expectedVersion = checkedIn.Version })
                       .ConfigureAwait(false))
            {
                checkoutPending = await ReadSuccessAsync<ReservationDto>(checkOut).ConfigureAwait(false);
            }

            Assert.Equal(ReservationStatus.CheckoutPending, checkoutPending.Status);
            ReservationDto checkedOut = await WaitForStatusAsync(
                client,
                tokens.AccessToken,
                replacement.ReservationId,
                ReservationStatus.CheckedOut,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            Assert.Equal(new DateOnly(2026, 10, 3), checkedOut.CheckedOutBusinessDate);
            Assert.Equal(checkedIn.CheckedInBy, checkedOut.CheckedOutBy);

            GuestStayHistoryItem stay = await WaitForGuestStayAsync(
                client,
                tokens.AccessToken,
                canonicalGuest.GuestId,
                replacement.ReservationId,
                GuestStayStatus.CheckedOut,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            Assert.Equal(checkedOut.Version, stay.ReservationVersion);
            Assert.Equal(checkedIn.CheckedInBusinessDate, stay.CheckedInBusinessDate);
            Assert.Equal(checkedOut.CheckedOutBusinessDate, stay.CheckedOutBusinessDate);

            await ApplyStaleGuestStayEventAsync(api, canonicalGuest.GuestId, replacement, linked.Version)
                .ConfigureAwait(false);
            GuestStayHistoryItem afterStale = await GetGuestStayAsync(
                client,
                tokens.AccessToken,
                canonicalGuest.GuestId,
                replacement.ReservationId).ConfigureAwait(false);
            Assert.Equal(GuestStayStatus.CheckedOut, afterStale.Status);
            Assert.Equal(checkedOut.Version, afterStale.ReservationVersion);

            using (HttpResponseMessage crossPropertyGuest = await SendAsync(
                       client,
                       HttpMethod.Get,
                       $"/api/guests/properties/{OtherPropertyId:D}/{canonicalGuest.GuestId:D}",
                       tokens.AccessToken).ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Forbidden, crossPropertyGuest).ConfigureAwait(false);
            }

            ReservationDto noShowCandidate = await CreateReservationAsync(
                client,
                tokens.AccessToken,
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 3),
                "No Show Guest").ConfigureAwait(false);
            ReservationDto noShowConfirmed = await WaitForStatusAsync(
                client,
                tokens.AccessToken,
                noShowCandidate.ReservationId,
                ReservationStatus.Confirmed,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            using (HttpResponseMessage noShow = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/reservations/properties/{PropertyId:D}/{noShowCandidate.ReservationId:D}/no-show",
                       tokens.AccessToken,
                       new { businessDate = new DateOnly(2026, 10, 1), expectedVersion = noShowConfirmed.Version })
                       .ConfigureAwait(false))
            {
                ReservationDto pending = await ReadSuccessAsync<ReservationDto>(noShow).ConfigureAwait(false);
                Assert.Equal(ReservationStatus.NoShowPending, pending.Status);
            }

            ReservationDto noShowTerminal = await WaitForStatusAsync(
                client,
                tokens.AccessToken,
                noShowCandidate.ReservationId,
                ReservationStatus.NoShow,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            Assert.Equal(new DateOnly(2026, 10, 1), noShowTerminal.NoShowBusinessDate);
            Assert.StartsWith("user:", noShowTerminal.NoShowBy, StringComparison.Ordinal);

            ReservationDto finalReplacement = await CreateReservationAsync(
                client,
                tokens.AccessToken,
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 3),
                "Final Replacement Guest").ConfigureAwait(false);
            await WaitForStatusAsync(
                client,
                tokens.AccessToken,
                finalReplacement.ReservationId,
                ReservationStatus.Confirmed,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        }
        finally
        {
            await worker.StopAsync().ConfigureAwait(false);
        }
    }

    private static IHost CreateWorker(string connectionString, string natsConnectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Environment.EnvironmentName = "Integration";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationIdentity:DisplayName"] = "BunkFy Reservation Saga Worker",
            ["ApplicationIdentity:Namespace"] = "bunkfy",
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] = connectionString,
            ["ConnectionStrings:nats"] = natsConnectionString,
            ["Tenancy:Enabled"] = "true",
            ["Caching:Enabled"] = "false",
            ["NatsJetStream:Enabled"] = "true",
            ["NatsConsumers:Enabled"] = "true",
            ["NatsConsumers:FetchBatchSize"] = "10",
            ["NatsConsumers:PollInterval"] = "00:00:00.100",
            ["NatsConsumers:AckWait"] = "00:00:05",
            ["NatsConsumers:AckProgressInterval"] = "00:00:01",
            ["NatsConsumers:HandlerTimeout"] = "00:00:10",
            ["NatsConsumers:NakDelay"] = "00:00:00.100",
            ["Outbox:PollIntervalMilliseconds"] = "100",
            ["Outbox:LockDurationMilliseconds"] = "5000",
            ["Worker:Modules:Properties"] = "true",
            ["Worker:Modules:Inventory"] = "true",
            ["Worker:Modules:Reservations"] = "true",
            ["Worker:Modules:Guests"] = "true",
            ["Tasks:Worker:Enabled"] = "false"
        });
        builder.AddWorkerHost();
        CountryPolicyIntegrationTestData.InstallRegistry(builder.Services);
        builder.ValidateModuleComposition();
        return builder.Build();
    }

    private static async Task SeedInventoryAsync(AuthTestApplication api)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using (IServiceScope scope = api.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
            IIntegrationEventHandler<PropertyCreatedIntegrationEvent> propertyHandler =
                ResolveInventoryHandler<PropertyCreatedIntegrationEvent>(scope.ServiceProvider);
            IIntegrationEventHandler<RoomCreatedIntegrationEvent> roomHandler =
                ResolveInventoryHandler<RoomCreatedIntegrationEvent>(scope.ServiceProvider);
            await propertyHandler.HandleAsync(
                new(Guid.NewGuid(), TenantId, now, PropertyId, "Saga House", "saga", "UTC", PropertyStatus.Active, 1),
                CancellationToken.None).ConfigureAwait(false);
            await roomHandler.HandleAsync(
                new(Guid.NewGuid(), TenantId, now, PropertyId, RoomId, "101", null, null, RoomStatus.Active, 1),
                CancellationToken.None).ConfigureAwait(false);
            await scope.ServiceProvider.GetRequiredService<InventoryDbContext>()
                .SaveChangesAsync()
                .ConfigureAwait(false);
        }

        using IServiceScope configurationScope = api.Services.CreateScope();
        configurationScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        Result<RoomInventoryDto> configured = await configurationScope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>()
            .SendAsync(
                new ConfigureRoomSalesModeCommand(PropertyId, RoomId, InventorySalesMode.RoomLevel, 1),
                CancellationToken.None)
            .ConfigureAwait(false);
        Assert.True(configured.IsSuccess, configured.Error.Code);
    }

    private static IIntegrationEventHandler<TEvent> ResolveInventoryHandler<TEvent>(IServiceProvider services)
        where TEvent : IIntegrationEvent
    {
        IntegrationEventSubscription subscription = services
            .GetRequiredService<IIntegrationEventSubscriptionRegistry>()
            .Subscriptions
            .Single(item => item.ConsumerModule == InventoryModuleMetadata.Name && item.EventType == typeof(TEvent));
        return (IIntegrationEventHandler<TEvent>)services.GetRequiredService(subscription.HandlerType);
    }

    private static async Task SeedGovernedPropertyProjectionsAsync(AuthTestApplication api)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        PropertyCreatedIntegrationEvent propertyCreated = new(
            Guid.NewGuid(),
            TenantId,
            DateTimeOffset.UtcNow,
            PropertyId,
            "Saga House",
            "saga",
            "UTC",
            PropertyStatus.Active,
            1);
        await ResolveHandler<PropertyCreatedIntegrationEvent>(
                scope.ServiceProvider,
                GuestsModuleMetadata.Name)
            .HandleAsync(propertyCreated, CancellationToken.None).ConfigureAwait(false);
        await ResolveHandler<PropertyCreatedIntegrationEvent>(
                scope.ServiceProvider,
                ReservationsModuleMetadata.Name)
            .HandleAsync(propertyCreated, CancellationToken.None).ConfigureAwait(false);
        await CountryPolicyIntegrationTestData.ApplyActivationAsync(
            scope.ServiceProvider,
            GuestsModuleMetadata.Name,
            TenantId,
            PropertyId,
            2).ConfigureAwait(false);
        await CountryPolicyIntegrationTestData.ApplyActivationAsync(
            scope.ServiceProvider,
            ReservationsModuleMetadata.Name,
            TenantId,
            PropertyId,
            2).ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<GuestsDbContext>()
            .SaveChangesAsync()
            .ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<ReservationsDbContext>()
            .SaveChangesAsync()
            .ConfigureAwait(false);
    }

    private static IIntegrationEventHandler<TEvent> ResolveHandler<TEvent>(
        IServiceProvider services,
        string consumerModule)
        where TEvent : IIntegrationEvent
    {
        IntegrationEventSubscription subscription = services
            .GetRequiredService<IIntegrationEventSubscriptionRegistry>()
            .Subscriptions
            .Single(item => item.ConsumerModule == consumerModule && item.EventType == typeof(TEvent));
        return (IIntegrationEventHandler<TEvent>)services.GetRequiredService(subscription.HandlerType);
    }

    private static async Task<GuestProfileDto> CreateGuestAsync(
        HttpClient client,
        string accessToken,
        string displayName)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            $"/api/guests/properties/{PropertyId:D}",
            accessToken,
            new
            {
                displayName,
                legalName = (string?)null,
                email = "shared@example.test",
                phone = "+100000000",
                dateOfBirth = (DateOnly?)null,
                nationalityCountryCode = (string?)null,
                preferredLanguageTag = "en",
                notes = (string?)null
            }).ConfigureAwait(false);
        return await ReadSuccessAsync<GuestProfileDto>(response).ConfigureAwait(false);
    }

    private static async Task WaitForGuestEligibilityProjectionAsync(
        AuthTestApplication api,
        Guid guestId,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using IServiceScope scope = api.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
            bool ready = await scope.ServiceProvider.GetRequiredService<ReservationsDbContext>()
                .GuestProfileProjections
                .AsNoTracking()
                .AnyAsync(profile => profile.Id == guestId && profile.Status == GuestStatus.Active)
                .ConfigureAwait(false);
            if (ready)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException("Reservations did not receive the Guest eligibility projection.");
    }

    private static async Task<GuestStayHistoryItem> WaitForGuestStayAsync(
        HttpClient client,
        string accessToken,
        Guid guestId,
        Guid reservationId,
        GuestStayStatus status,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            GuestStayHistoryItem? stay = await TryGetGuestStayAsync(
                client,
                accessToken,
                guestId,
                reservationId).ConfigureAwait(false);
            if (stay?.Status == status)
            {
                return stay;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException($"Guest stay did not reach '{status}'.");
    }

    private static async Task<GuestStayHistoryItem> GetGuestStayAsync(
        HttpClient client,
        string accessToken,
        Guid guestId,
        Guid reservationId) => await TryGetGuestStayAsync(client, accessToken, guestId, reservationId)
        .ConfigureAwait(false) ?? throw new InvalidOperationException("Guest stay was not found.");

    private static async Task<GuestStayHistoryItem?> TryGetGuestStayAsync(
        HttpClient client,
        string accessToken,
        Guid guestId,
        Guid reservationId)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/guests/properties/{PropertyId:D}/{guestId:D}/stays",
            accessToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        IReadOnlyCollection<GuestStayHistoryItem>? stays = await response.Content
            .ReadFromJsonAsync<IReadOnlyCollection<GuestStayHistoryItem>>()
            .ConfigureAwait(false);
        return stays?.SingleOrDefault(stay => stay.ReservationId == reservationId);
    }

    private static async Task ApplyStaleGuestStayEventAsync(
        AuthTestApplication api,
        Guid guestId,
        ReservationDto reservation,
        long staleVersion)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        IntegrationEventSubscription subscription = scope.ServiceProvider
            .GetRequiredService<IIntegrationEventSubscriptionRegistry>()
            .Subscriptions
            .Single(item => item.ConsumerModule == GuestsModuleMetadata.Name &&
                            item.EventType == typeof(ReservationGuestStayChangedIntegrationEvent));
        IIntegrationEventHandler<ReservationGuestStayChangedIntegrationEvent> handler =
            (IIntegrationEventHandler<ReservationGuestStayChangedIntegrationEvent>)scope.ServiceProvider
                .GetRequiredService(subscription.HandlerType);
        await handler.HandleAsync(
            new(
                Guid.NewGuid(),
                TenantId,
                DateTimeOffset.UtcNow,
                reservation.PropertyId,
                reservation.ReservationId,
                guestId,
                GuestStayRole.Primary,
                reservation.Arrival,
                reservation.Departure,
                GuestStayStatus.Confirmed,
                null,
                null,
                null,
                true,
                staleVersion),
            CancellationToken.None).ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<GuestsDbContext>()
            .SaveChangesAsync()
            .ConfigureAwait(false);
    }

    private static async Task WaitForSellableProjectionAsync(AuthTestApplication api, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using IServiceScope scope = api.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
            bool ready = await scope.ServiceProvider.GetRequiredService<ReservationsDbContext>()
                .InventoryUnitProjections
                .AsNoTracking()
                .AnyAsync(unit => unit.Id == RoomId && unit.PropertyId == PropertyId && unit.IsSellable)
                .ConfigureAwait(false);
            if (ready)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException("Reservations did not receive the sellable Inventory unit projection.");
    }

    private static async Task GrantReservationsAccessAsync(AdminCliTestApplication admin, Guid operatorId)
    {
        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "bootstrap", "--actor", "owner", "--yes"));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "create",
            "--actor", "owner",
            "--name", "reservations-operator"));
        foreach (string permission in new[]
                 {
                     ReservationsAdminPermissionCodes.Read,
                     ReservationsAdminPermissionCodes.Create,
                     ReservationsAdminPermissionCodes.Manage,
                     ReservationsAdminPermissionCodes.Cancel,
                     ReservationsAdminPermissionCodes.CheckIn,
                     ReservationsAdminPermissionCodes.NoShow,
                     ReservationsAdminPermissionCodes.CheckOut
                     ,ReservationsAdminPermissionCodes.ManageGuests
                     ,GuestsAdminPermissionCodes.Read
                     ,GuestsAdminPermissionCodes.Create
                     ,GuestsAdminPermissionCodes.Manage
                     ,GuestsAdminPermissionCodes.Archive
                 })
        {
            await AssertAdminSuccessAsync(admin.ExecuteAsync(
                "admin", "roles", "grant",
                "--actor", "owner",
                "--role", "reservations-operator",
                "--permission", permission));
        }

        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "assign",
            "--actor", "owner",
            "--target-kind", "user",
            "--target-id", operatorId.ToString("D"),
            "--role", "reservations-operator",
            "--scope", $"tenant:{TenantId}/property:{PropertyId:D}"));
    }

    private static async Task<ReservationDto> CreateReservationAsync(
        HttpClient client,
        string accessToken,
        DateOnly arrival,
        DateOnly departure,
        string guestName)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            $"/api/reservations/properties/{PropertyId:D}",
            accessToken,
            new
            {
                arrival,
                departure,
                inventoryUnitIds = new[] { RoomId },
                primaryGuestName = guestName,
                email = $"{guestName.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant()}@example.test",
                phone = (string?)null,
                guestCount = 1,
                sourceKind = ReservationSourceKind.Direct,
                sourceSystem = (string?)null,
                sourceReference = (string?)null,
                notes = (string?)null
            }).ConfigureAwait(false);
        return await ReadSuccessAsync<ReservationDto>(response).ConfigureAwait(false);
    }

    private static async Task<ReservationDto> WaitForStatusAsync(
        HttpClient client,
        string accessToken,
        Guid reservationId,
        ReservationStatus expected,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        ReservationDto? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await SendAsync(
                client,
                HttpMethod.Get,
                $"/api/reservations/properties/{PropertyId:D}/{reservationId:D}",
                accessToken).ConfigureAwait(false);
            last = await ReadSuccessAsync<ReservationDto>(response).ConfigureAwait(false);
            if (last.Status == expected)
            {
                return last;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Reservation '{reservationId}' did not reach '{expected}'. Last status: '{last?.Status}'.");
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? accessToken = null,
        object? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Add(TenantHeader, TenantId);
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
        Assert.True(response.IsSuccessStatusCode, $"Expected success but received {(int)response.StatusCode}. Body: {body}");
        T? value = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        Assert.NotNull(value);
        return value;
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected} but received {(int)response.StatusCode}. Body: {body}");
    }

    private static async Task AssertAdminSuccessAsync(Task<AdminCliResult> resultTask)
    {
        AdminCliResult result = await resultTask.ConfigureAwait(false);
        Assert.True(
            result.ExitCode == AdminExitCodes.Success,
            $"ExitCode={result.ExitCode}{Environment.NewLine}Output:{Environment.NewLine}{result.Output}{Environment.NewLine}Error:{Environment.NewLine}{result.Error}");
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

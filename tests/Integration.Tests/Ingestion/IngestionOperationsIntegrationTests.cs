namespace Integration.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapter.Runtime;
using BunkFy.Adapters.FakeHttp;
using BunkFy.Adapters.Http;
using BunkFy.Adapters.ImapReservationMail;
using BunkFy.Adapters.JsonFileDrop;
using BunkFy.Parsers.ReservationMail;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Results;
using Gma.Framework.Tenancy;
using Gma.Modules.Auth.Contracts;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Persistence;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BunkFy.Modules.Properties.Contracts;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class IngestionOperationsIntegrationTests
{
    private const string TenantId = "a5000000-0000-0000-0000-000000000001";
    private const string AccessKey = "minioadmin";
    private const string SecretKey = "minioadmin";
    private static readonly Guid PropertyId = Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherPropertyId = Guid.Parse("91000000-0000-0000-0000-000000000002");
    private static readonly Guid PushConnectionId = Guid.Parse("91000000-0000-0000-0000-000000000003");
    private static readonly Guid RemoteConnectionId = Guid.Parse("91000000-0000-0000-0000-000000000004");

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Management_api_scopes_connection_lifecycle_and_operational_reads()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_ingestion_operations_tests")
            .Build();
        await using IContainer minio = new ContainerBuilder("quay.io/minio/minio:latest")
            .WithEnvironment("MINIO_ROOT_USER", AccessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
            .WithPortBinding(9000, assignRandomHostPort: true)
            .WithCommand("server", "/data", "--console-address", ":9001")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(9000))
            .Build();
        await Task.WhenAll(nats.StartAsync(), postgreSql.StartAsync(), minio.StartAsync()).ConfigureAwait(false);
        string connectionString = postgreSql.GetConnectionString();
        string minioEndpoint = $"localhost:{minio.GetMappedPublicPort(9000)}";
        string bucketName = $"bunkfy-ingestion-operations-{Guid.NewGuid():N}";
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            AuthTestContainers.GetNatsConnectionString(nats),
            minioEndpoint: minioEndpoint,
            minioAccessKey: AccessKey,
            minioSecretKey: SecretKey,
            minioBucketName: bucketName,
            minioCreateBucketIfMissing: true);
        await api.MigratePropertiesAuthorizationDatabaseAsync().ConfigureAwait(false);
        await api.MigrateIngestionDatabaseAsync().ConfigureAwait(false);
        await using AdminCliTestApplication admin = new("PostgreSql", connectionString, includeIngestion: true);
        await admin.MigrateAsync().ConfigureAwait(false);
        await SeedPropertyProjectionAsync(api).ConfigureAwait(false);
        await ProveRetentionFenceConcurrencyAsync(api).ConfigureAwait(false);

        using HttpClient client = api.CreateClient();
        AuthTokensResponse tokens = await AuthApiClient.RegisterAsync(
            client,
            TenantId,
            "operator@ingestion.test").ConfigureAwait(false);
        Guid operatorId = GetSubjectId(tokens.AccessToken);
        await api.SeedOrganizationMembershipAsync(TenantId, operatorId).ConfigureAwait(false);
        Guid adminActorId = Guid.NewGuid();
        await GrantAccessAsync(admin, operatorId, adminActorId).ConfigureAwait(false);
        await using AdminApiTestApplication adminApi = new(
            "PostgreSql",
            connectionString,
            AuthTestContainers.GetNatsConnectionString(nats));
        using HttpClient adminClient = adminApi.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminApiTestApplication.CreateAccessTokenWithTenantClaim(adminActorId, TenantId));
        adminClient.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId);
        AdminCliResult redactionConfirmationRequired = await admin.ExecuteAsync(
            "ingestion", "retention", "redact-reservation-history",
            "--actor", "owner",
            "--tenant", TenantId,
            "--requested-by", "owner");
        Assert.NotEqual(AdminExitCodes.Success, redactionConfirmationRequired.ExitCode);
        Assert.Contains(
            AdminErrors.ConfirmationRequired.Message,
            redactionConfirmationRequired.Error,
            StringComparison.Ordinal);
        AdminCliResult reprocessingConfirmationRequired = await admin.ExecuteAsync(
            "ingestion", "reprocessing", "enqueue",
            "--actor", "owner",
            "--tenant", TenantId,
            "--property-id", PropertyId.ToString("D"),
            "--source-receipt-id", Guid.NewGuid().ToString("D"),
            "--parser-type", ReservationMailParserDescriptor.ParserType);
        Assert.NotEqual(AdminExitCodes.Success, reprocessingConfirmationRequired.ExitCode);
        Assert.True(
            reprocessingConfirmationRequired.Error.Contains(
                AdminErrors.ConfirmationRequired.Message,
                StringComparison.Ordinal),
            reprocessingConfirmationRequired.Error);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId);

        AdapterTypeCapabilityListResponse capabilities = await GetAsync<AdapterTypeCapabilityListResponse>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/adapter-types").ConfigureAwait(false);
        Assert.Equal(3, capabilities.AdapterTypes.Count);
        AdapterTypeCapabilityDto fakeHttp = Assert.Single(
            capabilities.AdapterTypes, capability => capability.AdapterType == "fake.http");
        Assert.Equal(
            [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling],
            fakeHttp.ExecutionModes);
        Assert.Equal(60, fakeHttp.MinimumPollingIntervalSeconds);
        Assert.Equal(300, fakeHttp.RecommendedPollingIntervalSeconds);
        AdapterTypeCapabilityDto fileDrop = Assert.Single(
            capabilities.AdapterTypes,
            capability => capability.AdapterType == JsonFileDropAdapterDescriptor.AdapterType);
        Assert.Equal(
            [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling],
            fileDrop.ExecutionModes);
        Assert.Equal(60, fileDrop.MinimumPollingIntervalSeconds);
        Assert.Equal(60, fileDrop.RecommendedPollingIntervalSeconds);
        AdapterTypeCapabilityDto imap = Assert.Single(
            capabilities.AdapterTypes,
            capability => capability.AdapterType == ImapReservationMailAdapterDescriptor.AdapterType);
        Assert.Equal(3, imap.ConfigurationSchemaVersion);
        Assert.Equal(
            [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling],
            imap.ExecutionModes);
        Assert.Equal(60, imap.MinimumPollingIntervalSeconds);
        Assert.Equal(300, imap.RecommendedPollingIntervalSeconds);

        ObservationParserCapabilityListResponse parserCapabilities =
            await GetAsync<ObservationParserCapabilityListResponse>(
                client,
                $"/api/ingestion/properties/{PropertyId:D}/parser-types").ConfigureAwait(false);
        ObservationParserCapabilityDto mailParser = Assert.Single(parserCapabilities.Parsers);
        Assert.Equal(ReservationMailParserDescriptor.ParserType, mailParser.ParserType);
        Assert.Equal(ReservationMailParserDescriptor.ParserVersion, mailParser.ParserVersion);
        Assert.Contains("mail.unparsed.v1", mailParser.SupportedSourceRecordTypes);

        using (HttpResponseMessage unsupportedMode = await client.PostAsJsonAsync(
                   $"/api/ingestion/properties/{PropertyId:D}/connections",
                   new
                   {
                       adapterType = "fake.http",
                       executionMode = AdapterExecutionMode.Continuous,
                       conflictPolicy = AdapterConflictPolicy.SuggestionsOnly,
                       configurationReference = "configuration://invalid"
                   }).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unsupportedMode.StatusCode);
        }

        AdapterConnectionDto created = await PostAsync<AdapterConnectionDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections",
            new
            {
                adapterType = "fake.http",
                executionMode = AdapterExecutionMode.Polling,
                conflictPolicy = AdapterConflictPolicy.SuggestionsOnly,
                configurationReference = "configuration://integration",
                secretReference = "secret://integration"
            }).ConfigureAwait(false);
        Assert.Equal(AdapterConnectionStatus.Enabled, created.Status);
        Assert.Equal(1, created.Version);
        Assert.True(created.HasSecretReference);

        AdapterConnectionListResponse connections = await GetAsync<AdapterConnectionListResponse>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections").ConfigureAwait(false);
        Assert.Equal(created.ConnectionId, Assert.Single(connections.Connections).ConnectionId);
        await SeedPushConnectionAsync(api).ConfigureAwait(false);
        await ProveAdapterIngressAsync(api, admin, client).ConfigureAwait(false);
        await SeedRemoteConnectionAsync(api).ConfigureAwait(false);
        await ProveRemoteAdapterLeasesAsync(api, client).ConfigureAwait(false);
        using (HttpResponseMessage connectionResponse = await client.GetAsync(
                   $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}").ConfigureAwait(false))
        {
            string json = await connectionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.True(connectionResponse.IsSuccessStatusCode, json);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.False(document.RootElement.TryGetProperty("secretReference", out _));
            Assert.True(document.RootElement.GetProperty("hasSecretReference").GetBoolean());
            Assert.DoesNotContain("secret://integration", json, StringComparison.Ordinal);
        }

        AdapterConnectionDto keptSecret = await PutAsync<AdapterConnectionDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}",
            new
            {
                executionMode = AdapterExecutionMode.Polling,
                conflictPolicy = AdapterConflictPolicy.SuggestionsOnly,
                configurationReference = "configuration://integration-updated",
                expectedVersion = created.Version
            }).ConfigureAwait(false);
        Assert.True(keptSecret.HasSecretReference);
        AdapterConnectionDto clearedSecret = await PutAsync<AdapterConnectionDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}",
            new
            {
                executionMode = AdapterExecutionMode.Polling,
                conflictPolicy = AdapterConflictPolicy.SuggestionsOnly,
                configurationReference = "configuration://integration-updated",
                clearSecretReference = true,
                expectedVersion = keptSecret.Version
            }).ConfigureAwait(false);
        Assert.False(clearedSecret.HasSecretReference);

        using (HttpResponseMessage invalidSchedule = await client.PutAsJsonAsync(
                   $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/polling-schedule",
                   new
                   {
                       intervalSeconds = 59,
                       maxAttempts = 3,
                       expectedVersion = clearedSecret.Version
                   }).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidSchedule.StatusCode);
        }

        AdapterConnectionDto scheduled = await PutAsync<AdapterConnectionDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/polling-schedule",
            new
            {
                intervalSeconds = 300,
                maxAttempts = 3,
                expectedVersion = clearedSecret.Version
            }).ConfigureAwait(false);
        Assert.Equal(300, scheduled.PollingIntervalSeconds);
        Assert.Equal(3, scheduled.PollingScheduleMaxAttempts);
        Assert.NotNull(scheduled.PollingScheduleConfiguredAtUtc);

        await AssertScheduleDiscoveryAsync(api, created.ConnectionId, expectedCount: 1).ConfigureAwait(false);
        AdapterConnectionHealthDto scheduledHealth = await GetAsync<AdapterConnectionHealthDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/health")
            .ConfigureAwait(false);
        Assert.Equal(300, scheduledHealth.PollingIntervalSeconds);
        Assert.Equal(3, scheduledHealth.PollingScheduleMaxAttempts);
        Assert.Equal(
            scheduled.PollingScheduleConfiguredAtUtc!.Value,
            scheduledHealth.PollingScheduleConfiguredAtUtc!.Value,
            TimeSpan.FromMilliseconds(1));
        Assert.Equal(
            scheduled.PollingScheduleConfiguredAtUtc!.Value,
            scheduledHealth.NextRunExpectedAtUtc!.Value,
            TimeSpan.FromMilliseconds(1));
        Assert.True(scheduledHealth.RunExpected);

        AdapterConnectionDto scheduleCleared = await PostAsync<AdapterConnectionDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/polling-schedule/clear",
            new { expectedVersion = scheduled.Version }).ConfigureAwait(false);
        Assert.Null(scheduleCleared.PollingIntervalSeconds);
        Assert.Null(scheduleCleared.PollingScheduleMaxAttempts);
        Assert.Null(scheduleCleared.PollingScheduleConfiguredAtUtc);
        await AssertScheduleDiscoveryAsync(api, created.ConnectionId, expectedCount: 0).ConfigureAwait(false);

        AdapterConnectionDto rescheduled = await PutAsync<AdapterConnectionDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/polling-schedule",
            new
            {
                intervalSeconds = 300,
                maxAttempts = 4,
                expectedVersion = scheduleCleared.Version
            }).ConfigureAwait(false);

        (Guid receiptId, byte[] rawPayload) = await SubmitObservationAsync(api, created.ConnectionId).ConfigureAwait(false);
        AdapterConnectionHealthDto activeHealth = await GetAsync<AdapterConnectionHealthDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/health")
            .ConfigureAwait(false);
        Assert.Equal(AdapterConnectionOperationalState.ObservationsReceived, activeHealth.OperationalState);
        Assert.Equal(AdapterCapabilityStatus.Available, activeHealth.CapabilityStatus);
        Assert.Equal("fake.http", activeHealth.AdapterType);
        Assert.Equal(1, activeHealth.ProtocolVersion);
        Assert.Equal(1, activeHealth.ConfigurationSchemaVersion);
        Assert.Equal(1, activeHealth.PendingReceiptCount);
        Assert.NotNull(activeHealth.LastObservationReceivedAtUtc);
        Assert.Equal(300, activeHealth.PollingIntervalSeconds);
        Assert.Equal(4, activeHealth.PollingScheduleMaxAttempts);
        string rawPayloadPath =
            $"/api/ingestion/properties/{PropertyId:D}/receipts/{receiptId:D}/raw-payload";
        using (HttpResponseMessage deniedPayload = await client.GetAsync(rawPayloadPath).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.Forbidden, deniedPayload.StatusCode);
        }

        await GrantRawPayloadAccessAsync(admin).ConfigureAwait(false);
        using (HttpResponseMessage downloadedPayload = await client.GetAsync(rawPayloadPath).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.OK, downloadedPayload.StatusCode);
            Assert.Equal("application/octet-stream", downloadedPayload.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", downloadedPayload.Content.Headers.ContentDisposition?.DispositionType);
            Assert.True(downloadedPayload.Headers.CacheControl?.NoStore);
            Assert.True(downloadedPayload.Headers.TryGetValues("X-Content-Type-Options", out IEnumerable<string>? values));
            Assert.Contains("nosniff", values);
            Assert.Equal(rawPayload, await downloadedPayload.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
        }

        using (HttpResponseMessage crossPropertyPayload = await client.GetAsync(
                   $"/api/ingestion/properties/{OtherPropertyId:D}/receipts/{receiptId:D}/raw-payload").ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.Forbidden, crossPropertyPayload.StatusCode);
        }

        await PurgeRawPayloadAsync(api, admin, adminClient, receiptId).ConfigureAwait(false);
        using (HttpResponseMessage purgedPayload = await client.GetAsync(rawPayloadPath).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.Gone, purgedPayload.StatusCode);
        }
        AdapterConnectionHealthDto retainedHealth = await GetAsync<AdapterConnectionHealthDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/health")
            .ConfigureAwait(false);
        Assert.Equal(0, retainedHealth.PendingReceiptCount);
        Assert.Equal(1, retainedHealth.RejectedReceiptCount);
        Assert.Equal(0, retainedHealth.ExpiredRawPayloadCount);
        Assert.Equal(0, retainedHealth.ProtectedRawPayloadCount);
        Assert.Equal(0, retainedHealth.HeldExpiredRawPayloadCount);
        Assert.Equal(0, retainedHealth.PurgingRawPayloadCount);
        Assert.Equal(0, retainedHealth.DueSensitiveHistoryCount);
        Assert.Equal(0, retainedHealth.HeldDueSensitiveHistoryCount);
        Assert.Equal(1, retainedHealth.RedactedSensitiveHistoryCount);
        Assert.Equal(0, retainedHealth.ActiveLegalHoldCount);

        AdapterConnectionDto disabled = await PostAsync<AdapterConnectionDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/disable",
            new { expectedVersion = rescheduled.Version }).ConfigureAwait(false);
        Assert.Equal(AdapterConnectionStatus.Disabled, disabled.Status);
        Assert.Equal(300, disabled.PollingIntervalSeconds);
        Assert.Equal(4, disabled.PollingScheduleMaxAttempts);
        await AssertScheduleDiscoveryAsync(api, created.ConnectionId, expectedCount: 0).ConfigureAwait(false);
        AdapterConnectionHealthDto disabledHealth = await GetAsync<AdapterConnectionHealthDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/health")
            .ConfigureAwait(false);
        Assert.Equal(AdapterConnectionOperationalState.Disabled, disabledHealth.OperationalState);
        Assert.Equal(300, disabledHealth.PollingIntervalSeconds);
        Assert.Equal(4, disabledHealth.PollingScheduleMaxAttempts);
        Assert.Null(disabledHealth.NextRunExpectedAtUtc);
        Assert.False(disabledHealth.RunExpected);

        AdapterConnectionDto reset = await PostAsync<AdapterConnectionDto>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{created.ConnectionId:D}/reset-checkpoint",
            new { expectedVersion = disabled.Version }).ConfigureAwait(false);
        Assert.Equal(disabled.Version, reset.Version);

        IngestionRunListResponse runs = await GetAsync<IngestionRunListResponse>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/runs?connectionId={created.ConnectionId:D}")
            .ConfigureAwait(false);
        ObservationReceiptListResponse receipts = await GetAsync<ObservationReceiptListResponse>(
            client,
            $"/api/ingestion/properties/{PropertyId:D}/receipts?connectionId={created.ConnectionId:D}")
            .ConfigureAwait(false);
        Assert.Empty(runs.Runs);
        Assert.Equal(receiptId, Assert.Single(receipts.Receipts).ReceiptId);

        await ProveJsonFileDropAdapterAsync(api, client).ConfigureAwait(false);

        using HttpResponseMessage crossProperty = await client.GetAsync(
            $"/api/ingestion/properties/{OtherPropertyId:D}/connections").ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Forbidden, crossProperty.StatusCode);
    }

    private static async Task ProveJsonFileDropAdapterAsync(
        AuthTestApplication api,
        HttpClient managementClient)
    {
        AdapterConnectionDto connection = await PostAsync<AdapterConnectionDto>(
            managementClient,
            $"/api/ingestion/properties/{PropertyId:D}/connections",
            new
            {
                adapterType = JsonFileDropAdapterDescriptor.AdapterType,
                executionMode = AdapterExecutionMode.Polling,
                conflictPolicy = AdapterConflictPolicy.SuggestionsOnly,
                configurationReference = "configuration://json-file-drop-integration"
            }).ConfigureAwait(false);

        string root = Path.Combine(
            Path.GetTempPath(),
            $"bunkfy-file-drop-integration-{Guid.NewGuid():N}");
        string pending = Path.Combine(root, connection.ConnectionId.ToString("N"), "pending");
        Directory.CreateDirectory(pending);
        string fileName = "20260712T120000Z-booking-43.json";
        string pendingPath = Path.Combine(pending, fileName);
        await File.WriteAllTextAsync(pendingPath, /*lang=json,strict*/ """
            {
              "schemaVersion": 1,
              "recordType": "operations.file-drop.v1",
              "externalRecordId": "booking-file-drop-43",
              "sourceRevision": "1",
              "sourceUpdatedAtUtc": "2026-07-12T11:59:00Z",
              "observedAtUtc": "2026-07-12T12:00:00Z",
              "payload": { "source": "json-file-drop", "booking": "booking-file-drop-43" }
            }
            """).ConfigureAwait(false);

        try
        {
            Guid taskRunId = Guid.NewGuid();
            const int taskAttempt = 1;
            using IServiceScope scope = api.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            Result<AdapterRunStart> started = await dispatcher.SendAsync(
                new StartAdapterRunCommand(connection.ConnectionId, taskRunId, taskAttempt),
                CancellationToken.None).ConfigureAwait(false);
            Assert.True(started.IsSuccess, started.Error.Code);

            DateTimeOffset assignedAtUtc = DateTimeOffset.UtcNow;
            AdapterRunAssignment assignment = new(
                started.Value.RunId,
                Guid.NewGuid(),
                started.Value.ConnectionId,
                started.Value.ScopeId,
                started.Value.PropertyId,
                started.Value.AdapterType,
                started.Value.ExecutionMode,
                assignedAtUtc,
                assignedAtUtc.AddMinutes(5),
                started.Value.Checkpoint);
            IAdapterObservationSink sink = scope.ServiceProvider
                .GetRequiredService<IAdapterObservationSinkFactory>()
                .Create(assignment);
            ServiceCollection adapterServices = new();
            adapterServices.AddJsonFileDropAdapter(new JsonFileDropAdapterOptions(root));
            using ServiceProvider adapterProvider = adapterServices.BuildServiceProvider();
            IAdapterRunner runner = Assert.Single(adapterProvider.GetServices<IAdapterRunner>());
            using AdapterConfigurationMaterial material = new(
                schemaVersion: 1,
                "application/json",
                "{}"u8,
                secretContentType: null,
                []);

            AdapterRunCompletion completion = await runner.RunAsync(
                assignment,
                material,
                sink,
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(AdapterRunOutcome.Succeeded, completion.Outcome);
            Assert.Equal(1, completion.AcceptedCount);
            Result<Unit> completed = await dispatcher.SendAsync(
                new CompleteAdapterRunCommand(
                    started.Value.RunId,
                    taskRunId,
                    taskAttempt,
                    completion.Outcome,
                    completion.ObservedCount,
                    completion.AcceptedCount,
                    completion.RejectedCount,
                    completion.AcceptedCheckpoint,
                    completion.ErrorCode),
                CancellationToken.None).ConfigureAwait(false);
            Assert.True(completed.IsSuccess, completed.Error.Code);

            ObservationReceiptListResponse receipts = await GetAsync<ObservationReceiptListResponse>(
                managementClient,
                $"/api/ingestion/properties/{PropertyId:D}/receipts?connectionId={connection.ConnectionId:D}")
                .ConfigureAwait(false);
            ObservationReceiptDto receipt = Assert.Single(receipts.Receipts);
            Assert.Equal("booking-file-drop-43", receipt.ExternalId);
            Assert.False(File.Exists(pendingPath));
            Assert.True(File.Exists(Path.Combine(
                root,
                connection.ConnectionId.ToString("N"),
                "processed",
                fileName)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task SeedPropertyProjectionAsync(AuthTestApplication api)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        IntegrationEventSubscription subscription = scope.ServiceProvider
            .GetRequiredService<IIntegrationEventSubscriptionRegistry>()
            .Subscriptions.Single(item => item.ConsumerModule == IngestionModuleMetadata.Name &&
                                          item.EventType == typeof(PropertyCreatedIntegrationEvent));
        var handler = (IIntegrationEventHandler<PropertyCreatedIntegrationEvent>)scope.ServiceProvider
            .GetRequiredService(subscription.HandlerType);
        await handler.HandleAsync(
            new(Guid.NewGuid(), TenantId, DateTimeOffset.UtcNow, PropertyId, "Operations House", "operations",
                "UTC", PropertyStatus.Active, 1), CancellationToken.None).ConfigureAwait(false);
        await CountryPolicyIntegrationTestData.ApplyActivationAsync(
            scope.ServiceProvider,
            IngestionModuleMetadata.Name,
            TenantId,
            PropertyId,
            2).ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<IngestionDbContext>().SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task GrantAccessAsync(
        AdminCliTestApplication admin,
        Guid operatorId,
        Guid adminActorId)
    {
        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "bootstrap", "--actor", "owner", "--yes"));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "create", "--actor", "owner", "--name", "ingestion-operator"));
        foreach (string permission in new[]
                 {
                     IngestionAdminPermissionCodes.Read,
                     IngestionAdminPermissionCodes.ConnectionsManage,
                     IngestionAdminPermissionCodes.CredentialsManage,
                     IngestionAdminPermissionCodes.RetentionManage,
                     IngestionAdminPermissionCodes.ReprocessingManage,
                     IngestionAdminPermissionCodes.LegalHoldsManage
                 })
        {
            await AssertAdminSuccessAsync(admin.ExecuteAsync(
                "admin", "roles", "grant", "--actor", "owner", "--role", "ingestion-operator",
                "--permission", permission));
        }

        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "assign", "--actor", "owner", "--target-kind", "user",
            "--target-id", operatorId.ToString("D"), "--role", "ingestion-operator",
            "--scope", $"tenant:{TenantId}/property:{PropertyId:D}"));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "assign", "--actor", "owner", "--target-kind", "admin-actor",
            "--target-id", adminActorId.ToString("D"), "--role", "ingestion-operator",
            "--scope", $"tenant:{TenantId}"));
    }

    private static async Task GrantRawPayloadAccessAsync(AdminCliTestApplication admin) =>
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "grant", "--actor", "owner", "--role", "ingestion-operator",
            "--permission", IngestionAdminPermissionCodes.RawPayloadsRead));

    private static async Task SeedPushConnectionAsync(AuthTestApplication api)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        IngestionDbContext dbContext = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        AdapterConnection connection = AdapterConnection.Create(
            PushConnectionId,
            TenantId,
            PropertyId,
            "integration.push",
            AdapterExecutionMode.Push,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://integration-push",
            secretReference: null,
            DateTimeOffset.UtcNow).Value;
        dbContext.AdapterConnections.Add(connection);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedRemoteConnectionAsync(AuthTestApplication api)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        IngestionDbContext dbContext = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        AdapterConnection connection = AdapterConnection.Create(
            RemoteConnectionId,
            TenantId,
            PropertyId,
            FakeHttpAdapterDescriptor.Value.AdapterType,
            AdapterExecutionMode.RemotePolling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://integration-remote",
            secretReference: null,
            DateTimeOffset.UtcNow).Value;
        dbContext.AdapterConnections.Add(connection);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task ProveRemoteAdapterLeasesAsync(
        AuthTestApplication api,
        HttpClient managementClient)
    {
        string credentialsPath =
            $"/api/ingestion/properties/{PropertyId:D}/connections/{RemoteConnectionId:D}/credentials";
        CreateAdapterIngressCredentialResponse credential =
            await PostAsync<CreateAdapterIngressCredentialResponse>(
                managementClient,
                credentialsPath,
                new { label = "remote poller" }).ConfigureAwait(false);
        using HttpClient adapterHttp = api.CreateClient();
        AdapterHttpIngressClient remoteClient = CreateAdapterClient(
            adapterHttp, credential.Token, RemoteConnectionId);
        Guid workerId = Guid.Parse("93000000-0000-0000-0000-000000000001");
        RemoteLeasedAdapterCycleRunner cycle = new(
            new RemoteIntegrationRunner(),
            remoteClient,
            new StandaloneMaterialProvider(),
            new AdapterRuntimeIdentity(
                TenantId,
                PropertyId,
                RemoteConnectionId,
                FakeHttpAdapterDescriptor.Value.AdapterType,
                TimeSpan.FromMinutes(5)),
            workerId,
            TimeSpan.FromSeconds(30));

        AdapterRunCompletion runtimeCompletion;
        try
        {
            runtimeCompletion = await cycle.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            Assert.Fail($"Remote adapter cycle request failed with HTTP status {exception.StatusCode?.ToString() ?? "none"}.");
            throw;
        }
        Assert.Equal(AdapterRunOutcome.Succeeded, runtimeCompletion.Outcome);
        Assert.Equal("remote-cursor-1", runtimeCompletion.AcceptedCheckpoint);
        AdapterConnectionDto afterRuntime = await GetAsync<AdapterConnectionDto>(
            managementClient,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{RemoteConnectionId:D}")
            .ConfigureAwait(false);
        Assert.Equal("remote-cursor-1", afterRuntime.Checkpoint);

        AdapterRemoteLeaseClaimRequest firstClaim = new(
            Guid.NewGuid(),
            Guid.Parse("93000000-0000-0000-0000-000000000002"),
            FakeHttpAdapterDescriptor.Value.AdapterType,
            FakeHttpAdapterDescriptor.Value.ProtocolVersion,
            FakeHttpAdapterDescriptor.Value.ConfigurationSchemaVersion,
            RequestedLeaseSeconds: 60);
        AdapterRemoteLeaseClaimRequest secondClaim = firstClaim with
        {
            ClaimId = Guid.NewGuid(),
            WorkerId = Guid.Parse("93000000-0000-0000-0000-000000000003")
        };
        Task<HttpResponseMessage> firstTask = SendAdapterControlAsync(
            adapterHttp, RemoteConnectionId, credential.Token, "remote-leases/claim", firstClaim);
        Task<HttpResponseMessage> secondTask = SendAdapterControlAsync(
            adapterHttp, RemoteConnectionId, credential.Token, "remote-leases/claim", secondClaim);
        HttpResponseMessage[] claims = await Task.WhenAll(firstTask, secondTask).ConfigureAwait(false);
        try
        {
            HttpResponseMessage winnerResponse = Assert.Single(claims, response => response.IsSuccessStatusCode);
            Assert.Single(claims, response => response.StatusCode == HttpStatusCode.Conflict);
            AdapterRemoteLeaseClaimResponse winner = (await winnerResponse.Content
                .ReadFromJsonAsync<AdapterRemoteLeaseClaimResponse>().ConfigureAwait(false))!;
            Assert.Equal("remote-cursor-1", winner.Assignment.Checkpoint);
            Guid winningWorker = claims[0].IsSuccessStatusCode
                ? firstClaim.WorkerId
                : secondClaim.WorkerId;
            AdapterRemoteLeaseProof staleProof = new(
                winner.Assignment.RunId,
                winner.Assignment.LeaseId,
                winner.LeaseEpoch,
                winningWorker);

            using (IServiceScope scope = api.Services.CreateScope())
            {
                IngestionDbContext dbContext = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
                DateTimeOffset expiredAt = DateTimeOffset.UtcNow.AddSeconds(-1);
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE ingestion.adapter_connections
                    SET "RemoteLeaseExpiresAtUtc" = {expiredAt}
                    WHERE "Id" = {RemoteConnectionId};
                    """).ConfigureAwait(false);
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE ingestion.runs
                    SET "RemoteLeaseExpiresAtUtc" = {expiredAt}
                    WHERE "Id" = {winner.Assignment.RunId};
                    """).ConfigureAwait(false);
            }

            AdapterRemoteLeaseClaimRequest takeoverRequest = firstClaim with
            {
                ClaimId = Guid.NewGuid(),
                WorkerId = Guid.Parse("93000000-0000-0000-0000-000000000004")
            };
            using HttpResponseMessage takeoverResponse = await SendAdapterControlAsync(
                adapterHttp,
                RemoteConnectionId,
                credential.Token,
                "remote-leases/claim",
                takeoverRequest).ConfigureAwait(false);
            AdapterRemoteLeaseClaimResponse takeover =
                await ReadSuccessAsync<AdapterRemoteLeaseClaimResponse>(takeoverResponse).ConfigureAwait(false);
            Assert.True(takeover.LeaseEpoch > winner.LeaseEpoch);
            Assert.Equal("remote-cursor-1", takeover.Assignment.Checkpoint);

            byte[] stalePayload = /*lang=json,strict*/ "{\"source\":\"stale-worker\"}"u8.ToArray();
            AdapterObservedRecord staleRecord = CreateIngressRecord(
                Guid.Parse("92000000-0000-0000-0000-000000000004"),
                stalePayload,
                "remote-stale-4");
            using HttpResponseMessage staleSubmission = await SendAdapterControlAsync(
                adapterHttp,
                RemoteConnectionId,
                credential.Token,
                "remote-leases/observations",
                new AdapterRemoteObservationSubmissionRequest(
                    staleProof,
                    [AdapterIngressObservationRequest.FromRecord(staleRecord)],
                    "stale-checkpoint")).ConfigureAwait(false);
            AdapterRemoteObservationSubmissionResponse staleResult =
                await ReadSuccessAsync<AdapterRemoteObservationSubmissionResponse>(staleSubmission)
                    .ConfigureAwait(false);
            Assert.Equal(
                AdapterObservationDisposition.Rejected,
                Assert.Single(staleResult.Acknowledgement.Results).Disposition);
            Assert.False(staleResult.Acknowledgement.CheckpointAccepted);

            AdapterRemoteLeaseProof takeoverProof = new(
                takeover.Assignment.RunId,
                takeover.Assignment.LeaseId,
                takeover.LeaseEpoch,
                takeoverRequest.WorkerId);
            using HttpResponseMessage completed = await SendAdapterControlAsync(
                adapterHttp,
                RemoteConnectionId,
                credential.Token,
                "remote-leases/complete",
                new AdapterRemoteRunCompletionRequest(
                    takeoverProof,
                    AdapterRunOutcome.Succeeded,
                    0,
                    0,
                    0,
                    "remote-cursor-1",
                    ErrorCode: null)).ConfigureAwait(false);
            _ = await ReadSuccessAsync<AdapterRemoteRunCompletionResponse>(completed).ConfigureAwait(false);
        }
        finally
        {
            foreach (HttpResponseMessage response in claims)
            {
                response.Dispose();
            }
        }

        using HttpResponseMessage directPushBypass = await SendIngressAsync(
            adapterHttp,
            TenantId,
            RemoteConnectionId,
            credential.Token,
            CreateIngressSubmission(Guid.NewGuid(), "{}"u8.ToArray(), "remote-bypass"))
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Unauthorized, directPushBypass.StatusCode);

        IngestionRunListResponse remoteRuns = await GetAsync<IngestionRunListResponse>(
            managementClient,
            $"/api/ingestion/properties/{PropertyId:D}/runs?connectionId={RemoteConnectionId:D}")
            .ConfigureAwait(false);
        Assert.Equal(3, remoteRuns.Runs.Count);
        Assert.All(remoteRuns.Runs, run =>
            Assert.Equal(IngestionRunExecutionKindDto.RemoteLease, run.ExecutionKind));
        Assert.Equal(2, remoteRuns.Runs.Count(run => run.Status == IngestionRunStatus.Succeeded));
        IngestionRunDto heartbeatRun = Assert.Single(remoteRuns.Runs, run => run.ObservedCount == 1);
        Assert.True(heartbeatRun.Version >= 3);
        IngestionRunDto expiredRun = Assert.Single(
            remoteRuns.Runs,
            run => run.Status == IngestionRunStatus.Failed);
        Assert.Equal("ingestion.remote-lease-expired", expiredRun.ErrorCode);
    }

    private static async Task ProveAdapterIngressAsync(
        AuthTestApplication api,
        AdminCliTestApplication admin,
        HttpClient managementClient)
    {
        string credentialsPath =
            $"/api/ingestion/properties/{PropertyId:D}/connections/{PushConnectionId:D}/credentials";
        CreateAdapterIngressCredentialResponse primary;
        using (HttpResponseMessage created = await managementClient.PostAsJsonAsync(
                   credentialsPath,
                   new { label = "integration primary" }).ConfigureAwait(false))
        {
            primary = await ReadSuccessAsync<CreateAdapterIngressCredentialResponse>(created).ConfigureAwait(false);
            Assert.True(created.Headers.CacheControl?.NoStore);
        }

        Assert.StartsWith("bfi_v1_", primary.Token, StringComparison.Ordinal);
        AdapterIngressCredentialListResponse initialList = await GetAsync<AdapterIngressCredentialListResponse>(
            managementClient, credentialsPath).ConfigureAwait(false);
        AdapterIngressCredentialDto listedPrimary = Assert.Single(initialList.Credentials);
        string listedJson = JsonSerializer.Serialize(initialList);
        Assert.DoesNotContain(primary.Token, listedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secretHash", listedJson, StringComparison.OrdinalIgnoreCase);
        AdminCliResult cliList = await admin.ExecuteAsync(
            "ingestion", "credentials", "list",
            "--actor", "owner",
            "--tenant", TenantId,
            "--property-id", PropertyId.ToString("D"),
            "--connection-id", PushConnectionId.ToString("D"),
            "--output", "json");
        Assert.Equal(AdminExitCodes.Success, cliList.ExitCode);
        Assert.DoesNotContain(primary.Token, cliList.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("secretHash", cliList.Output, StringComparison.OrdinalIgnoreCase);

        byte[] payload = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"source\":\"remote-push\"}");
        Guid operationId = Guid.Parse("92000000-0000-0000-0000-000000000001");
        object submission = CreateIngressSubmission(operationId, payload, "remote-push-1");
        AdapterObservedRecord observedRecord = CreateIngressRecord(operationId, payload, "remote-push-1");
        using HttpClient ingressClient = api.CreateClient();

        using (HttpResponseMessage missing = await SendIngressAsync(
                   ingressClient, TenantId, PushConnectionId, token: null, submission).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        }
        using (HttpResponseMessage staffJwt = await managementClient.PostAsJsonAsync(
                   $"/api/ingestion/adapter-ingress/connections/{PushConnectionId:D}/observations",
                   submission).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, staffJwt.StatusCode);
        }
        using (HttpResponseMessage wrongTenant = await SendIngressAsync(
                   ingressClient, "tenant-other", PushConnectionId, primary.Token, submission).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, wrongTenant.StatusCode);
        }
        using (HttpResponseMessage wrongConnection = await SendIngressAsync(
                   ingressClient, TenantId, Guid.NewGuid(), primary.Token, submission).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, wrongConnection.StatusCode);
        }

        AdapterHttpIngressClient primaryIngress = CreateIngressClient(ingressClient, primary.Token);
        AdapterIngressSubmissionResponse accepted = await primaryIngress.SubmitAsync(
            [observedRecord], CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(AdapterObservationDisposition.Accepted, Assert.Single(accepted.Results).Disposition);
        AdapterIngressSubmissionResponse duplicate = await primaryIngress.SubmitAsync(
            [observedRecord], CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(AdapterObservationDisposition.Duplicate, Assert.Single(duplicate.Results).Disposition);

        CreateAdapterIngressCredentialResponse secondary = await PostAsync<CreateAdapterIngressCredentialResponse>(
            managementClient,
            credentialsPath,
            new { label = "integration rotated" }).ConfigureAwait(false);
        Assert.NotEqual(primary.Token, secondary.Token);

        AdapterIngressCredentialDto revoked = await PostAsync<AdapterIngressCredentialDto>(
            managementClient,
            $"{credentialsPath}/{primary.Credential.CredentialId:D}/revoke",
            new { expectedVersion = primary.Credential.Version }).ConfigureAwait(false);
        Assert.Equal(AdapterIngressCredentialStatus.Revoked, revoked.Status);
        using (HttpResponseMessage revokedIngress = await SendIngressAsync(
                   ingressClient, TenantId, PushConnectionId, primary.Token, submission).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, revokedIngress.StatusCode);
        }
        AdapterHttpIngressClient secondaryIngress = CreateIngressClient(ingressClient, secondary.Token);

        MemoryCheckpointLease runtimeCheckpoint = new(PushConnectionId);
        StandaloneAdapterCycleRunner standaloneCycle = new(
            new StandaloneIntegrationRunner(),
            secondaryIngress,
            runtimeCheckpoint,
            new StandaloneMaterialProvider(),
            new AdapterRuntimeIdentity(
                TenantId,
                PropertyId,
                PushConnectionId,
                "integration.push",
                TimeSpan.FromMinutes(5)));
        AdapterRunCompletion standaloneCompletion = await standaloneCycle.RunAsync(CancellationToken.None)
            .ConfigureAwait(false);
        Assert.Equal(AdapterRunOutcome.Succeeded, standaloneCompletion.Outcome);
        Assert.Equal("standalone-cursor-3", runtimeCheckpoint.Checkpoint);
        Assert.Equal(1, runtimeCheckpoint.Generation);
        ObservationReceiptListResponse standaloneReceipts = await GetAsync<ObservationReceiptListResponse>(
            managementClient,
            $"/api/ingestion/properties/{PropertyId:D}/receipts?connectionId={PushConnectionId:D}")
            .ConfigureAwait(false);
        Assert.Contains(standaloneReceipts.Receipts, receipt =>
            receipt.ExternalId == "remote-standalone-3");

        AdapterConnectionDto disabled = await PostAsync<AdapterConnectionDto>(
            managementClient,
            $"/api/ingestion/properties/{PropertyId:D}/connections/{PushConnectionId:D}/disable",
            new { expectedVersion = 1L }).ConfigureAwait(false);
        Assert.Equal(AdapterConnectionStatus.Disabled, disabled.Status);
        AdapterObservedRecord disabledRecord = CreateIngressRecord(
            Guid.Parse("92000000-0000-0000-0000-000000000002"), payload, "remote-push-2");
        using HttpResponseMessage disabledResponse = await SendIngressAsync(
            ingressClient,
            TenantId,
            PushConnectionId,
            secondary.Token,
            CreateIngressSubmission(
                disabledRecord.OperationId,
                disabledRecord.Payload.ToArray(),
                disabledRecord.ExternalRecordId)).ConfigureAwait(false);
        string disabledBody = await disabledResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(
            disabledResponse.IsSuccessStatusCode,
            $"Expected a shaped disabled-connection result but received {(int)disabledResponse.StatusCode}: {disabledBody}");
        AdapterIngressSubmissionResponse rejected = (await disabledResponse.Content
            .ReadFromJsonAsync<AdapterIngressSubmissionResponse>().ConfigureAwait(false))!;
        AdapterObservationResult rejectedResult = Assert.Single(rejected.Results);
        Assert.Equal(AdapterObservationDisposition.Rejected, rejectedResult.Disposition);
        Assert.Equal("ingestion.connectionnotenabled", rejectedResult.ErrorCode);

        AdapterIngressCredentialListResponse finalList = await GetAsync<AdapterIngressCredentialListResponse>(
            managementClient, credentialsPath).ConfigureAwait(false);
        Assert.Equal(2, finalList.TotalCount);
        Assert.Contains(finalList.Credentials, credential =>
            credential.CredentialId == listedPrimary.CredentialId && credential.LastAuthenticatedAtUtc.HasValue);

        AdminCliResult confirmationRequired = await admin.ExecuteAsync(
            "ingestion", "credentials", "revoke",
            "--actor", "owner",
            "--tenant", TenantId,
            "--property-id", PropertyId.ToString("D"),
            "--connection-id", PushConnectionId.ToString("D"),
            "--credential-id", secondary.Credential.CredentialId.ToString("D"),
            "--expected-version", secondary.Credential.Version.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        Assert.NotEqual(AdminExitCodes.Success, confirmationRequired.ExitCode);
        Assert.Contains(AdminErrors.ConfirmationRequired.Message, confirmationRequired.Error, StringComparison.Ordinal);
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "ingestion", "credentials", "revoke",
            "--actor", "owner",
            "--tenant", TenantId,
            "--property-id", PropertyId.ToString("D"),
            "--connection-id", PushConnectionId.ToString("D"),
            "--credential-id", secondary.Credential.CredentialId.ToString("D"),
            "--expected-version", secondary.Credential.Version.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            "--yes"));
        Assert.Equal(0, await admin.CountAuditEntriesContainingAsync(primary.Token).ConfigureAwait(false));
        Assert.Equal(0, await admin.CountAuditEntriesContainingAsync(secondary.Token).ConfigureAwait(false));
    }

    private static object CreateIngressSubmission(Guid operationId, byte[] payload, string externalId) => new
    {
        records = new[]
        {
            new
            {
                operationId,
                recordType = "integration.push.v1",
                externalRecordId = externalId,
                sourceRevision = "1",
                sourceUpdatedAtUtc = DateTimeOffset.UtcNow,
                observedAtUtc = DateTimeOffset.UtcNow,
                contentType = "application/json",
                payload,
                contentSha256 = AdapterPayloadHash.ComputeSha256(payload)
            }
        }
    };

    private static AdapterObservedRecord CreateIngressRecord(
        Guid operationId,
        byte[] payload,
        string externalId) => new(
            operationId,
            "integration.push.v1",
            externalId,
            "1",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "application/json",
            payload,
            AdapterPayloadHash.ComputeSha256(payload));

    private static AdapterHttpIngressClient CreateIngressClient(HttpClient client, string token) =>
        CreateAdapterClient(client, token, PushConnectionId);

    private static AdapterHttpIngressClient CreateAdapterClient(
        HttpClient client,
        string token,
        Guid connectionId) => new(
        client,
        new AdapterHttpIngressOptions(
            client.BaseAddress!,
            TenantId,
            connectionId,
            maxAttempts: 2,
            retryBaseDelay: TimeSpan.FromMilliseconds(10),
            retryMaxDelay: TimeSpan.FromMilliseconds(10),
            retryJitterFactor: 0,
            allowInsecureLoopback: true),
        new StaticAdapterIngressTokenProvider(token));

    private sealed class RemoteIntegrationRunner : IAdapterRunner
    {
        private static readonly byte[] Payload = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"source\":\"remote-leased-runtime\"}");

        public AdapterDescriptor Descriptor => FakeHttpAdapterDescriptor.Value;

        public async Task<AdapterRunCompletion> RunAsync(
            AdapterRunAssignment assignment,
            AdapterConfigurationMaterial material,
            IAdapterObservationSink sink,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(12), cancellationToken).ConfigureAwait(false);
            AdapterObservedRecord record = new(
                Guid.Parse("92000000-0000-0000-0000-000000000005"),
                "integration.remote.v1",
                "remote-leased-5",
                "5",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                "application/json",
                Payload,
                AdapterPayloadHash.ComputeSha256(Payload));
            AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
                new AdapterObservationSubmission(
                    assignment.RunId,
                    assignment.LeaseId,
                    [record],
                    "  remote-cursor-1  "),
                cancellationToken).ConfigureAwait(false);
            Assert.True(acknowledgement.CheckpointAccepted);
            Assert.Equal("remote-cursor-1", acknowledgement.AcceptedCheckpoint);
            Assert.Equal(AdapterObservationDisposition.Accepted, Assert.Single(acknowledgement.Results).Disposition);
            return new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.Succeeded,
                1,
                1,
                0,
                acknowledgement.AcceptedCheckpoint,
                errorCode: null,
                errorMessage: null);
        }
    }

    private sealed class StandaloneIntegrationRunner : IAdapterRunner
    {
        private static readonly byte[] Payload = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"source\":\"standalone-runtime\"}");

        public static AdapterObservedRecord Record { get; } = new(
            Guid.Parse("92000000-0000-0000-0000-000000000003"),
            "integration.push.v1",
            "remote-standalone-3",
            "3",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "application/json",
            Payload,
            AdapterPayloadHash.ComputeSha256(Payload));

        public AdapterDescriptor Descriptor { get; } = new(
            "integration.push",
            protocolVersion: 1,
            configurationSchemaVersion: 1,
            [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling],
            new AdapterPollingCapability(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5)));

        public async Task<AdapterRunCompletion> RunAsync(
            AdapterRunAssignment assignment,
            AdapterConfigurationMaterial material,
            IAdapterObservationSink sink,
            CancellationToken cancellationToken)
        {
            AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
                new AdapterObservationSubmission(
                    assignment.RunId,
                    assignment.LeaseId,
                    [Record],
                    "standalone-cursor-3"),
                cancellationToken).ConfigureAwait(false);
            AdapterObservationResult result = Assert.Single(acknowledgement.Results);
            Assert.Equal(AdapterObservationDisposition.Accepted, result.Disposition);
            Assert.True(acknowledgement.CheckpointAccepted);
            return new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.Succeeded,
                observedCount: 1,
                acceptedCount: 1,
                rejectedCount: 0,
                acknowledgement.AcceptedCheckpoint,
                errorCode: null,
                errorMessage: null);
        }
    }

    private sealed class StandaloneMaterialProvider : IAdapterRuntimeMaterialProvider
    {
        public Task<AdapterConfigurationMaterial> ResolveAsync(
            AdapterRuntimeIdentity identity,
            int configurationSchemaVersion,
            CancellationToken cancellationToken) => Task.FromResult(new AdapterConfigurationMaterial(
            configurationSchemaVersion,
            "application/json",
            "{}"u8));
    }

    private sealed class MemoryCheckpointLease(Guid connectionId) : IAdapterCheckpointLease
    {
        public Guid ConnectionId { get; } = connectionId;
        public string? Checkpoint { get; private set; }
        public long Generation { get; private set; }

        public Task SaveAsync(
            string checkpoint,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken)
        {
            this.Checkpoint = checkpoint;
            this.Generation++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task<HttpResponseMessage> SendIngressAsync(
        HttpClient client,
        string tenantId,
        Guid connectionId,
        string? token,
        object submission)
    {
        HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/api/ingestion/adapter-ingress/connections/{connectionId:D}/observations")
        {
            Content = JsonContent.Create(submission)
        };
        request.Headers.Add("X-Tenant-Id", tenantId);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("BunkFy-Adapter", token);
        }

        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendAdapterControlAsync<TRequest>(
        HttpClient client,
        Guid connectionId,
        string token,
        string action,
        TRequest content)
    {
        HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/api/ingestion/adapter-ingress/connections/{connectionId:D}/{action}")
        {
            Content = JsonContent.Create(content)
        };
        request.Headers.Add("X-Tenant-Id", TenantId);
        request.Headers.Authorization = new AuthenticationHeaderValue("BunkFy-Adapter", token);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task AssertScheduleDiscoveryAsync(
        AuthTestApplication api,
        Guid connectionId,
        int expectedCount)
    {
        using IServiceScope scope = api.Services.CreateScope();
        IReadOnlyList<AdapterPollingScheduleDefinition> schedules = await scope.ServiceProvider
            .GetRequiredService<IAdapterPollingScheduleReader>()
            .ListActiveAsync(CancellationToken.None).ConfigureAwait(false);
        AdapterPollingScheduleDefinition[] matching = schedules
            .Where(item => item.ConnectionId == connectionId)
            .ToArray();
        Assert.Equal(expectedCount, matching.Length);
        if (expectedCount == 1)
        {
            AdapterPollingScheduleDefinition schedule = Assert.Single(matching);
            Assert.Equal(TenantId, schedule.ScopeId);
            Assert.Equal(300, schedule.IntervalSeconds);
            Assert.InRange(schedule.MaxAttempts, 3, 4);
        }
    }

    private static async Task<(Guid ReceiptId, byte[] Payload)> SubmitObservationAsync(
        AuthTestApplication api,
        Guid connectionId)
    {
        byte[] payload = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"reservation\":\"booking-operations-42\"}");
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        Result<AdapterObservationResult> result = await scope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>()
            .SendAsync(
                new ReceiveObservationCommand(
                    connectionId,
                    RunId: null,
                    Guid.NewGuid(),
                    "operations.raw.v1",
                    "booking-operations-42",
                    "1",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    "application/json",
                    payload,
                    AdapterPayloadHash.ComputeSha256(payload)),
                CancellationToken.None).ConfigureAwait(false);
        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.NotNull(result.Value.ReceiptId);
        return (result.Value.ReceiptId.Value, payload);
    }

    private static async Task PurgeRawPayloadAsync(
        AuthTestApplication api,
        AdminCliTestApplication admin,
        HttpClient adminClient,
        Guid receiptId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        IngestionDbContext dbContext = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        ObservationReceipt receipt = await dbContext.ObservationReceipts.SingleAsync(
            item => item.Id == receiptId).ConfigureAwait(false);
        Assert.True(receipt.Reject("retention integration test", DateTimeOffset.UtcNow).IsSuccess);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        await dbContext.ObservationReceipts
            .Where(item => item.Id == receiptId)
            .ExecuteUpdateAsync(update => update.SetProperty(
                item => item.RawPayloadRetainUntilUtc,
                DateTimeOffset.UtcNow.AddMinutes(-1))).ConfigureAwait(false);

        Guid claimId = Guid.NewGuid();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        DateTimeOffset proposalCreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-3);
        ChangeProposal proposal = ChangeProposal.Create(
            Guid.NewGuid(), TenantId, receipt.PropertyId, receipt.ConnectionId, receipt.Id,
            Guid.NewGuid(), receipt.RawPayloadFileId, 1, "retention-test", /*lang=json,strict*/ "{\"guest\":\"Sensitive\"}",
            proposalCreatedAtUtc).Value;
        dbContext.ChangeProposals.Add(proposal);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        Result<IReadOnlyList<RawPayloadPurgeCandidate>> pendingClaim = await dispatcher.SendAsync(
            new ClaimExpiredRawPayloadsCommand(
                Guid.NewGuid(),
                BatchSize: 10,
                PurgeExpiredRawPayloadsPayload.DefaultStaleClaimMinutes),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(pendingClaim.IsSuccess, pendingClaim.Error.Code);
        Assert.Empty(pendingClaim.Value);

        Assert.True(proposal.BeginApply(
            "staff:retention-test",
            Guid.NewGuid(),
            proposal.Version,
            proposalCreatedAtUtc.AddDays(1)).IsSuccess);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        Result<IReadOnlyList<RawPayloadPurgeCandidate>> applyingClaim = await dispatcher.SendAsync(
            new ClaimExpiredRawPayloadsCommand(
                Guid.NewGuid(),
                BatchSize: 10,
                PurgeExpiredRawPayloadsPayload.DefaultStaleClaimMinutes),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(applyingClaim.IsSuccess, applyingClaim.Error.Code);
        Assert.Empty(applyingClaim.Value);

        DateTimeOffset proposalCompletedAtUtc = proposalCreatedAtUtc.AddDays(1).AddMinutes(1);
        Assert.True(proposal.MarkFailed(
            "Retention integration test",
            proposal.Version,
            proposalCompletedAtUtc.AddDays(1),
            proposalCompletedAtUtc).IsSuccess);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        LegalHoldDto firstPlaced = await PostAsync<LegalHoldDto>(
            adminClient,
            $"/api/admin/ingestion/properties/{receipt.PropertyId:D}/legal-holds",
            new { reason = "Regulatory matter 2026-01" }).ConfigureAwait(false);
        Result<LegalHoldDto> firstHold = Result.Success(firstPlaced);
        Result<LegalHoldDto> secondHold = await dispatcher.SendAsync(
            new PlaceLegalHoldCommand(
                receipt.PropertyId,
                "Litigation matter 2026-02",
                "integration:legal"),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(firstHold.IsSuccess, firstHold.Error.Code);
        Assert.True(secondHold.IsSuccess, secondHold.Error.Code);
        Result<LegalHoldListResponse> activeHolds = await dispatcher.QueryAsync(
            new ListLegalHoldsQuery(
                receipt.PropertyId,
                LegalHoldStatus.Active,
                Page: 1,
                PageSize: 20),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(activeHolds.IsSuccess, activeHolds.Error.Code);
        Assert.Equal(2, activeHolds.Value.TotalCount);
        LegalHoldListResponse adminHoldList = await GetAsync<LegalHoldListResponse>(
            adminClient,
            $"/api/admin/ingestion/properties/{receipt.PropertyId:D}/legal-holds?status=Active")
            .ConfigureAwait(false);
        Assert.Equal(2, adminHoldList.TotalCount);
        LegalHoldDto adminHoldDetail = await GetAsync<LegalHoldDto>(
            adminClient,
            $"/api/admin/ingestion/properties/{receipt.PropertyId:D}/legal-holds/{firstHold.Value.HoldId:D}")
            .ConfigureAwait(false);
        Assert.Equal("Regulatory matter 2026-01", adminHoldDetail.Reason);
        LegalHoldListResponse crossPropertyHolds = await GetAsync<LegalHoldListResponse>(
            adminClient,
            $"/api/admin/ingestion/properties/{OtherPropertyId:D}/legal-holds").ConfigureAwait(false);
        Assert.Empty(crossPropertyHolds.LegalHolds);
        Assert.Equal(0, crossPropertyHolds.TotalCount);
        AdminCliResult legalHoldList = await admin.ExecuteAsync(
            "ingestion", "legal-holds", "list",
            "--actor", "owner",
            "--tenant", TenantId,
            "--output", "json",
            "--property-id", receipt.PropertyId.ToString("D"));
        Assert.True(
            legalHoldList.ExitCode == AdminExitCodes.Success,
            $"ExitCode={legalHoldList.ExitCode}{Environment.NewLine}{legalHoldList.Output}{Environment.NewLine}{legalHoldList.Error}");
        Assert.Contains(firstHold.Value.HoldId.ToString("D"), legalHoldList.Output, StringComparison.OrdinalIgnoreCase);

        Result<IReadOnlyList<RawPayloadPurgeCandidate>> heldClaim = await dispatcher.SendAsync(
            new ClaimExpiredRawPayloadsCommand(
                Guid.NewGuid(),
                BatchSize: 10,
                PurgeExpiredRawPayloadsPayload.DefaultStaleClaimMinutes),
            CancellationToken.None).ConfigureAwait(false);
        Result<SensitiveHistoryRedactionBatchResult> heldRedaction = await dispatcher.SendAsync(
            new RedactExpiredSensitiveHistoryCommand(BatchSize: 10),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(heldClaim.IsSuccess, heldClaim.Error.Code);
        Assert.Empty(heldClaim.Value);
        Assert.True(heldRedaction.IsSuccess, heldRedaction.Error.Code);
        Assert.Equal(new SensitiveHistoryRedactionBatchResult(0, 0), heldRedaction.Value);
        Result<AdapterConnectionHealthDto> heldHealth = await dispatcher.QueryAsync(
            new GetAdapterConnectionHealthQuery(receipt.PropertyId, receipt.ConnectionId),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(heldHealth.IsSuccess, heldHealth.Error.Code);
        Assert.Equal(1, heldHealth.Value.HeldExpiredRawPayloadCount);
        Assert.Equal(1, heldHealth.Value.HeldDueSensitiveHistoryCount);
        Assert.Equal(2, heldHealth.Value.ActiveLegalHoldCount);
        Assert.Equal(0, heldHealth.Value.ExpiredRawPayloadCount);
        Assert.Equal(0, heldHealth.Value.DueSensitiveHistoryCount);

        Result<LegalHoldDto> firstReleased = await dispatcher.SendAsync(
            new ReleaseLegalHoldCommand(
                receipt.PropertyId,
                firstHold.Value.HoldId,
                firstHold.Value.Version,
                "Regulatory matter closed",
                "integration:legal"),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(firstReleased.IsSuccess, firstReleased.Error.Code);
        Result<IReadOnlyList<RawPayloadPurgeCandidate>> stillHeld = await dispatcher.SendAsync(
            new ClaimExpiredRawPayloadsCommand(
                Guid.NewGuid(),
                BatchSize: 10,
                PurgeExpiredRawPayloadsPayload.DefaultStaleClaimMinutes),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(stillHeld.IsSuccess, stillHeld.Error.Code);
        Assert.Empty(stillHeld.Value);

        AdminCliResult releaseConfirmationRequired = await admin.ExecuteAsync(
            "ingestion", "legal-holds", "release",
            "--actor", "owner",
            "--tenant", TenantId,
            "--property-id", receipt.PropertyId.ToString("D"),
            "--hold-id", secondHold.Value.HoldId.ToString("D"),
            "--expected-version", secondHold.Value.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--reason", "Litigation matter closed");
        Assert.NotEqual(AdminExitCodes.Success, releaseConfirmationRequired.ExitCode);
        Assert.Contains(
            AdminErrors.ConfirmationRequired.Message,
            releaseConfirmationRequired.Error,
            StringComparison.Ordinal);
        using (HttpResponseMessage releaseWithoutConfirmation = await adminClient.PostAsJsonAsync(
                   $"/api/admin/ingestion/properties/{receipt.PropertyId:D}/legal-holds/{secondHold.Value.HoldId:D}/release",
                   new
                   {
                       expectedVersion = secondHold.Value.Version,
                       releaseReason = "Litigation matter closed",
                       confirmed = false
                   }).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.BadRequest, releaseWithoutConfirmation.StatusCode);
            Assert.Contains(
                AdminErrors.ConfirmationRequired.Code,
                await releaseWithoutConfirmation.Content.ReadAsStringAsync().ConfigureAwait(false),
                StringComparison.Ordinal);
        }
        LegalHoldDto secondReleased = await PostAsync<LegalHoldDto>(
            adminClient,
            $"/api/admin/ingestion/properties/{receipt.PropertyId:D}/legal-holds/{secondHold.Value.HoldId:D}/release",
            new
            {
                expectedVersion = secondHold.Value.Version,
                releaseReason = "Litigation matter closed",
                confirmed = true
            }).ConfigureAwait(false);
        Assert.Equal(LegalHoldStatus.Released, secondReleased.Status);
        dbContext.ChangeTracker.Clear();

        Result<IReadOnlyList<RawPayloadPurgeCandidate>> claimed = await dispatcher.SendAsync(
            new ClaimExpiredRawPayloadsCommand(
                claimId,
                BatchSize: 10,
                PurgeExpiredRawPayloadsPayload.DefaultStaleClaimMinutes),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(claimed.IsSuccess, claimed.Error.Code);
        RawPayloadPurgeCandidate candidate = Assert.Single(claimed.Value);
        Result<LegalHoldDto> tooLateHold = await dispatcher.SendAsync(
            new PlaceLegalHoldCommand(
                receipt.PropertyId,
                "Hold attempted after deletion claim",
                "integration:legal"),
            CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(IngestionApplicationErrors.LegalHoldPurgeInProgress, tooLateHold.Error);
        IRawPayloadStore rawPayloads = scope.ServiceProvider.GetRequiredService<IRawPayloadStore>();
        _ = await rawPayloads.DeleteAsync(
            candidate.RawPayloadFileId,
            TenantId,
            candidate.ConnectionId,
            CancellationToken.None).ConfigureAwait(false);
        Assert.Null(await rawPayloads.ReadAsync(
            candidate.RawPayloadFileId,
            TenantId,
            candidate.ConnectionId,
            CancellationToken.None).ConfigureAwait(false));
        Result<Unit> completed = await dispatcher.SendAsync(
            new CompleteRawPayloadPurgeCommand(candidate.ReceiptId, claimId),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(completed.IsSuccess, completed.Error.Code);

        Result<SensitiveHistoryRedactionBatchResult> redacted = await dispatcher.SendAsync(
            new RedactExpiredSensitiveHistoryCommand(BatchSize: 10),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(redacted.IsSuccess, redacted.Error.Code);
        Assert.Equal(1, redacted.Value.ProposalCount);
        Assert.Equal(0, redacted.Value.DispatchCount);
        dbContext.ChangeTracker.Clear();
        ChangeProposal redactedProposal = await dbContext.ChangeProposals.SingleAsync(
            item => item.Id == proposal.Id).ConfigureAwait(false);
        Assert.Null(redactedProposal.Diff);
        Assert.Equal("retention-test", redactedProposal.ReasonCode);
        Assert.NotNull(redactedProposal.SensitiveDataRedactedAtUtc);
        Result<ChangeProposalDto> redactedRead = await dispatcher.QueryAsync(
            new GetChangeProposalQuery(receipt.PropertyId, proposal.Id),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(redactedRead.IsSuccess, redactedRead.Error.Code);
        Assert.Equal(SensitiveHistoryStatus.Redacted, redactedRead.Value.SensitiveHistoryStatus);
        Assert.Equal("retention-test", redactedRead.Value.ReasonCode);
        Assert.Null(redactedRead.Value.Diff);
        Result<LegalHoldListResponse> releasedHolds = await dispatcher.QueryAsync(
            new ListLegalHoldsQuery(
                receipt.PropertyId,
                LegalHoldStatus.Released,
                Page: 1,
                PageSize: 20),
            CancellationToken.None).ConfigureAwait(false);
        Assert.True(releasedHolds.IsSuccess, releasedHolds.Error.Code);
        Assert.Equal(2, releasedHolds.Value.TotalCount);
    }

    private static async Task ProveRetentionFenceConcurrencyAsync(AuthTestApplication api)
    {
        using IServiceScope firstScope = api.Services.CreateScope();
        using IServiceScope secondScope = api.Services.CreateScope();
        firstScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        secondScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        IRetentionFenceRepository firstFence = firstScope.ServiceProvider
            .GetRequiredService<IRetentionFenceRepository>();
        IRetentionFenceRepository secondFence = secondScope.ServiceProvider
            .GetRequiredService<IRetentionFenceRepository>();
        Assert.True(await firstFence.TryAdvanceAsync(PropertyId, CancellationToken.None).ConfigureAwait(false));
        Assert.True(await secondFence.TryAdvanceAsync(PropertyId, CancellationToken.None).ConfigureAwait(false));

        await firstScope.ServiceProvider.GetRequiredService<IngestionDbContext>()
            .SaveChangesAsync().ConfigureAwait(false);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondScope.ServiceProvider.GetRequiredService<IngestionDbContext>()
                .SaveChangesAsync());
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(false);
        return await ReadSuccessAsync<T>(response).ConfigureAwait(false);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(path, body).ConfigureAwait(false);
        return await ReadSuccessAsync<T>(response).ConfigureAwait(false);
    }

    private static async Task<T> PutAsync<T>(HttpClient client, string path, object body)
    {
        using HttpResponseMessage response = await client.PutAsJsonAsync(path, body).ConfigureAwait(false);
        return await ReadSuccessAsync<T>(response).ConfigureAwait(false);
    }

    private static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but received {(int)response.StatusCode}. Body: {body}");
        T? value = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        return Assert.IsType<T>(value);
    }

    private static async Task AssertAdminSuccessAsync(Task<AdminCliResult> resultTask)
    {
        AdminCliResult result = await resultTask.ConfigureAwait(false);
        Assert.True(result.ExitCode is AdminExitCodes.Success,
            $"ExitCode={result.ExitCode}{Environment.NewLine}{result.Output}{Environment.NewLine}{result.Error}");
    }

    private static Guid GetSubjectId(string accessToken)
    {
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string? subject = token.Claims.FirstOrDefault(claim =>
            claim.Type is ClaimTypes.NameIdentifier or "nameid" or "sub")?.Value;
        Assert.True(Guid.TryParse(subject, out Guid id));
        return id;
    }
}

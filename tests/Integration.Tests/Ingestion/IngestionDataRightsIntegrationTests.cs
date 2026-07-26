namespace Integration.Tests;

using System.Security.Cryptography;
using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.FileManagement.LocalStorage;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class IngestionDataRightsIntegrationTests
{
    private const string TenantId = "tenant-ingestion-data-rights";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 18, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Ingestion_contributors_stream_exact_provider_evidence_on_postgresql()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_ingestion_data_rights_tests")
            .Build();
        await postgreSql.StartAsync();

        string fileRoot = Path.Combine(
            Path.GetTempPath(),
            $"bunkfy-ingestion-data-rights-{Guid.NewGuid():N}");
        try
        {
            using ServiceProvider provider = CreatePersistenceProvider(
                postgreSql.GetConnectionString(),
                fileRoot);
            using IServiceScope scope = provider.CreateScope();
            IngestionDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
            await dbContext.Database.MigrateAsync();

            Guid propertyId = Guid.NewGuid();
            Guid reservationId = Guid.NewGuid();
            Guid connectionId = Guid.NewGuid();
            Guid receiptId = Guid.NewGuid();
            Guid payloadFileId = Guid.NewGuid();
            IIngestionPropertyProjectionRepository properties =
                scope.ServiceProvider
                    .GetRequiredService<IIngestionPropertyProjectionRepository>();
            await properties.ApplySnapshotAsync(
                new IngestionPropertyProjectionWriteModel(
                    TenantId,
                    propertyId,
                    "Provider House",
                    "provider-house",
                    IsActive: true,
                    PropertyProcessingStatus.Unconfigured,
                    GovernancePolicy: null,
                    SourceVersion: 1),
                CancellationToken.None);

            AdapterConnection connection = AdapterConnection.Create(
                connectionId,
                TenantId,
                propertyId,
                "fake.http",
                AdapterExecutionMode.Push,
                IngestionConflictPolicy.SuggestionsOnly,
                "configuration://data-rights",
                secretReference: null,
                Now).Value;
            byte[] payload = Enumerable.Range(0, 18_765)
                .Select(index => (byte)(index % 239))
                .ToArray();
            string hash = Sha256(payload);
            ObservationReceipt receipt = CreateReceipt(
                receiptId,
                propertyId,
                connectionId,
                payloadFileId,
                hash);
            ReservationSourceLink sourceLink = CreateLinkedSource(
                propertyId,
                reservationId,
                connectionId,
                receiptId,
                hash);
            dbContext.AdapterConnections.Add(connection);
            dbContext.ObservationReceipts.Add(receipt);
            dbContext.ReservationSourceLinks.Add(sourceLink);
            await dbContext.SaveChangesAsync();

            IRawPayloadStore rawPayloadStore =
                scope.ServiceProvider.GetRequiredService<IRawPayloadStore>();
            await rawPayloadStore.StoreAsync(
                new RawPayloadWrite(
                    payloadFileId,
                    TenantId,
                    connectionId,
                    "application/json",
                    payload,
                    hash),
                CancellationToken.None);

            IDataRightsSubjectDiscoveryContributor discovery = scope.ServiceProvider
                .GetServices<IDataRightsSubjectDiscoveryContributor>()
                .Single(contributor => contributor.OwnerKey == "ingestion");
            DataRightsSubjectDiscoveryResult discovered = await discovery.DiscoverAsync(
                new DataRightsSubjectDiscoveryRequest(
                    TenantId,
                    propertyId,
                    new DataRightsSubjectLookup(
                        reservationId,
                        Email: null,
                        Phone: null,
                        Name: null,
                        DateOfBirth: null),
                    DataRightsSubjectDiscoveryLimits.MaxCandidates),
                CancellationToken.None);
            DataRightsSubjectCandidate candidate = Assert.Single(discovered.Candidates);

            IDataRightsSubjectExportContributor exporter = scope.ServiceProvider
                .GetServices<IDataRightsSubjectExportContributor>()
                .Single(contributor => contributor.OwnerKey == "ingestion");
            CollectingSink sink = new();
            DataRightsSubjectExportResult exported = await exporter.ExportAsync(
                new DataRightsSubjectExportRequest(
                    TenantId,
                    propertyId,
                    candidate.Coordinate),
                sink,
                CancellationToken.None);

            Assert.Equal(DataRightsSubjectExportStatus.Succeeded, exported.Status);
            Assert.Equal(exported.RecordCount, sink.Records.Count);
            Assert.Equal(
                sourceLink.SourceReference,
                Field(
                    Assert.Single(
                        sink.Records,
                        record => record.RecordType == "reservation-source-link"),
                    "ingestion.operations.source-reference").GetString());
            byte[] reconstructed = sink.Records
                .Where(record =>
                    record.RecordType ==
                    "ingestion-raw-payload-chunk")
                .OrderBy(record =>
                    Field(record, "ingestion.operations.raw-payload-chunk-index")
                        .GetInt32())
                .SelectMany(record =>
                    Field(record, "ingestion.raw-source.content")
                        .GetBytesFromBase64())
                .ToArray();
            Assert.Equal(payload, reconstructed);

            IIngestionAnonymisationEligibilityEvaluator evaluator =
                scope.ServiceProvider.GetRequiredService<
                    IIngestionAnonymisationEligibilityEvaluator>();
            IngestionAnonymisationEligibilityResult eligibility =
                await evaluator.EvaluateAsync(
                    new(
                        IngestionAnonymisationEligibilityContract.CurrentVersion,
                        TenantId,
                        Guid.NewGuid(),
                        ApprovalRevision: 1,
                        OperationRevision: 2,
                        propertyId,
                        sourceLink.Id,
                        sourceLink.Version,
                        new(
                            PropertyPolicySourceVersion: 1,
                            "GB",
                            "integration-hostel-baseline",
                            PolicyVersion: 1,
                            "integration-guest-operational",
                            RetentionPolicyVersion: 1,
                            new string('a', 64),
                            "data-rights-anonymisation",
                            "erasure",
                            "authorized-workspace-operator",
                            Now)),
                    CancellationToken.None);
            Assert.Equal(
                IngestionAnonymisationBlockerCode.PropertyPolicyUnavailable,
                eligibility.BlockerCode);
        }
        finally
        {
            if (Directory.Exists(fileRoot))
            {
                Directory.Delete(fileRoot, recursive: true);
            }
        }
    }

    private static ServiceProvider CreatePersistenceProvider(
        string connectionString,
        string fileRoot)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = connectionString;
        builder.Configuration["FileManagement:Enabled"] = "true";
        builder.Configuration["FileManagement:Provider"] = "LocalStorage";
        builder.Configuration["FileManagement:MaximumObjectBytes"] = "5242880";
        builder.Configuration["FileManagement:AllowedContentTypes:0"] = "application/json";
        builder.Configuration["FileManagement:LocalStorage:RootPath"] = fileRoot;
        builder.Services.AddSingleton<IScopeContext>(new TestScopeContext(TenantId));
        builder.Services.AddSingleton<ISystemClock>(new TestClock());
        CountryPolicyIntegrationTestData.InstallRegistry(builder.Services);
        builder.AddLocalFileStorage();
        builder.Services.AddIngestionApplication();
        builder.AddIngestionPersistence();
        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
    }

    private static ReservationSourceLink CreateLinkedSource(
        Guid propertyId,
        Guid reservationId,
        Guid connectionId,
        Guid receiptId,
        string hash)
    {
        ReservationSourceLink sourceLink = ReservationSourceLink.Create(
            Guid.NewGuid(),
            TenantId,
            propertyId,
            connectionId,
            "booking-com",
            "provider-42",
            Now).Value;
        Assert.True(sourceLink.Observe(
            receiptId,
            "revision-1",
            1,
            Now,
            hash,
            Now).IsSuccess);
        Guid operationId = Guid.NewGuid();
        Assert.True(sourceLink.BeginDispatch(operationId, Now).IsSuccess);
        Assert.True(sourceLink.CompleteDispatch(
            operationId,
            receiptId,
            "revision-1",
            1,
            "{\"primaryGuestName\":\"Maya Chen\"}",
            reservationId,
            1,
            keepActive: false,
            applied: true,
            cancellationPending: false,
            cancelled: false,
            Now).IsSuccess);
        return sourceLink;
    }

    private static ObservationReceipt CreateReceipt(
        Guid receiptId,
        Guid propertyId,
        Guid connectionId,
        Guid payloadFileId,
        string hash)
    {
        ObservationCountryPolicyEvidence evidence =
            ObservationCountryPolicyEvidence.Create(
                "GB",
                "policy",
                1,
                "eu",
                "eu-only",
                "retention",
                1,
                new string('a', 64),
                "reservation-import",
                "adapter-ingress",
                "property-policy",
                Now.AddDays(-1),
                Now.AddDays(30),
                Now).Value;
        ObservationReceipt receipt = ObservationReceipt.Create(
            receiptId,
            TenantId,
            propertyId,
            connectionId,
            runId: null,
            Guid.NewGuid(),
            "reservation",
            "provider-42",
            "revision-1",
            "reservation:provider-42:revision-1",
            hash,
            evidence,
            payloadFileId,
            Now.AddDays(30),
            Now,
            Now,
            Now).Value;
        Assert.True(receipt.MarkProcessed(Now.AddMinutes(1)).IsSuccess);
        return receipt;
    }

    private static System.Text.Json.JsonElement Field(
        DataRightsExportRecord record,
        string fieldId) =>
        Assert.Single(record.Fields, field => field.FieldId == fieldId).Value;

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

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

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}

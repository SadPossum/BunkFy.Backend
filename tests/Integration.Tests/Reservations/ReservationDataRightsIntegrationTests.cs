namespace Integration.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Persistence;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Framework.Tenancy;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class ReservationDataRightsIntegrationTests
{
    private const string TenantId = "a5000000-0000-0000-0000-000000000001";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Discovery_and_export_use_registered_contributors_on_postgresql()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_reservation_data_rights_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid propertyId = Guid.NewGuid();
        Reservation reservation = CreateReservation(propertyId);
        using ServiceProvider provider = CreatePersistenceProvider(postgreSql.GetConnectionString());
        using IServiceScope scope = provider.CreateScope();
        ReservationsDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ReservationsDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO reservations.property_projection
                 ("Id", "ScopeId", "TimeZoneId", "IsActive", "IsKnown",
                  "ProcessingStatus", "TopologySourceVersion", "PolicySourceVersion")
             VALUES
                 ({propertyId}, {"tenant-a"}, {"UTC"}, {true}, {true}, {1}, {1L}, {0L});
             """);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        IDataRightsSubjectDiscoveryContributor discovery = scope.ServiceProvider
            .GetServices<IDataRightsSubjectDiscoveryContributor>()
            .Single(contributor => contributor.OwnerKey == "reservations");
        DataRightsSubjectDiscoveryResult discovered = await discovery.DiscoverAsync(
            new(
                "tenant-a",
                propertyId,
                new(null, " maya.chen@example.test ", null, "maya chen", null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);
        DataRightsSubjectCandidate candidate = Assert.Single(discovered.Candidates);

        IDataRightsSubjectExportContributor exporter = scope.ServiceProvider
            .GetServices<IDataRightsSubjectExportContributor>()
            .Single(contributor => contributor.OwnerKey == "reservations");
        CollectingSink sink = new();
        DataRightsSubjectExportResult exported = await exporter.ExportAsync(
            new(
                "tenant-a",
                propertyId,
                candidate.Coordinate),
            sink,
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectDiscoveryStatus.Succeeded, discovered.Status);
        Assert.Equal(DataRightsSubjectExportStatus.Succeeded, exported.Status);
        DataRightsExportRecord record = Assert.Single(sink.Records);
        Assert.Equal("reservation", record.RecordType);
        Assert.Equal(
            "maya.chen@example.test",
            Assert.Single(
                record.Fields,
                field => field.FieldId == "reservation.guest.email").Value.GetString());
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Approved_correction_on_retired_property_commits_and_replays_atomically()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_reservation_data_rights_correction_tests")
            .Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            AuthTestContainers.GetNatsConnectionString(nats));
        await MigrateCorrectionDatabasesAsync(api).ConfigureAwait(false);

        Guid propertyId = Guid.NewGuid();
        (Reservation reservation, DataRightsCase dataRightsCase) =
            await SeedApprovedCorrectionAsync(api, propertyId).ConfigureAwait(false);
        Guid idempotencyKey = Guid.NewGuid();
        ApplyReservationDataRightsCorrectionCommand command = new(
            idempotencyKey,
            propertyId,
            dataRightsCase.Id,
            dataRightsCase.DecisionRevision!.Value,
            reservation.Id,
            reservation.Version,
            reservation.DetailsRevision,
            "Corrected Guest",
            "corrected@example.test",
            "+44 20 9999 0000",
            reservation.GuestCount,
            "Corrected through an approved rights request.",
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime,
            "user:privacy-operator");

        using IServiceScope commandScope = api.Services.CreateScope();
        commandScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(TenantId);
        IRequestDispatcher dispatcher =
            commandScope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<ReservationDto> ordinaryManagement = await dispatcher.SendAsync(
            new UpdateReservationGuestDetailsCommand(
                propertyId,
                reservation.Id,
                "Ordinary Update",
                reservation.Email,
                reservation.Phone,
                reservation.GuestCount,
                reservation.Notes,
                reservation.DetailsRevision,
                ReservationDetailsChangeOriginKind.Staff,
                "user:operator",
                reservation.ExpectedArrivalTime,
                reservation.ExpectedDepartureTime),
            CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(
            ReservationsApplicationErrors.CountryPolicyDenied(
                BunkFy.DataGovernance.CountryPolicyDecisionReason.MissingBinding),
            ordinaryManagement.Error);

        Result<ReservationDataRightsCorrectionReceiptDto> first =
            await dispatcher.SendAsync(command, CancellationToken.None).ConfigureAwait(false);
        Assert.True(first.IsSuccess, first.Error.Code);

        Result<ReservationDataRightsCorrectionReceiptDto> replay =
            await dispatcher.SendAsync(command, CancellationToken.None).ConfigureAwait(false);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(first.Value.ContractVersion, replay.Value.ContractVersion);
        Assert.Equal(first.Value.ReceiptId, replay.Value.ReceiptId);
        Assert.Equal(first.Value.CaseId, replay.Value.CaseId);
        Assert.Equal(first.Value.ApprovalRevision, replay.Value.ApprovalRevision);
        Assert.Equal(first.Value.ReservationId, replay.Value.ReservationId);
        Assert.Equal(first.Value.PreviousVersion, replay.Value.PreviousVersion);
        Assert.Equal(first.Value.CurrentVersion, replay.Value.CurrentVersion);
        Assert.Equal(first.Value.PreviousDetailsRevision, replay.Value.PreviousDetailsRevision);
        Assert.Equal(first.Value.CurrentDetailsRevision, replay.Value.CurrentDetailsRevision);
        Assert.Equal(first.Value.ChangedFields, replay.Value.ChangedFields);
        Assert.Equal(first.Value.DetailsChangeEventId, replay.Value.DetailsChangeEventId);
        Assert.Equal(first.Value.EventId, replay.Value.EventId);
        Assert.Equal(first.Value.CompletedAtUtc, replay.Value.CompletedAtUtc);

        Result<ReservationDataRightsCorrectionReceiptDto> conflict =
            await dispatcher.SendAsync(
                command with { PrimaryGuestName = "Different Reuse" },
                CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(ReservationsApplicationErrors.CorrectionIdempotencyConflict, conflict.Error);

        using IServiceScope verificationScope = api.Services.CreateScope();
        verificationScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(TenantId);
        ReservationsDbContext reservations =
            verificationScope.ServiceProvider.GetRequiredService<ReservationsDbContext>();
        Reservation persisted = await reservations.Reservations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == reservation.Id)
            .ConfigureAwait(false);
        ReservationDetailsHistoryEntry history = await reservations.ReservationDetailsHistory
            .AsNoTracking()
            .SingleAsync()
            .ConfigureAwait(false);
        ReservationDataRightsCorrectionReceipt receipt =
            await reservations.DataRightsCorrectionReceipts
            .AsNoTracking()
            .SingleAsync()
            .ConfigureAwait(false);
        Gma.Framework.Messaging.Infrastructure.OutboxMessage correctionEvent = Assert.Single(
            await reservations.OutboxMessages
                .AsNoTracking()
                .ToArrayAsync()
                .ConfigureAwait(false),
            message => message.EventType.Contains(
                "ReservationDataRightsCorrectionApplied",
                StringComparison.Ordinal));

        Assert.Equal("Corrected Guest", persisted.PrimaryGuestName);
        Assert.Equal(reservation.Version + 1, persisted.Version);
        Assert.Equal(reservation.DetailsRevision + 1, persisted.DetailsRevision);
        Assert.Equal(first.Value.DetailsChangeEventId, history.Id);
        Assert.Equal(ReservationDetailsChangeOrigin.DataRightsCorrection, history.Origin);
        Assert.Equal(first.Value.ReceiptId, receipt.Id);
        Assert.Equal(dataRightsCase.Id, receipt.CaseId);
        Assert.Equal(reservation.Version, receipt.SelectedRecordVersion);
        Assert.Equal(persisted.Version, receipt.CurrentRecordVersion);
        Assert.Equal(reservation.DetailsRevision, receipt.SelectedDetailsRevision);
        Assert.Equal(persisted.DetailsRevision, receipt.CurrentDetailsRevision);
        Assert.Equal(first.Value.EventId, correctionEvent.Id);
        Assert.DoesNotContain("Corrected Guest", correctionEvent.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("corrected@example.test", correctionEvent.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("+44 20 9999 0000", correctionEvent.Payload, StringComparison.Ordinal);
    }

    private static async Task MigrateCorrectionDatabasesAsync(AuthTestApplication api)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        await scope.ServiceProvider.GetRequiredService<ReservationsDbContext>()
            .Database.MigrateAsync()
            .ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<DataRightsDbContext>()
            .Database.MigrateAsync()
            .ConfigureAwait(false);
    }

    private static async Task<(Reservation Reservation, DataRightsCase Case)>
        SeedApprovedCorrectionAsync(AuthTestApplication api, Guid propertyId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        ReservationsDbContext reservations =
            scope.ServiceProvider.GetRequiredService<ReservationsDbContext>();
        PropertyCreatedIntegrationEvent propertyCreated = new(
            Guid.NewGuid(),
            TenantId,
            Now.AddDays(-2),
            propertyId,
            "Correction House",
            "correction-house",
            "UTC",
            PropertyStatus.Active,
            1);
        await ResolveHandler<PropertyCreatedIntegrationEvent>(
                scope.ServiceProvider,
                ReservationsModuleMetadata.Name)
            .HandleAsync(propertyCreated, CancellationToken.None)
            .ConfigureAwait(false);
        await CountryPolicyIntegrationTestData.ApplyActivationAsync(
            scope.ServiceProvider,
            ReservationsModuleMetadata.Name,
            TenantId,
            propertyId,
            2).ConfigureAwait(false);
        await ResolveHandler<PropertyRetiredIntegrationEvent>(
                scope.ServiceProvider,
                ReservationsModuleMetadata.Name)
            .HandleAsync(
                new(
                    Guid.NewGuid(),
                    TenantId,
                    Now.AddDays(-1),
                    propertyId,
                    3,
                    "user:property-admin"),
                CancellationToken.None)
            .ConfigureAwait(false);

        Reservation reservation = CreateReservation(propertyId, TenantId);
        reservations.Reservations.Add(reservation);
        await reservations.SaveChangesAsync().ConfigureAwait(false);

        DataRightsCaseRequest caseRequest = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Correction,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            TenantId,
            caseRequest,
            "user:privacy-reviewer",
            Now.AddMinutes(1)).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            dataRightsCase.Version,
            "user:privacy-reviewer",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            ReservationsDataRightsCoordinates.Owner,
            ReservationsDataRightsCoordinates.ReservationRecordType,
            reservation.Id,
            reservation.Version,
            dataRightsCase.Version,
            "user:privacy-reviewer",
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            dataRightsCase.Version,
            "user:privacy-reviewer",
            Now.AddMinutes(4)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            dataRightsCase.Version,
            "user:decision-maker",
            Now.AddMinutes(5)).IsSuccess);
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            dataRightsCase.Version,
            "user:decision-maker",
            Now.AddMinutes(6)).IsSuccess);
        DataRightsDbContext dataRights =
            scope.ServiceProvider.GetRequiredService<DataRightsDbContext>();
        dataRights.Cases.Add(dataRightsCase);
        await dataRights.SaveChangesAsync().ConfigureAwait(false);
        return (reservation, dataRightsCase);
    }

    private static IIntegrationEventHandler<TEvent> ResolveHandler<TEvent>(
        IServiceProvider services,
        string consumerModule)
        where TEvent : IIntegrationEvent
    {
        IntegrationEventSubscription subscription = services
            .GetRequiredService<IIntegrationEventSubscriptionRegistry>()
            .Subscriptions
            .Single(item =>
                item.ConsumerModule == consumerModule &&
                item.EventType == typeof(TEvent));
        return (IIntegrationEventHandler<TEvent>)services.GetRequiredService(
            subscription.HandlerType);
    }

    private static Reservation CreateReservation(Guid propertyId) =>
        CreateReservation(propertyId, "tenant-a");

    private static Reservation CreateReservation(Guid propertyId, string tenantId) =>
        Reservation.Create(
            Guid.NewGuid(),
            tenantId,
            propertyId,
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            "Maya Chen",
            "maya.chen@example.test",
            "+44 20 1234 5678",
            guestCount: 1,
            ReservationSource.Direct,
            sourceSystem: null,
            sourceReference: null,
            notes: "Late arrival",
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            initialDetailsActorId: null,
            initialAdapterConnectionId: null,
            initialExternalOperationId: null,
            Guid.NewGuid(),
            Now).Value;

    private static ServiceProvider CreatePersistenceProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = connectionString;
        builder.Services.AddSingleton<IScopeContext>(new TestScopeContext("tenant-a"));
        builder.AddReservationsPersistence();
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

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}

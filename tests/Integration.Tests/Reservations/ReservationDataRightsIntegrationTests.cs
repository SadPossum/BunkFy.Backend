namespace Integration.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class ReservationDataRightsIntegrationTests
{
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

    private static Reservation CreateReservation(Guid propertyId) =>
        Reservation.Create(
            Guid.NewGuid(),
            "tenant-a",
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

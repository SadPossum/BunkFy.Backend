namespace BunkFy.Modules.Reservations.Tests.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationDataRightsExportContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Export_writes_every_owned_record_once_and_excludes_unrelated_rows()
    {
        await using ReservationsDbContext dbContext = CreateDbContext("tenant-a");
        Guid propertyId = AddKnownProperty(dbContext);
        Guid otherPropertyId = AddKnownProperty(dbContext);
        Reservation reservation = CreateReservation(propertyId, "Maya Chen");
        Assert.True(reservation.ConfirmAllocation(
            reservation.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(reservation.LinkGuest(
            Guid.NewGuid(),
            ReservationGuestRole.Primary,
            replaceExistingRole: false,
            reservation.Version,
            "staff:test",
            Guid.NewGuid(),
            Now.AddMinutes(2)).IsSuccess);
        Guid adapterConnectionId = Guid.NewGuid();
        Guid externalOperationId = Guid.NewGuid();
        Assert.True(reservation.BeginAllocationAmendment(
            Guid.NewGuid(),
            new string('a', Reservation.RequestFingerprintLength),
            reservation.Arrival,
            reservation.Departure.AddDays(1),
            reservation.RequestedUnits.Select(unit => unit.InventoryUnitId).ToArray(),
            "Maya Chen Updated",
            "maya.updated@example.test",
            "+44 20 9999 4321",
            reservation.GuestCount,
            "Adapter note",
            reservation.DetailsRevision,
            ReservationDetailsChangeOrigin.Adapter,
            "adapter:test",
            adapterConnectionId,
            externalOperationId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(3)).IsSuccess);
        dbContext.Reservations.Add(reservation);
        dbContext.Reservations.Add(CreateReservation(otherPropertyId, "Other Guest"));

        dbContext.ReservationDetailsHistory.AddRange(
            CreateHistory(reservation, fromRevision: 0, toRevision: 1, Now),
            CreateHistory(reservation, fromRevision: 1, toRevision: 2, Now.AddMinutes(1)));
        dbContext.ReservationDetailsHistory.Add(
            CreateHistory(
                CreateReservation(otherPropertyId, "Unrelated History"),
                fromRevision: 0,
                toRevision: 1,
                Now));
        ReservationDataRightsCorrectionReceipt correctionReceipt =
            ReservationDataRightsCorrectionReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                propertyId,
                Guid.NewGuid(),
                approvalRevision: 1,
                reservation.Id,
                new(
                    PreviousRecordVersion: 1,
                    CurrentRecordVersion: 2,
                    PreviousDetailsRevision: 0,
                    CurrentDetailsRevision: 1,
                    [ReservationDetailsField.Email],
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Now.AddMinutes(3)),
                Guid.NewGuid()).Value;
        dbContext.DataRightsCorrectionReceipts.Add(correctionReceipt);

        dbContext.ExternalOperations.Add(new ReservationExternalOperation(
            new ReservationExternalOperationRecord(
                externalOperationId,
                "tenant-a",
                Guid.NewGuid(),
                adapterConnectionId,
                propertyId,
                ExternalReservationOperationKind.Amend,
                new string('b', Reservation.RequestFingerprintLength),
                ExternalReservationOperationOutcome.Accepted,
                reservation.Id,
                reservation.DetailsRevision,
                reservation.Version,
                ErrorCode: null,
                Now.AddMinutes(4))));
        dbContext.ExternalOperations.Add(new ReservationExternalOperation(
            new ReservationExternalOperationRecord(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                otherPropertyId,
                ExternalReservationOperationKind.Create,
                new string('c', Reservation.RequestFingerprintLength),
                ExternalReservationOperationOutcome.Applied,
                Guid.NewGuid(),
                DetailsRevision: 1,
                ReservationVersion: 1,
                ErrorCode: null,
                Now)));

        dbContext.ArrivalReminders.Add(ReservationArrivalReminder.Create(
            Guid.NewGuid(),
            "tenant-a",
            reservation.Id,
            propertyId,
            reservation.DetailsRevision,
            "Europe/Moscow",
            reservation.Arrival,
            new TimeOnly(15, 0),
            Now.AddDays(1),
            Now.AddHours(22),
            leadTimeMinutes: 120));
        dbContext.ArrivalReminders.Add(ReservationArrivalReminder.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            otherPropertyId,
            detailsRevision: 1,
            "UTC",
            new DateOnly(2026, 8, 1),
            new TimeOnly(15, 0),
            Now.AddDays(1),
            Now.AddHours(22),
            leadTimeMinutes: 120));
        await dbContext.SaveChangesAsync();

        ReservationDataRightsExportContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));
        CollectingSink sink = new();

        DataRightsSubjectExportResult result = await contributor.ExportAsync(
            CreateRequest(propertyId, reservation),
            sink,
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectExportStatus.Succeeded, result.Status);
        Assert.Equal(8, result.RecordCount);
        Assert.Equal(8, sink.Records.Count);
        Assert.Equal(
            [
                ReservationDataRightsDiscoveryContributor.ReservationRecordType,
                ReservationDataRightsExportContributor.PendingAmendmentRecordType,
                ReservationDataRightsExportContributor.GuestLinkRecordType,
                ReservationDataRightsExportContributor.DetailsHistoryRecordType,
                ReservationDataRightsExportContributor.DetailsHistoryRecordType,
                ReservationDataRightsExportContributor.DataRightsCorrectionReceiptRecordType,
                ReservationDataRightsExportContributor.ExternalOperationRecordType,
                ReservationDataRightsExportContributor.ArrivalReminderRecordType
            ],
            sink.Records.Select(record => record.RecordType));
        Assert.DoesNotContain(
            sink.Records.SelectMany(record => record.Fields),
            field => field.FieldId == "reservation.audit.actor-id");
        DataRightsExportRecord correctionExport = Assert.Single(
            sink.Records,
            record => record.RecordType ==
                ReservationDataRightsExportContributor.DataRightsCorrectionReceiptRecordType);
        Assert.DoesNotContain(
            correctionExport.Fields,
            field => field.FieldId is
                "reservation.data-rights.idempotency-key" or
                "reservation.data-rights.correlation-id");
        Assert.All(
            sink.Records,
            record => Assert.InRange(
                record.Fields.Count,
                1,
                DataRightsExportLimits.MaxFieldsPerRecord));
    }

    [Fact]
    public async Task Export_rejects_stale_cross_property_and_unknown_scope_without_writes()
    {
        await using ReservationsDbContext dbContext = CreateDbContext("tenant-a");
        Guid propertyId = AddKnownProperty(dbContext);
        Guid otherPropertyId = AddKnownProperty(dbContext);
        Reservation reservation = CreateReservation(propertyId, "Guest");
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();
        ReservationDataRightsExportContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));

        CollectingSink staleSink = new();
        DataRightsSubjectExportResult stale = await contributor.ExportAsync(
            CreateRequest(propertyId, reservation) with
            {
                Coordinate = CreateRequest(propertyId, reservation).Coordinate with
                {
                    RecordVersion = reservation.Version + 1
                }
            },
            staleSink,
            CancellationToken.None);
        CollectingSink otherPropertySink = new();
        DataRightsSubjectExportResult otherProperty = await contributor.ExportAsync(
            CreateRequest(otherPropertyId, reservation),
            otherPropertySink,
            CancellationToken.None);
        CollectingSink unknownPropertySink = new();
        DataRightsSubjectExportResult unknownProperty = await contributor.ExportAsync(
            CreateRequest(Guid.NewGuid(), reservation),
            unknownPropertySink,
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectExportStatus.Stale, stale.Status);
        Assert.Equal(DataRightsSubjectExportStatus.NotFound, otherProperty.Status);
        Assert.Equal(DataRightsSubjectExportStatus.ScopeUnavailable, unknownProperty.Status);
        Assert.Empty(staleSink.Records);
        Assert.Empty(otherPropertySink.Records);
        Assert.Empty(unknownPropertySink.Records);
    }

    [Fact]
    public async Task Sink_failure_propagates_without_a_false_success_result()
    {
        await using ReservationsDbContext dbContext = CreateDbContext("tenant-a");
        Guid propertyId = AddKnownProperty(dbContext);
        Reservation reservation = CreateReservation(propertyId, "Guest");
        dbContext.Reservations.Add(reservation);
        dbContext.ReservationDetailsHistory.Add(CreateHistory(
            reservation,
            fromRevision: 0,
            toRevision: 1,
            Now));
        await dbContext.SaveChangesAsync();
        ReservationDataRightsExportContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));
        FailingSink sink = new(failOnWrite: 2);

        await Assert.ThrowsAsync<IOException>(() => contributor.ExportAsync(
            CreateRequest(propertyId, reservation),
            sink,
            CancellationToken.None));

        Assert.Single(sink.Records);
    }

    [Fact]
    public void Export_schema_is_catalogue_bound_and_versioned()
    {
        ReservationDataRightsExportSchema.EnsureValid();

        DataRightsExportDescriptor descriptor =
            ReservationDataRightsExportSchema.Descriptor;
        Assert.Equal(ReservationDataRightsDiscoveryContributor.Owner, descriptor.OwnerKey);
        Assert.Equal("reservations.personal-data", descriptor.CatalogId);
        Assert.Equal(4, descriptor.CatalogVersion);
        Assert.Equal("reservations.subject-export", descriptor.ExportSchemaId);
        Assert.NotEmpty(descriptor.FieldIds);
    }

    private static DataRightsSubjectExportRequest CreateRequest(
        Guid propertyId,
        Reservation reservation) =>
        new(
            "tenant-a",
            propertyId,
            new(
                ReservationDataRightsDiscoveryContributor.Owner,
                ReservationDataRightsDiscoveryContributor.ReservationRecordType,
                reservation.Id,
                reservation.Version));

    private static ReservationDetailsHistoryEntry CreateHistory(
        Reservation reservation,
        long fromRevision,
        long toRevision,
        DateTimeOffset occurredAtUtc) =>
        new(
            Guid.NewGuid(),
            "tenant-a",
            reservation.Id,
            reservation.PropertyId,
            fromRevision,
            toRevision,
            ReservationDetailsChangeOrigin.Staff,
            actorId: "staff:test",
            adapterConnectionId: null,
            externalOperationId: null,
            operationDeduplicationKey: new string('d', 64),
            Guid.NewGuid(),
            changedFieldsJson: "[\"PrimaryGuestName\"]",
            beforeSnapshotJson: fromRevision == 0 ? null : "{\"primaryGuestName\":\"Before\"}",
            afterSnapshotJson: "{\"primaryGuestName\":\"After\"}",
            afterSnapshotHash: new string('e', 64),
            occurredAtUtc);

    private static Reservation CreateReservation(
        Guid propertyId,
        string primaryGuestName) =>
        Reservation.Create(
            Guid.NewGuid(),
            "tenant-a",
            propertyId,
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            primaryGuestName,
            "guest@example.test",
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

    private static Guid AddKnownProperty(ReservationsDbContext dbContext)
    {
        Guid propertyId = Guid.NewGuid();
        ReservationPropertyProjection property =
            ReservationPropertyProjection.Create(propertyId, "tenant-a");
        property.ApplyTopology("UTC", isActive: true, sourceVersion: 1);
        dbContext.PropertyProjections.Add(property);
        return propertyId;
    }

    private static ReservationsDbContext CreateDbContext(string tenantId)
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase($"reservations-data-rights-export-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext(tenantId));
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

    private sealed class FailingSink(int failOnWrite) : IDataRightsExportSink
    {
        private int writeCount;

        public List<DataRightsExportRecord> Records { get; } = [];

        public ValueTask WriteAsync(
            DataRightsExportRecord record,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.writeCount++;
            if (this.writeCount == failOnWrite)
            {
                throw new IOException("The test sink failed.");
            }

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

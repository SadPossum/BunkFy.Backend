namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Entities;
using BunkFy.Modules.Reservations.Persistence;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationsModelTests
{
    [Fact]
    public void Model_has_scoped_requested_units_idempotency_and_version_concurrency()
    {
        using ReservationsDbContext dbContext = CreateDbContext();
        IEntityType reservationEntity = dbContext.Model.FindEntityType(typeof(Reservation))!;
        IEntityType reservationDesignEntity = dbContext.GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(Reservation))!;
        IEntityType unitEntity = dbContext.Model.FindEntityType(typeof(RequestedInventoryUnit))!;
        IForeignKey parent = Assert.Single(unitEntity.GetForeignKeys(), candidate => candidate.PrincipalEntityType == reservationEntity);

        Assert.True(reservationEntity.FindProperty(nameof(Reservation.Version))!.IsConcurrencyToken);
        Assert.NotNull(reservationEntity.FindProperty(nameof(Reservation.DetailsRevision)));
        Assert.NotNull(reservationEntity.FindProperty(nameof(Reservation.LastDetailsChangeOrigin)));
        Assert.Equal("time(0) without time zone", ColumnType(reservationDesignEntity, nameof(Reservation.ExpectedArrivalTime)));
        Assert.Equal("time(0) without time zone", ColumnType(reservationDesignEntity, nameof(Reservation.ExpectedDepartureTime)));
        Assert.Equal("time(0) without time zone", ColumnType(reservationDesignEntity, nameof(Reservation.PendingExpectedArrivalTime)));
        Assert.Equal("time(0) without time zone", ColumnType(reservationDesignEntity, nameof(Reservation.PendingExpectedDepartureTime)));
        Assert.Equal(
            Reservation.ActorIdMaxLength,
            reservationEntity.FindProperty(nameof(Reservation.PendingStayActorId))!.GetMaxLength());
        Assert.Equal(
            Reservation.ActorIdMaxLength,
            reservationEntity.FindProperty(nameof(Reservation.CheckedInBy))!.GetMaxLength());
        Assert.NotNull(reservationEntity.FindProperty(nameof(Reservation.NoShowBusinessDate)));
        Assert.NotNull(reservationEntity.FindProperty(nameof(Reservation.CheckedOutAtUtc)));
        string[] lifecycleConstraints =
        [
            "CK_reservations_pending_stay_complete",
            "CK_reservations_checked_in_complete",
            "CK_reservations_no_show_complete",
            "CK_reservations_checked_out_complete"
        ];
        Assert.All(
            lifecycleConstraints,
            constraint => Assert.Contains(
                reservationDesignEntity.GetCheckConstraints(),
                candidate => candidate.Name == constraint));
        Assert.Contains(
            reservationEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Reservation.ScopeId), nameof(Reservation.AllocationRequestId)]));
        Assert.Equal(["ScopeId", "ReservationId"], parent.Properties.Select(property => property.Name));
        Assert.Equal(["ScopeId", "Id"], parent.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, parent.DeleteBehavior);
    }

    [Fact]
    public void Model_has_scoped_revision_history_and_provider_agnostic_operation_deduplication()
    {
        using ReservationsDbContext dbContext = CreateDbContext();
        IEntityType history = dbContext.Model.FindEntityType(typeof(ReservationDetailsHistoryEntry))!;
        IEntityType externalOperation = dbContext.Model.FindEntityType(typeof(ReservationExternalOperation))!;

        Assert.Contains(
            history.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(ReservationDetailsHistoryEntry.ScopeId),
                    nameof(ReservationDetailsHistoryEntry.ReservationId),
                    nameof(ReservationDetailsHistoryEntry.ToRevision)
                ]));
        Assert.Contains(
            history.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(ReservationDetailsHistoryEntry.ScopeId),
                    nameof(ReservationDetailsHistoryEntry.OperationDeduplicationKey)
                ]));
        Assert.Null(history.GetIndexes().Single(index => index.Properties.Any(property =>
            property.Name == nameof(ReservationDetailsHistoryEntry.OperationDeduplicationKey))).GetFilter());
        Assert.Equal(
            [nameof(ReservationExternalOperation.ScopeId), nameof(ReservationExternalOperation.Id)],
            externalOperation.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(64, externalOperation.FindProperty(nameof(ReservationExternalOperation.RequestFingerprint))!.GetMaxLength());
        Assert.Contains(
            externalOperation.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(ReservationExternalOperation.ScopeId),
                    nameof(ReservationExternalOperation.ConnectionId),
                    nameof(ReservationExternalOperation.CompletedAtUtc)
                ]));
    }

    [Fact]
    public void Model_has_scoped_inventory_projection_children_and_versions()
    {
        using ReservationsDbContext dbContext = CreateDbContext();
        IEntityType unitEntity = dbContext.Model.FindEntityType(typeof(ReservationInventoryUnitProjection))!;
        IEntityType allocationEntity = dbContext.Model.FindEntityType(typeof(ReservationInventoryAllocationProjection))!;
        IEntityType allocationUnitEntity = dbContext.Model.FindEntityType(typeof(ReservationInventoryAllocationUnitProjection))!;
        IForeignKey parent = Assert.Single(
            allocationUnitEntity.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType == allocationEntity);

        Assert.True(unitEntity.FindProperty(nameof(ReservationInventoryUnitProjection.UnitVersion))!.IsConcurrencyToken);
        Assert.Equal(["ScopeId", "AllocationId"], parent.Properties.Select(property => property.Name));
        Assert.Equal(["ScopeId", "Id"], parent.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, parent.DeleteBehavior);
    }

    [Fact]
    public void Model_has_indexed_revision_bound_arrival_reminders()
    {
        using ReservationsDbContext dbContext = CreateDbContext();
        IEntityType reminder = dbContext.Model.FindEntityType(typeof(ReservationArrivalReminder))!;
        IEntityType property = dbContext.Model.FindEntityType(typeof(ReservationPropertyProjection))!;

        Assert.True(reminder.FindProperty(nameof(ReservationArrivalReminder.Version))!.IsConcurrencyToken);
        Assert.True(property.FindProperty(nameof(ReservationPropertyProjection.SourceVersion))!.IsConcurrencyToken);
        Assert.Contains(
            reminder.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(ReservationArrivalReminder.ScopeId),
                nameof(ReservationArrivalReminder.ReservationId),
                nameof(ReservationArrivalReminder.DetailsRevision),
                nameof(ReservationArrivalReminder.TimeZoneId),
                nameof(ReservationArrivalReminder.LeadTimeMinutes)
            ]));
        Assert.Contains(
            reminder.GetIndexes(),
            index => index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(ReservationArrivalReminder.ScopeId),
                nameof(ReservationArrivalReminder.State),
                nameof(ReservationArrivalReminder.DueAtUtc)
            ]));
    }

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options = new DbContextOptionsBuilder<ReservationsDbContext>()
            .UseInMemoryDatabase($"reservations-model-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private static string? ColumnType(IEntityType entityType, string propertyName) =>
        entityType.FindProperty(propertyName)?.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value as string;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

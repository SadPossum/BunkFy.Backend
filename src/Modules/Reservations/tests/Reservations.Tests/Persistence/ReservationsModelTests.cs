namespace Reservations.Tests;

using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Reservations.Domain.Aggregates;
using Reservations.Domain.Entities;
using Reservations.Persistence;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationsModelTests
{
    [Fact]
    public void Model_has_scoped_requested_units_idempotency_and_version_concurrency()
    {
        using ReservationsDbContext dbContext = CreateDbContext();
        IEntityType reservationEntity = dbContext.Model.FindEntityType(typeof(Reservation))!;
        IEntityType unitEntity = dbContext.Model.FindEntityType(typeof(RequestedInventoryUnit))!;
        IForeignKey parent = Assert.Single(unitEntity.GetForeignKeys(), candidate => candidate.PrincipalEntityType == reservationEntity);

        Assert.True(reservationEntity.FindProperty(nameof(Reservation.Version))!.IsConcurrencyToken);
        Assert.Contains(
            reservationEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Reservation.ScopeId), nameof(Reservation.AllocationRequestId)]));
        Assert.Equal(["ScopeId", "ReservationId"], parent.Properties.Select(property => property.Name));
        Assert.Equal(["ScopeId", "Id"], parent.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, parent.DeleteBehavior);
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

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options = new DbContextOptionsBuilder<ReservationsDbContext>()
            .UseInMemoryDatabase($"reservations-model-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

namespace BunkFy.Modules.Reservations.Persistence;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Entities;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
public sealed class ReservationsDbContext(DbContextOptions<ReservationsDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<ReservationsDbContext>(options, scopeContext)
{
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<Reservation> Reservations => this.Set<Reservation>();
    public DbSet<RequestedInventoryUnit> RequestedInventoryUnits => this.Set<RequestedInventoryUnit>();
    public DbSet<ReservationGuest> ReservationGuests => this.Set<ReservationGuest>();
    public DbSet<ReservationGuestProfileProjection> GuestProfileProjections => this.Set<ReservationGuestProfileProjection>();
    public DbSet<ReservationGuestProcessingRestrictionProjection> GuestProcessingRestrictionProjections =>
        this.Set<ReservationGuestProcessingRestrictionProjection>();
    public DbSet<ReservationDetailsHistoryEntry> ReservationDetailsHistory => this.Set<ReservationDetailsHistoryEntry>();
    public DbSet<ReservationPropertyProjection> PropertyProjections => this.Set<ReservationPropertyProjection>();
    public DbSet<ReservationArrivalReminder> ArrivalReminders => this.Set<ReservationArrivalReminder>();
    public DbSet<ReservationExternalOperation> ExternalOperations => this.Set<ReservationExternalOperation>();
    public DbSet<ReservationInventoryUnitProjection> InventoryUnitProjections => this.Set<ReservationInventoryUnitProjection>();
    public DbSet<ReservationInventoryBlockProjection> InventoryBlockProjections => this.Set<ReservationInventoryBlockProjection>();
    public DbSet<ReservationInventoryAllocationProjection> InventoryAllocationProjections => this.Set<ReservationInventoryAllocationProjection>();
    public DbSet<ReservationInventoryAllocationUnitProjection> InventoryAllocationUnitProjections => this.Set<ReservationInventoryAllocationUnitProjection>();
    public DbSet<ReservationsProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints => this.Set<ReservationsProjectionRebuildCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(ReservationsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReservationsDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}

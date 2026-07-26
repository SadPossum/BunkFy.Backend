namespace BunkFy.Modules.Reservations.Persistence;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
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
    public DbSet<ReservationDataRightsCorrectionReceipt> DataRightsCorrectionReceipts =>
        this.Set<ReservationDataRightsCorrectionReceipt>();
    public DbSet<ReservationProcessingRestriction> ProcessingRestrictions =>
        this.Set<ReservationProcessingRestriction>();
    public DbSet<ReservationProcessingRestrictionReceipt> ProcessingRestrictionReceipts =>
        this.Set<ReservationProcessingRestrictionReceipt>();
    public DbSet<ReservationProcessingRestrictionProjection>
        ProcessingRestrictionProjections =>
        this.Set<ReservationProcessingRestrictionProjection>();
    public DbSet<ReservationDataHold> DataHolds =>
        this.Set<ReservationDataHold>();
    public DbSet<ReservationDataHoldReceipt> DataHoldReceipts =>
        this.Set<ReservationDataHoldReceipt>();
    public DbSet<ReservationAnonymisationReceipt> AnonymisationReceipts =>
        this.Set<ReservationAnonymisationReceipt>();
    public DbSet<ReservationAnonymisationTombstone>
        AnonymisationTombstones =>
        this.Set<ReservationAnonymisationTombstone>();
    public DbSet<ReservationAnonymisationRestoreReceipt>
        AnonymisationRestoreReceipts =>
        this.Set<ReservationAnonymisationRestoreReceipt>();
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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.EnsureDataRightsReceiptsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        this.EnsureDataRightsReceiptsAreAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(ReservationsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReservationsDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }

    private void EnsureDataRightsReceiptsAreAppendOnly()
    {
        bool correctionMutationRequested = this.ChangeTracker
            .Entries<ReservationDataRightsCorrectionReceipt>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (correctionMutationRequested)
        {
            throw new InvalidOperationException(
                "Reservation data-rights correction receipts are append-only.");
        }

        bool restrictionMutationRequested = this.ChangeTracker
            .Entries<ReservationProcessingRestrictionReceipt>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (restrictionMutationRequested)
        {
            throw new InvalidOperationException(
                "Reservation processing-restriction receipts are append-only.");
        }

        bool holdReceiptMutationRequested = this.ChangeTracker
            .Entries<ReservationDataHoldReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        if (holdReceiptMutationRequested)
        {
            throw new InvalidOperationException(
                "Reservation data-hold receipts are append-only.");
        }

        bool anonymisationReceiptMutationRequested = this.ChangeTracker
            .Entries<ReservationAnonymisationReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        if (anonymisationReceiptMutationRequested)
        {
            throw new InvalidOperationException(
                "Reservation anonymisation receipts are append-only.");
        }

        bool restoreReceiptMutationRequested = this.ChangeTracker
            .Entries<ReservationAnonymisationRestoreReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        if (restoreReceiptMutationRequested)
        {
            throw new InvalidOperationException(
                "Reservation anonymisation restore receipts are append-only.");
        }

        bool tombstoneDeletionRequested = this.ChangeTracker
            .Entries<ReservationAnonymisationTombstone>()
            .Any(entry => entry.State == EntityState.Deleted);
        if (tombstoneDeletionRequested)
        {
            throw new InvalidOperationException(
                "Reservation anonymisation tombstones cannot be deleted.");
        }
    }
}

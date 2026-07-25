namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Persistence.Models;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

public sealed class GuestsDbContext(DbContextOptions<GuestsDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<GuestsDbContext>(options, scopeContext)
{
    public DbSet<GuestProfile> GuestProfiles => this.Set<GuestProfile>();
    public DbSet<GuestDataRightsCorrectionReceipt> DataRightsCorrectionReceipts =>
        this.Set<GuestDataRightsCorrectionReceipt>();
    public DbSet<GuestProcessingRestriction> ProcessingRestrictions =>
        this.Set<GuestProcessingRestriction>();
    public DbSet<GuestProcessingRestrictionReceipt> ProcessingRestrictionReceipts =>
        this.Set<GuestProcessingRestrictionReceipt>();
    public DbSet<GuestProcessingRestrictionProjection> ProcessingRestrictionProjections =>
        this.Set<GuestProcessingRestrictionProjection>();
    public DbSet<GuestDataHold> DataHolds => this.Set<GuestDataHold>();
    public DbSet<GuestDataHoldReceipt> DataHoldReceipts => this.Set<GuestDataHoldReceipt>();
    public DbSet<GuestAnonymisationReceipt> AnonymisationReceipts =>
        this.Set<GuestAnonymisationReceipt>();
    public DbSet<GuestAnonymisationTombstone> AnonymisationTombstones =>
        this.Set<GuestAnonymisationTombstone>();
    internal DbSet<GuestOperationLock> OperationLocks => this.Set<GuestOperationLock>();
    public DbSet<GuestPropertyProjection> PropertyProjections => this.Set<GuestPropertyProjection>();
    public DbSet<GuestStayHistoryEntry> StayHistory => this.Set<GuestStayHistoryEntry>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<GuestsProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints => this.Set<GuestsProjectionRebuildCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(GuestsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GuestsDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}

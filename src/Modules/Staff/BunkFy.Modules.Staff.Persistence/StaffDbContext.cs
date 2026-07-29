namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Domain.Governance;
using BunkFy.Modules.Staff.Persistence.Models;

public sealed class StaffDbContext(DbContextOptions<StaffDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<StaffDbContext>(options, scopeContext)
{
    public DbSet<StaffMember> StaffMembers => this.Set<StaffMember>();
    public DbSet<StaffDataRightsCorrectionReceipt> DataRightsCorrectionReceipts =>
        this.Set<StaffDataRightsCorrectionReceipt>();
    public DbSet<StaffProcessingRestriction> ProcessingRestrictions =>
        this.Set<StaffProcessingRestriction>();
    public DbSet<StaffProcessingRestrictionProjection>
        ProcessingRestrictionProjections =>
        this.Set<StaffProcessingRestrictionProjection>();
    public DbSet<StaffProcessingRestrictionReceipt>
        ProcessingRestrictionReceipts =>
        this.Set<StaffProcessingRestrictionReceipt>();
    public DbSet<StaffEmploymentGovernance> EmploymentGovernance =>
        this.Set<StaffEmploymentGovernance>();
    public DbSet<StaffEmploymentGovernanceChangeReceipt>
        EmploymentGovernanceChangeReceipts =>
        this.Set<StaffEmploymentGovernanceChangeReceipt>();
    public DbSet<StaffDataHold> DataHolds =>
        this.Set<StaffDataHold>();
    public DbSet<StaffDataHoldReceipt> DataHoldReceipts =>
        this.Set<StaffDataHoldReceipt>();
    internal DbSet<StaffOperationLock> OperationLocks =>
        this.Set<StaffOperationLock>();
    public DbSet<StaffPropertyAssignment> PropertyAssignments => this.Set<StaffPropertyAssignment>();
    public DbSet<StaffPropertyProjection> PropertyProjections => this.Set<StaffPropertyProjection>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<StaffProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints =>
        this.Set<StaffProjectionRebuildCheckpoint>();

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
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(StaffMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StaffDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }

    private void EnsureDataRightsReceiptsAreAppendOnly()
    {
        bool correctionMutationRequested = this.ChangeTracker
            .Entries<StaffDataRightsCorrectionReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        bool restrictionMutationRequested = this.ChangeTracker
            .Entries<StaffProcessingRestrictionReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        bool governanceMutationRequested = this.ChangeTracker
            .Entries<StaffEmploymentGovernanceChangeReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        bool holdReceiptMutationRequested = this.ChangeTracker
            .Entries<StaffDataHoldReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        if (correctionMutationRequested ||
            restrictionMutationRequested ||
            governanceMutationRequested ||
            holdReceiptMutationRequested)
        {
            throw new InvalidOperationException(
                "Staff data-rights receipts are append-only.");
        }
    }
}

namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

public sealed class DataRightsDbContext(
    DbContextOptions<DataRightsDbContext> options,
    IScopeContext scopeContext) : ScopeAwareDbContext<DataRightsDbContext>(options, scopeContext)
{
    public DbSet<DataRightsCase> Cases => this.Set<DataRightsCase>();
    public DbSet<DataRightsCorrectionExecution> CorrectionExecutions =>
        this.Set<DataRightsCorrectionExecution>();
    public DbSet<DataRightsExecutionBatch> ExecutionBatches =>
        this.Set<DataRightsExecutionBatch>();
    public DbSet<DataRightsExportArtifact> ExportArtifacts =>
        this.Set<DataRightsExportArtifact>();
    public DbSet<DataRightsExportAuditEntry> ExportAuditEntries =>
        this.Set<DataRightsExportAuditEntry>();
    public DbSet<DataRightsExecutionWorkItem> ExecutionWorkItems =>
        this.Set<DataRightsExecutionWorkItem>();
    public DbSet<DataRightsProcessingLedgerEntry> ProcessingLedgerEntries =>
        this.Set<DataRightsProcessingLedgerEntry>();
    public DbSet<DataRightsRestoreCheckpoint> RestoreCheckpoints =>
        this.Set<DataRightsRestoreCheckpoint>();
    public DbSet<DataRightsPropertyProjection> PropertyProjections =>
        this.Set<DataRightsPropertyProjection>();
    public DbSet<DataRightsProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints =>
        this.Set<DataRightsProjectionRebuildCheckpoint>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.EnsureAppendOnlyEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        this.EnsureAppendOnlyEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DataRightsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataRightsDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }

    private void EnsureAppendOnlyEntities()
    {
        bool ledgerMutationRequested = this.ChangeTracker
            .Entries<DataRightsProcessingLedgerEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (ledgerMutationRequested)
        {
            throw new InvalidOperationException(
                "Data-rights processing ledger entries are append-only.");
        }

        bool exportAuditMutationRequested = this.ChangeTracker
            .Entries<DataRightsExportAuditEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (exportAuditMutationRequested)
        {
            throw new InvalidOperationException(
                "Data-rights export audit entries are append-only.");
        }
    }
}

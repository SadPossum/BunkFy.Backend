namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Retention;
using BunkFy.Modules.Guests.Persistence.Models;
using BunkFy.Modules.Guests.Persistence.TenantTermination;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public sealed class GuestsDbContext(
    DbContextOptions<GuestsDbContext> options,
    IScopeContext scopeContext,
    IWorkspaceTerminationFenceReader? terminationFences = null)
    : ScopeAwareDbContext<GuestsDbContext>(options, scopeContext)
{
    private readonly IScopeContext scopeContext = scopeContext;

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
    public DbSet<GuestAnonymisationRestoreReceipt>
        AnonymisationRestoreReceipts =>
            this.Set<GuestAnonymisationRestoreReceipt>();
    public DbSet<GuestRetentionExecution> RetentionExecutions =>
        this.Set<GuestRetentionExecution>();
    public DbSet<GuestRetentionSweepCheckpoint> RetentionSweepCheckpoints =>
        this.Set<GuestRetentionSweepCheckpoint>();
    public DbSet<GuestRetentionAnonymisationReceipt>
        RetentionAnonymisationReceipts =>
            this.Set<GuestRetentionAnonymisationReceipt>();
    internal DbSet<GuestOperationLock> OperationLocks => this.Set<GuestOperationLock>();
    public DbSet<GuestPropertyProjection> PropertyProjections => this.Set<GuestPropertyProjection>();
    public DbSet<GuestStayHistoryEntry> StayHistory => this.Set<GuestStayHistoryEntry>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<GuestsProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints => this.Set<GuestsProjectionRebuildCheckpoint>();
    internal DbSet<GuestsTenantRevision> TenantRevisions =>
        this.Set<GuestsTenantRevision>();
    internal DbSet<GuestsTenantDestroyOperation> TenantDestroyOperations =>
        this.Set<GuestsTenantDestroyOperation>();
    internal DbSet<GuestsTenantDestroyReceipt> TenantDestroyReceipts =>
        this.Set<GuestsTenantDestroyReceipt>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.EnsureImmutableReceiptsAreAppendOnly();
        return this.SaveChangesWithAdmissionAsync(
                acceptAllChangesOnSuccess,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        this.EnsureImmutableReceiptsAreAppendOnly();
        return this.SaveChangesWithAdmissionAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(GuestsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GuestsDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }

    private void EnsureImmutableReceiptsAreAppendOnly()
    {
        bool correctionMutationRequested = this.ChangeTracker
            .Entries<GuestDataRightsCorrectionReceipt>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (correctionMutationRequested)
        {
            throw new InvalidOperationException(
                "Guest data-rights correction receipts are append-only.");
        }

        bool restrictionMutationRequested = this.ChangeTracker
            .Entries<GuestProcessingRestrictionReceipt>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (restrictionMutationRequested)
        {
            throw new InvalidOperationException(
                "Guest processing-restriction receipts are append-only.");
        }

        bool holdMutationRequested = this.ChangeTracker
            .Entries<GuestDataHoldReceipt>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (holdMutationRequested)
        {
            throw new InvalidOperationException(
                "Guest data-hold receipts are append-only.");
        }

        bool anonymisationMutationRequested = this.ChangeTracker
            .Entries<GuestAnonymisationReceipt>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (anonymisationMutationRequested)
        {
            throw new InvalidOperationException(
                "Guest anonymisation receipts are append-only.");
        }

        bool restoreMutationRequested = this.ChangeTracker
            .Entries<GuestAnonymisationRestoreReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or
                    EntityState.Deleted);
        bool retentionMutationRequested = this.ChangeTracker
            .Entries<GuestRetentionAnonymisationReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or
                    EntityState.Deleted);
        if (restoreMutationRequested || retentionMutationRequested)
        {
            throw new InvalidOperationException(
                "Guest anonymisation receipts are append-only.");
        }

        bool tombstoneDeletionRequested = this.ChangeTracker
            .Entries<GuestAnonymisationTombstone>()
            .Any(entry => entry.State == EntityState.Deleted);
        if (tombstoneDeletionRequested)
        {
            throw new InvalidOperationException(
                "Guest anonymisation tombstones cannot be deleted.");
        }

        bool tenantDestroyReceiptMutationRequested = this.ChangeTracker
            .Entries<GuestsTenantDestroyReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        if (tenantDestroyReceiptMutationRequested)
        {
            throw new InvalidOperationException(
                "Guests tenant destruction receipts are append-only.");
        }
    }

    private async Task<int> SaveChangesWithAdmissionAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken)
    {
        bool hasOperationalMutation = this.HasOperationalMutation();
        if (!hasOperationalMutation && !this.HasTenantOwnedMutation())
        {
            return await base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken).ConfigureAwait(false);
        }

        if (!this.scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(this.scopeContext.ScopeId))
        {
            throw new GuestsOperationalAdmissionException(
                GuestsOperationalAdmissionFailure.Unavailable);
        }

        string tenantId = this.scopeContext.ScopeId;
        IDbContextTransaction? ownedTransaction = null;
        if (this.Database.IsRelational() &&
            this.Database.CurrentTransaction is null)
        {
            ownedTransaction = await this.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            if (this.Database.IsRelational())
            {
                await GuestsTenantMutationLock.AcquireAdmissionAsync(
                    this,
                    tenantId,
                    cancellationToken).ConfigureAwait(false);
                if (hasOperationalMutation)
                {
                    await GuestsTenantMutationLock.AcquireRevisionAdvanceAsync(
                        this,
                        tenantId,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            await this.EnsureOperationalAdmissionAsync(cancellationToken)
                .ConfigureAwait(false);
            if (hasOperationalMutation)
            {
                await this.AdvanceTenantRevisionAsync(
                    tenantId,
                    cancellationToken).ConfigureAwait(false);
            }

            int affected = await base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken).ConfigureAwait(false);
            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return affected;
        }
        catch
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.RollbackAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task EnsureOperationalAdmissionAsync(
        CancellationToken cancellationToken)
    {
        GuestsTenantRevision? localState =
            this.TenantRevisions.Local.SingleOrDefault() ??
            await this.TenantRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (localState is not null && !localState.IsOpen)
        {
            throw new GuestsOperationalAdmissionException(
                GuestsOperationalAdmissionFailure.Restricted);
        }

        if (terminationFences is null)
        {
            if (this.Database.IsRelational())
            {
                throw new GuestsOperationalAdmissionException(
                    GuestsOperationalAdmissionFailure.Unavailable);
            }

            return;
        }

        WorkspaceTerminationFenceSnapshot? fence;
        try
        {
            fence = await terminationFences.GetCurrentAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            throw new GuestsOperationalAdmissionException(
                GuestsOperationalAdmissionFailure.Unavailable);
        }

        if (fence is null)
        {
            return;
        }

        bool valid = fence.ProcessId != Guid.Empty &&
            fence.TerminationEpoch != Guid.Empty &&
            fence.Version > 0 &&
            fence.State is
                WorkspaceTerminationFenceState.Frozen or
                WorkspaceTerminationFenceState.DestructionStarted or
                WorkspaceTerminationFenceState.Closed;
        throw new GuestsOperationalAdmissionException(
            valid
                ? GuestsOperationalAdmissionFailure.Restricted
                : GuestsOperationalAdmissionFailure.Unavailable);
    }

    private async Task AdvanceTenantRevisionAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        GuestsTenantRevision? revision =
            this.TenantRevisions.Local.SingleOrDefault() ??
            await this.TenantRevisions
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (revision is null)
        {
            this.TenantRevisions.Add(GuestsTenantRevision.Create(tenantId));
        }
        else
        {
            revision.Advance();
        }
    }

    private bool HasOperationalMutation() =>
        this.ChangeTracker.Entries().Any(entry =>
            (entry.State is EntityState.Added or EntityState.Modified or
                EntityState.Deleted) &&
            entry.Entity is not (
                InboxMessage or
                OutboxMessage or
                GuestProcessingRestrictionProjection or
                GuestPropertyProjection or
                GuestStayHistoryEntry or
                GuestOperationLock or
                GuestsProjectionRebuildCheckpoint or
                GuestRetentionSweepCheckpoint or
                GuestsTenantRevision or
                GuestsTenantDestroyOperation or
                GuestsTenantDestroyReceipt));

    private bool HasTenantOwnedMutation() =>
        this.ChangeTracker.Entries().Any(entry =>
            (entry.State is EntityState.Added or
                EntityState.Modified or EntityState.Deleted) &&
            entry.Entity switch
            {
                InboxMessage message =>
                    entry.State == EntityState.Added &&
                    !string.IsNullOrWhiteSpace(message.ScopeId),
                OutboxMessage message =>
                    entry.State == EntityState.Added &&
                    !string.IsNullOrWhiteSpace(message.ScopeId),
                GuestsTenantRevision or
                GuestsTenantDestroyOperation or
                GuestsTenantDestroyReceipt => false,
                _ => true
            });

    internal async ValueTask<bool> TryAdmitMessageMutationAsync(
        string? tenantId,
        CancellationToken cancellationToken)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId) ||
            !this.scopeContext.IsEnabled ||
            !string.Equals(
                this.scopeContext.ScopeId,
                canonicalTenantId,
                StringComparison.Ordinal) ||
            (this.Database.IsRelational() &&
             this.Database.CurrentTransaction is null))
        {
            return false;
        }

        try
        {
            if (this.Database.IsRelational())
            {
                await GuestsTenantMutationLock.AcquireAdmissionAsync(
                    this,
                    canonicalTenantId,
                    cancellationToken).ConfigureAwait(false);
            }

            await this.EnsureOperationalAdmissionAsync(cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (GuestsOperationalAdmissionException)
        {
            return false;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    internal async Task<int> SaveTenantDestructionChangesAsync(
        string tenantId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId) ||
            operationId == Guid.Empty ||
            !this.scopeContext.IsEnabled ||
            !string.Equals(
                this.scopeContext.ScopeId,
                canonicalTenantId,
                StringComparison.Ordinal) ||
            (this.Database.IsRelational() &&
             this.Database.CurrentTransaction is null))
        {
            throw new InvalidOperationException(
                "Guests tenant destruction admission is invalid.");
        }

        GuestsTenantRevision? state =
            this.TenantRevisions.Local.SingleOrDefault() ??
            await this.TenantRevisions
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (state is null || state.DestroyOperationId != operationId ||
            state.LifecycleStatus is not (
                GuestsTenantLifecycleStatus.Closing or
                GuestsTenantLifecycleStatus.Closed))
        {
            throw new InvalidOperationException(
                "Guests tenant destruction state is invalid.");
        }

        if (this.Database.IsNpgsql())
        {
            await this.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT set_config('bunkfy.guests_tenant_destroy_operation_id', {operationId.ToString("D")}, true)",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await base.SaveChangesAsync(
                acceptAllChangesOnSuccess: true,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

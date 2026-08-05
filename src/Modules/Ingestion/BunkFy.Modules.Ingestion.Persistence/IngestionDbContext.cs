namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Runs;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Retention;
using BunkFy.Modules.Ingestion.Domain.Controls;
using BunkFy.Modules.Ingestion.Persistence.TenantTermination;
using BunkFy.Modules.Workspaces.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public sealed class IngestionDbContext(
    DbContextOptions<IngestionDbContext> options,
    IScopeContext scopeContext,
    IWorkspaceTerminationFenceReader? terminationFences = null)
    : ScopeAwareDbContext<IngestionDbContext>(options, scopeContext)
{
    private readonly IScopeContext scopeContext = scopeContext;

    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<AdapterConnection> AdapterConnections => this.Set<AdapterConnection>();
    public DbSet<AdapterIngressCredential> AdapterIngressCredentials => this.Set<AdapterIngressCredential>();
    public DbSet<AdapterIngressTenantControl> AdapterIngressTenantControls =>
        this.Set<AdapterIngressTenantControl>();
    public DbSet<AdapterIngressGlobalControl> AdapterIngressGlobalControls =>
        this.Set<AdapterIngressGlobalControl>();
    public DbSet<IngestionPropertyProjection> PropertyProjections => this.Set<IngestionPropertyProjection>();
    public DbSet<IngestionProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints =>
        this.Set<IngestionProjectionRebuildCheckpoint>();
    public DbSet<IngestionRun> Runs => this.Set<IngestionRun>();
    public DbSet<ObservationReceipt> ObservationReceipts => this.Set<ObservationReceipt>();
    public DbSet<ObservationReprocessingAttempt> ObservationReprocessingAttempts =>
        this.Set<ObservationReprocessingAttempt>();
    public DbSet<ObservationReprocessingOutput> ObservationReprocessingOutputs =>
        this.Set<ObservationReprocessingOutput>();
    public DbSet<ChangeProposal> ChangeProposals => this.Set<ChangeProposal>();
    public DbSet<ReservationSourceLink> ReservationSourceLinks => this.Set<ReservationSourceLink>();
    public DbSet<ReservationDispatch> ReservationDispatches => this.Set<ReservationDispatch>();
    public DbSet<LegalHold> LegalHolds => this.Set<LegalHold>();
    public DbSet<IngestionRetentionExecution> RetentionExecutions =>
        this.Set<IngestionRetentionExecution>();
    public DbSet<IngestionAnonymisationTombstone>
        AnonymisationTombstones =>
        this.Set<IngestionAnonymisationTombstone>();
    public DbSet<IngestionAnonymisationReceipt>
        AnonymisationReceipts =>
        this.Set<IngestionAnonymisationReceipt>();
    public DbSet<IngestionAnonymisationFingerprint>
        AnonymisationFingerprints =>
        this.Set<IngestionAnonymisationFingerprint>();
    public DbSet<IngestionAnonymisationRecordPlanEntry>
        AnonymisationRecordPlan =>
        this.Set<IngestionAnonymisationRecordPlanEntry>();
    internal DbSet<IngestionSourceOperationLock> SourceOperationLocks =>
        this.Set<IngestionSourceOperationLock>();
    internal DbSet<IngestionTenantRevision> TenantRevisions =>
        this.Set<IngestionTenantRevision>();
    internal DbSet<IngestionTenantDestroyOperation> TenantDestroyOperations =>
        this.Set<IngestionTenantDestroyOperation>();
    internal DbSet<IngestionTenantDestroyReceipt> TenantDestroyReceipts =>
        this.Set<IngestionTenantDestroyReceipt>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.EnsureOwnerProofIsAppendOnly();
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
        this.EnsureOwnerProofIsAppendOnly();
        return this.SaveChangesWithAdmissionAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    internal async Task<int> ExecuteCoordinatedMutationAsync(
        Func<CancellationToken, Task<int>> mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        this.EnsureCoordinatedMutationStartsClean();
        string tenantId = this.RequireTenant();
        IDbContextTransaction? ownedTransaction =
            await this.BeginOwnedTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
        try
        {
            if (this.Database.IsRelational())
            {
                await IngestionTenantMutationLock.AcquireAdmissionAsync(
                    this,
                    tenantId,
                    cancellationToken).ConfigureAwait(false);
            }

            await this.EnsureOperationalAdmissionAsync(cancellationToken)
                .ConfigureAwait(false);
            int affected = await mutation(cancellationToken)
                .ConfigureAwait(false);
            if (affected > 0)
            {
                await this.AdvanceTenantRevisionAsync(
                    tenantId,
                    cancellationToken).ConfigureAwait(false);
                _ = await base.SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(IngestionMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IngestionDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }

    private void EnsureOwnerProofIsAppendOnly()
    {
        bool receiptMutationRequested = this.ChangeTracker
            .Entries<IngestionAnonymisationReceipt>()
            .Any(entry => entry.State is
                EntityState.Modified or EntityState.Deleted);
        bool tombstoneDeletionRequested = this.ChangeTracker
            .Entries<IngestionAnonymisationTombstone>()
            .Any(entry => entry.State == EntityState.Deleted);
        bool tenantDestroyReceiptMutationRequested = this.ChangeTracker
            .Entries<IngestionTenantDestroyReceipt>()
            .Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        if (receiptMutationRequested ||
            tombstoneDeletionRequested ||
            tenantDestroyReceiptMutationRequested)
        {
            throw new InvalidOperationException(
                "Ingestion owner proof is append-only.");
        }
    }

    private void EnsureCoordinatedMutationStartsClean()
    {
        if (this.ChangeTracker.Entries().Any(entry =>
                entry.State is EntityState.Added or
                    EntityState.Modified or
                    EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Coordinated Ingestion direct mutations require a clean change tracker.");
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

        string tenantId = this.RequireTenant();
        IDbContextTransaction? ownedTransaction =
            await this.BeginOwnedTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
        try
        {
            if (this.Database.IsRelational())
            {
                await IngestionTenantMutationLock.AcquireAdmissionAsync(
                    this,
                    tenantId,
                    cancellationToken).ConfigureAwait(false);
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

    private string RequireTenant()
    {
        if (!this.scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(this.scopeContext.ScopeId))
        {
            throw new IngestionOperationalAdmissionException(
                IngestionOperationalAdmissionFailure.Unavailable);
        }

        return this.scopeContext.ScopeId;
    }

    private async Task<IDbContextTransaction?> BeginOwnedTransactionAsync(
        CancellationToken cancellationToken) =>
        this.Database.IsRelational() &&
        this.Database.CurrentTransaction is null
            ? await this.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

    private async Task EnsureOperationalAdmissionAsync(
        CancellationToken cancellationToken)
    {
        IngestionTenantRevision? localState =
            this.TenantRevisions.Local.SingleOrDefault() ??
            await this.TenantRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (localState is not null && !localState.IsOpen)
        {
            throw new IngestionOperationalAdmissionException(
                IngestionOperationalAdmissionFailure.Restricted);
        }

        if (terminationFences is null)
        {
            if (this.Database.IsRelational())
            {
                throw new IngestionOperationalAdmissionException(
                    IngestionOperationalAdmissionFailure.Unavailable);
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
            throw new IngestionOperationalAdmissionException(
                IngestionOperationalAdmissionFailure.Unavailable);
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
        throw new IngestionOperationalAdmissionException(
            valid
                ? IngestionOperationalAdmissionFailure.Restricted
                : IngestionOperationalAdmissionFailure.Unavailable);
    }

    private async Task AdvanceTenantRevisionAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        IngestionTenantRevision? revision =
            this.TenantRevisions.Local.SingleOrDefault() ??
            await this.TenantRevisions.SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (revision is null)
        {
            this.TenantRevisions.Add(
                IngestionTenantRevision.Create(tenantId));
        }
        else
        {
            revision.Advance();
        }
    }

    private bool HasOperationalMutation() =>
        this.ChangeTracker.Entries().Any(entry =>
            (entry.State is
                EntityState.Added or
                EntityState.Modified or
                EntityState.Deleted) &&
            entry.Entity is not (
                InboxMessage or
                OutboxMessage or
                AdapterIngressGlobalControl or
                IngestionPropertyProjection or
                IngestionProjectionRebuildCheckpoint or
                IngestionSourceOperationLock or
                IngestionTenantRevision or
                IngestionTenantDestroyOperation or
                IngestionTenantDestroyReceipt));

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
                AdapterIngressGlobalControl or
                IngestionTenantRevision or
                IngestionTenantDestroyOperation or
                IngestionTenantDestroyReceipt => false,
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
                await IngestionTenantMutationLock.AcquireAdmissionAsync(
                    this,
                    canonicalTenantId,
                    cancellationToken).ConfigureAwait(false);
            }

            await this.EnsureOperationalAdmissionAsync(cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (IngestionOperationalAdmissionException)
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
                "Ingestion tenant destruction admission is invalid.");
        }

        IngestionTenantRevision? state =
            this.TenantRevisions.Local.SingleOrDefault() ??
            await this.TenantRevisions
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (state is null || state.DestroyOperationId != operationId ||
            state.LifecycleStatus is not (
                IngestionTenantLifecycleStatus.Closing or
                IngestionTenantLifecycleStatus.Closed))
        {
            throw new InvalidOperationException(
                "Ingestion tenant destruction state is invalid.");
        }

        if (this.Database.IsNpgsql())
        {
            await this.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT set_config('bunkfy.ingestion_tenant_destroy_operation_id', {operationId.ToString("D")}, true)",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await base.SaveChangesAsync(
                acceptAllChangesOnSuccess: true,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

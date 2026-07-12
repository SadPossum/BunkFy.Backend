namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Messaging.Infrastructure;
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
using Microsoft.EntityFrameworkCore;
public sealed class IngestionDbContext(DbContextOptions<IngestionDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<IngestionDbContext>(options, scopeContext)
{
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<AdapterConnection> AdapterConnections => this.Set<AdapterConnection>();
    public DbSet<AdapterIngressCredential> AdapterIngressCredentials => this.Set<AdapterIngressCredential>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(IngestionMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IngestionDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}

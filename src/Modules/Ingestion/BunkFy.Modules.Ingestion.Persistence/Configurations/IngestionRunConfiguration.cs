namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    public void Configure(EntityTypeBuilder<IngestionRun> builder)
    {
        builder.ToTable("runs", table => table.HasCheckConstraint(
            "CK_runs_execution_identity",
            "(\"ExecutionKind\" = 1 AND \"TaskRunId\" IS NOT NULL AND \"TaskAttempt\" > 0 AND " +
            "\"RemoteLeaseId\" IS NULL AND \"RemoteClaimId\" IS NULL AND \"RemoteLeaseEpoch\" IS NULL AND \"RemoteCredentialId\" IS NULL AND " +
            "\"RemoteWorkerId\" IS NULL AND \"RemoteLeaseExpiresAtUtc\" IS NULL) OR " +
            "(\"ExecutionKind\" = 2 AND \"TaskRunId\" IS NULL AND \"TaskAttempt\" IS NULL AND " +
            "\"RemoteLeaseId\" IS NOT NULL AND \"RemoteClaimId\" IS NOT NULL AND \"RemoteLeaseEpoch\" > 0 AND \"RemoteCredentialId\" IS NOT NULL AND " +
            "\"RemoteWorkerId\" IS NOT NULL AND \"RemoteLeaseExpiresAtUtc\" IS NOT NULL)"));
        builder.HasKey(run => run.Id);
        builder.HasAlternateKey(run => new { run.ScopeId, run.Id });
        builder.Property(run => run.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(run => run.ExecutionKind).HasConversion<int>().IsRequired();
        builder.Property(run => run.StartingCheckpoint).HasMaxLength(IngestionRun.CheckpointMaxLength);
        builder.Property(run => run.AcceptedCheckpoint).HasMaxLength(IngestionRun.CheckpointMaxLength);
        builder.Property(run => run.State).HasConversion<int>().IsRequired();
        builder.Property(run => run.ErrorMessage).HasMaxLength(IngestionRun.ErrorMessageMaxLength);
        builder.Property(run => run.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(run => new { run.ScopeId, run.TaskRunId, run.TaskAttempt })
            .IsUnique()
            .HasFilter("\"ExecutionKind\" = 1");
        builder.HasIndex(run => new { run.ScopeId, run.RemoteLeaseId })
            .IsUnique()
            .HasFilter("\"ExecutionKind\" = 2");
        builder.HasIndex(run => new { run.ScopeId, run.ConnectionId, run.StartedAtUtc });
        builder.HasIndex(run => new { run.ScopeId, run.ConnectionId })
            .IsUnique()
            .HasFilter("\"State\" = 1");
        builder.HasOne<AdapterConnection>()
            .WithMany()
            .HasForeignKey(run => new { run.ScopeId, run.ConnectionId })
            .HasPrincipalKey(connection => new { connection.ScopeId, connection.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(run => run.DomainEvents);
    }
}

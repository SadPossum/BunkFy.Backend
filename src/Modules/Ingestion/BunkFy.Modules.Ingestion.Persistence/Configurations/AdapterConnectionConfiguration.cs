namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class AdapterConnectionConfiguration : IEntityTypeConfiguration<AdapterConnection>
{
    public void Configure(EntityTypeBuilder<AdapterConnection> builder)
    {
        builder.ToTable("adapter_connections", table =>
        {
            table.HasCheckConstraint(
                "CK_adapter_connections_polling_schedule_complete",
                "(\"PollingIntervalSeconds\" IS NULL AND \"PollingScheduleMaxAttempts\" IS NULL AND \"PollingScheduleConfiguredAtUtc\" IS NULL) OR " +
                "(\"PollingIntervalSeconds\" BETWEEN 60 AND 2592000 AND \"PollingScheduleMaxAttempts\" BETWEEN 1 AND 10 AND " +
                "\"PollingScheduleConfiguredAtUtc\" IS NOT NULL AND \"ExecutionMode\" = 1)");
            table.HasCheckConstraint(
                "CK_adapter_connections_remote_lease_complete",
                "(\"RemoteLeaseRunId\" IS NULL AND \"RemoteLeaseId\" IS NULL AND \"RemoteLeaseClaimId\" IS NULL AND \"RemoteLeaseCredentialId\" IS NULL AND " +
                "\"RemoteLeaseWorkerId\" IS NULL AND \"RemoteLeaseExpiresAtUtc\" IS NULL) OR " +
                "(\"ExecutionMode\" = 4 AND \"RemoteLeaseRunId\" IS NOT NULL AND \"RemoteLeaseId\" IS NOT NULL AND \"RemoteLeaseClaimId\" IS NOT NULL AND " +
                "\"RemoteLeaseCredentialId\" IS NOT NULL AND \"RemoteLeaseWorkerId\" IS NOT NULL AND " +
                "\"RemoteLeaseEpoch\" > 0 AND \"RemoteLeaseExpiresAtUtc\" IS NOT NULL)");
        });
        builder.HasKey(connection => connection.Id);
        builder.HasAlternateKey(connection => new { connection.ScopeId, connection.Id });
        builder.Property(connection => connection.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(connection => connection.AdapterType).HasMaxLength(AdapterConnection.AdapterTypeMaxLength).IsRequired();
        builder.Property(connection => connection.ExecutionMode).HasConversion<int>().IsRequired();
        builder.Property(connection => connection.ConflictPolicy).HasConversion<int>().IsRequired();
        builder.Property(connection => connection.ConfigurationReference).HasMaxLength(AdapterConnection.ReferenceMaxLength).IsRequired();
        builder.Property(connection => connection.SecretReference).HasMaxLength(AdapterConnection.ReferenceMaxLength);
        builder.Property(connection => connection.Checkpoint).HasMaxLength(AdapterConnection.CheckpointMaxLength);
        builder.Property(connection => connection.PollingIntervalSeconds);
        builder.Property(connection => connection.PollingScheduleMaxAttempts);
        builder.Property(connection => connection.PollingScheduleConfiguredAtUtc);
        builder.Property(connection => connection.RemoteLeaseEpoch).IsConcurrencyToken().IsRequired();
        builder.Property(connection => connection.State).HasConversion<int>().IsRequired();
        builder.Property(connection => connection.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(connection => new { connection.ScopeId, connection.PropertyId, connection.State });
        builder.HasIndex(connection => new { connection.ScopeId, connection.AdapterType });
        builder.HasIndex(connection => new
        {
            connection.State,
            connection.ExecutionMode,
            connection.PollingIntervalSeconds
        });
        builder.HasIndex(connection => new
        {
            connection.ScopeId,
            connection.ExecutionMode,
            connection.RemoteLeaseExpiresAtUtc
        });
        builder.Ignore(connection => connection.DomainEvents);
    }
}

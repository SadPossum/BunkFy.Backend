namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionConnectionManagementOperationConfiguration
    : IEntityTypeConfiguration<IngestionConnectionManagementOperation>
{
    private const string EmptyGuid =
        "00000000-0000-0000-0000-000000000000";

    public void Configure(
        EntityTypeBuilder<IngestionConnectionManagementOperation> builder)
    {
        builder.ToTable("connection_management_operations", table =>
        {
            table.HasCheckConstraint(
                "CK_ingestion_connection_management_operations_coordinates",
                $"\"Id\" <> '{EmptyGuid}' AND " +
                $"\"PropertyId\" <> '{EmptyGuid}' AND " +
                $"\"ConnectionId\" <> '{EmptyGuid}' AND " +
                "char_length(btrim(\"ScopeId\")) > 0");
            table.HasCheckConstraint(
                "CK_ingestion_connection_management_operations_fingerprint",
                "char_length(\"RequestFingerprint\") = 64 AND " +
                "\"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_ingestion_connection_management_operations_outcome",
                "(\"Kind\" = 1 AND \"ExpectedVersion\" = 0 AND " +
                "\"ResultVersion\" = 1 AND \"Id\" = \"ConnectionId\") OR " +
                "(\"Kind\" = 2 AND \"ExpectedVersion\" > 0 AND " +
                "\"ResultVersion\" >= \"ExpectedVersion\" AND " +
                "\"ResultVersion\" <= \"ExpectedVersion\" + 1)");
        });
        builder.HasKey(operation => new
        {
            operation.ScopeId,
            operation.ConnectionId,
            operation.Id
        });
        builder.Property(operation => operation.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(operation => operation.Kind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(operation => operation.RequestFingerprint)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.HasOne<AdapterConnection>()
            .WithMany()
            .HasForeignKey(operation => new
            {
                operation.ScopeId,
                operation.ConnectionId
            })
            .HasPrincipalKey(connection => new
            {
                connection.ScopeId,
                connection.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(operation => new
        {
            operation.ScopeId,
            operation.PropertyId,
            operation.CompletedAtUtc,
            operation.ConnectionId,
            operation.Id
        });
    }
}

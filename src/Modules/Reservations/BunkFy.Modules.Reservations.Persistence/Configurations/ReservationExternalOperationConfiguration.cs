namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationExternalOperationConfiguration
    : IEntityTypeConfiguration<ReservationExternalOperation>
{
    public void Configure(EntityTypeBuilder<ReservationExternalOperation> builder)
    {
        builder.ToTable("external_operations");
        builder.HasKey(operation => new { operation.ScopeId, operation.Id });
        builder.Property(operation => operation.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(operation => operation.Kind).HasConversion<int>().IsRequired();
        builder.Property(operation => operation.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.Outcome).HasConversion<int>().IsRequired();
        builder.Property(operation => operation.ErrorCode).HasMaxLength(200);
        builder.HasIndex(operation => new { operation.ScopeId, operation.ConnectionId, operation.CompletedAtUtc });
        builder.HasIndex(operation => new { operation.ScopeId, operation.ReceiptId });
    }
}

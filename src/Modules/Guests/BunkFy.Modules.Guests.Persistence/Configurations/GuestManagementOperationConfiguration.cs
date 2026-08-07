namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestManagementOperationConfiguration
    : IEntityTypeConfiguration<GuestManagementOperation>
{
    public void Configure(EntityTypeBuilder<GuestManagementOperation> builder)
    {
        builder.ToTable("management_operations", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_management_operations_kind",
                "\"Kind\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_guest_management_operations_versions",
                "\"ExpectedVersion\" > 0 AND " +
                "\"ResultVersion\" = \"ExpectedVersion\" + 1");
            table.HasCheckConstraint(
                "CK_guest_management_operations_result",
                "(\"Kind\" = 1 AND \"ResultStatus\" = 1 AND " +
                "\"RequestFingerprint\" IS NOT NULL AND " +
                "char_length(\"RequestFingerprint\") = 64 AND " +
                "\"RequestFingerprint\" ~ '^[0-9a-f]{64}$') OR " +
                "(\"Kind\" = 2 AND \"ResultStatus\" = 2 AND " +
                "\"RequestFingerprint\" IS NULL)");
        });
        builder.HasKey(operation => new
        {
            operation.ScopeId,
            operation.GuestId,
            operation.Id
        });
        builder.Property(operation => operation.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(operation => operation.Kind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(operation => operation.RequestFingerprint)
            .HasMaxLength(64);
        builder.Property(operation => operation.ResultStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.HasOne<GuestProfile>()
            .WithMany()
            .HasForeignKey(operation => new
            {
                operation.ScopeId,
                operation.GuestId
            })
            .HasPrincipalKey(profile => new
            {
                profile.ScopeId,
                profile.Id
            })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(operation => new
        {
            operation.ScopeId,
            operation.PropertyId,
            operation.CompletedAtUtc,
            operation.Id
        });
    }
}

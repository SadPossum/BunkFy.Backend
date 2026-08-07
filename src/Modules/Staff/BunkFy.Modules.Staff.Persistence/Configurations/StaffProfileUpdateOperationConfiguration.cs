namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffProfileUpdateOperationConfiguration
    : IEntityTypeConfiguration<StaffProfileUpdateOperation>
{
    public void Configure(
        EntityTypeBuilder<StaffProfileUpdateOperation> builder)
    {
        builder.ToTable("profile_update_operations", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_profile_update_operations_versions",
                "\"ExpectedVersion\" > 0 AND " +
                "\"ResultVersion\" >= \"ExpectedVersion\" AND " +
                "\"ResultVersion\" <= \"ExpectedVersion\" + 1");
            table.HasCheckConstraint(
                "CK_staff_profile_update_operations_fingerprint",
                "char_length(\"RequestFingerprint\") = 64 AND " +
                "\"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_staff_profile_update_operations_status",
                "\"ResultStatus\" IN (1, 2)");
        });
        builder.HasKey(operation => new
        {
            operation.ScopeId,
            operation.StaffMemberId,
            operation.Id
        });
        builder.Property(operation => operation.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(operation => operation.RequestFingerprint)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(operation => operation.ResultStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(operation => new
            {
                operation.ScopeId,
                operation.StaffMemberId
            })
            .HasPrincipalKey(member => new
            {
                member.ScopeId,
                member.Id
            })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(operation => new
        {
            operation.ScopeId,
            operation.CompletedAtUtc,
            operation.Id
        });
    }
}

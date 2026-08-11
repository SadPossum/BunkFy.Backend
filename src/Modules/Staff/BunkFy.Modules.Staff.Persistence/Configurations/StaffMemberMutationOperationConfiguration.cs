namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffMemberMutationOperationConfiguration
    : IEntityTypeConfiguration<StaffMemberMutationOperation>
{
    public void Configure(
        EntityTypeBuilder<StaffMemberMutationOperation> builder)
    {
        builder.ToTable("member_mutation_operations", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_member_mutation_operations_versions",
                "\"ExpectedVersion\" > 0 AND " +
                "\"ResultVersion\" >= \"ExpectedVersion\" AND " +
                "\"ResultVersion\" <= \"ExpectedVersion\" + " +
                "CASE WHEN \"Kind\" = 8 THEN 2 ELSE 1 END");
            table.HasCheckConstraint(
                "CK_staff_member_mutation_operations_fingerprint",
                "char_length(\"RequestFingerprint\") = 64 AND " +
                "\"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_staff_member_mutation_operations_status",
                "\"ResultStatus\" IN (1, 2, 3) AND " +
                "(\"Kind\" <> 8 OR \"ResultStatus\" = 1)");
            table.HasCheckConstraint(
                "CK_staff_member_mutation_operations_kind",
                "\"Kind\" IN (1, 2, 3, 4, 5, 6, 7, 8)");
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
        builder.Property(operation => operation.Kind)
            .HasConversion<int>()
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
        builder.HasIndex(operation => new
        {
            operation.ScopeId,
            operation.Id
        })
            .HasDatabaseName(
                "UX_staff_member_mutation_operations_onboarding_operation")
            .HasFilter("\"Kind\" = 8")
            .IsUnique();
    }
}

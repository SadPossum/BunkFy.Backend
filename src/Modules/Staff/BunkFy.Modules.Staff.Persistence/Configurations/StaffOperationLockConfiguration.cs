namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffOperationLockConfiguration
    : IEntityTypeConfiguration<StaffOperationLock>
{
    public void Configure(EntityTypeBuilder<StaffOperationLock> builder)
    {
        builder.ToTable("staff_operation_locks", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_operation_locks_coordinate",
                "\"Id\" = \"StaffMemberId\"");
            table.HasCheckConstraint(
                "CK_staff_operation_locks_revision",
                "\"Revision\" >= 1");
        });
        builder.HasKey(resourceLock => resourceLock.Id);
        builder.HasAlternateKey(resourceLock => new
        {
            resourceLock.ScopeId,
            resourceLock.Id
        });
        builder.Property(resourceLock => resourceLock.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(resourceLock => resourceLock.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(resourceLock => new
        {
            resourceLock.ScopeId,
            resourceLock.StaffMemberId
        }).IsUnique();
        builder.HasOne<StaffMember>()
            .WithOne()
            .HasForeignKey<StaffOperationLock>(resourceLock => new
            {
                resourceLock.ScopeId,
                resourceLock.StaffMemberId
            })
            .HasPrincipalKey<StaffMember>(member => new
            {
                member.ScopeId,
                member.Id
            })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

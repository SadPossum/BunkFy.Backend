namespace BunkFy.Modules.Staff.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.ToTable("staff_members", table =>
        {
            table.HasCheckConstraint("CK_staff_members_version", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_staff_members_display_name", "length(trim(\"DisplayName\")) > 0");
            table.HasCheckConstraint("CK_staff_members_created_by", "length(trim(\"CreatedBy\")) > 0");
            table.HasCheckConstraint("CK_staff_members_last_changed_by", "length(trim(\"LastChangedBy\")) > 0");
            table.HasCheckConstraint("CK_staff_members_lifecycle",
                "(\"Status\" = 1 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NULL AND \"DepartureEffectiveOn\" IS NULL) OR " +
                "(\"Status\" = 2 AND \"SuspendedAtUtc\" IS NOT NULL AND \"DepartedAtUtc\" IS NULL AND \"DepartureEffectiveOn\" IS NULL) OR " +
                "(\"Status\" = 3 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NOT NULL AND \"DepartureEffectiveOn\" IS NOT NULL)");
        });
        builder.HasKey(member => member.Id);
        builder.HasAlternateKey(member => new { member.ScopeId, member.Id });
        builder.Property(member => member.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(member => member.DisplayName).HasMaxLength(StaffMember.DisplayNameMaxLength).IsRequired();
        builder.Property(member => member.DisplayNameSearch).HasMaxLength(StaffMember.DisplayNameMaxLength).IsRequired();
        builder.Property(member => member.LegalName).HasMaxLength(StaffMember.LegalNameMaxLength);
        builder.Property(member => member.LegalNameSearch).HasMaxLength(StaffMember.LegalNameMaxLength);
        builder.Property(member => member.WorkEmail).HasMaxLength(StaffMember.EmailMaxLength);
        builder.Property(member => member.WorkEmailSearch).HasMaxLength(StaffMember.EmailMaxLength);
        builder.Property(member => member.WorkPhone).HasMaxLength(StaffMember.PhoneMaxLength);
        builder.Property(member => member.WorkPhoneSearch).HasMaxLength(StaffMember.PhoneMaxLength);
        builder.Property(member => member.EmployeeNumber).HasMaxLength(StaffMember.EmployeeNumberMaxLength);
        builder.Property(member => member.EmployeeNumberSearch).HasMaxLength(StaffMember.EmployeeNumberMaxLength);
        builder.Property(member => member.JobTitle).HasMaxLength(StaffMember.JobTitleMaxLength);
        builder.Property(member => member.Department).HasMaxLength(StaffMember.DepartmentMaxLength);
        builder.Property(member => member.AuthSubjectId).HasMaxLength(StaffMember.AuthSubjectIdMaxLength);
        builder.Property(member => member.Status).HasConversion<int>().IsRequired();
        builder.Property(member => member.Version).IsConcurrencyToken().IsRequired();
        builder.Property(member => member.ProjectionOrdinal).ValueGeneratedOnAdd().IsRequired();
        builder.HasIndex(member => member.ProjectionOrdinal).IsUnique();
        builder.Property(member => member.CreatedBy).HasMaxLength(StaffMember.ActorIdMaxLength).IsRequired();
        builder.Property(member => member.LastChangedBy).HasMaxLength(StaffMember.ActorIdMaxLength).IsRequired();
        builder.HasIndex(member => new { member.ScopeId, member.EmployeeNumberSearch }).IsUnique();
        builder.HasIndex(member => new { member.ScopeId, member.AuthSubjectId }).IsUnique();
        builder.HasIndex(member => new { member.ScopeId, member.Status, member.DisplayNameSearch, member.Id });
        builder.HasMany(member => member.Assignments).WithOne().HasForeignKey(assignment =>
            new { assignment.ScopeId, assignment.StaffMemberId }).HasPrincipalKey(member =>
            new { member.ScopeId, member.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(member => member.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(member => member.DomainEvents);
    }
}

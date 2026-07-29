namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffDataHoldConfiguration
    : IEntityTypeConfiguration<StaffDataHold>
{
    public void Configure(EntityTypeBuilder<StaffDataHold> builder)
    {
        builder.ToTable("staff_data_holds", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_data_holds_state",
                "\"State\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_staff_data_holds_lifecycle",
                "(\"State\" = 1 AND \"Version\" = 1 AND " +
                "\"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL) OR " +
                "(\"State\" = 2 AND \"Version\" = 2 AND " +
                "\"ReleasedBy\" IS NOT NULL AND " +
                "\"ReleasedAtUtc\" IS NOT NULL AND " +
                "\"ReleasedAtUtc\" >= \"PlacedAtUtc\")");
        });
        builder.HasKey(hold => hold.Id);
        builder.HasAlternateKey(hold => new
        {
            hold.ScopeId,
            hold.Id
        });
        builder.Property(hold => hold.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(hold => hold.ReasonCode)
            .HasMaxLength(StaffDataHold.ReasonCodeMaxLength)
            .IsRequired();
        builder.Property(hold => hold.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(hold => hold.PlacedBy)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(hold => hold.ReleasedBy)
            .HasMaxLength(200);
        builder.Property(hold => hold.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(hold => new
        {
            hold.ScopeId,
            hold.StaffMemberId,
            hold.State,
            hold.PlacedAtUtc,
            hold.Id
        });
        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(hold => new
            {
                hold.ScopeId,
                hold.StaffMemberId
            })
            .HasPrincipalKey(member => new
            {
                member.ScopeId,
                member.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

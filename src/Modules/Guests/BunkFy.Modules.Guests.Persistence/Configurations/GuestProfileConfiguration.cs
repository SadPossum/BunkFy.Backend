namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestProfileConfiguration : IEntityTypeConfiguration<GuestProfile>
{
    public void Configure(EntityTypeBuilder<GuestProfile> builder)
    {
        builder.ToTable("guest_profiles", table =>
        {
            table.HasCheckConstraint("CK_guest_profiles_version", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_guest_profiles_display_name", "length(trim(\"DisplayName\")) > 0");
            table.HasCheckConstraint("CK_guest_profiles_created_by", "length(trim(\"CreatedBy\")) > 0");
            table.HasCheckConstraint("CK_guest_profiles_last_changed_by", "length(trim(\"LastChangedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_guest_profiles_lifecycle",
                "(\"Status\" = 1 AND \"ArchivedAtUtc\" IS NULL) OR " +
                "(\"Status\" = 2 AND \"ArchivedAtUtc\" IS NOT NULL AND \"ArchivedAtUtc\" >= \"CreatedAtUtc\")");
        });
        builder.HasKey(profile => profile.Id);
        builder.HasAlternateKey(profile => new { profile.ScopeId, profile.Id });
        builder.Property(profile => profile.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(profile => profile.DisplayName).HasMaxLength(GuestProfile.DisplayNameMaxLength).IsRequired();
        builder.Property(profile => profile.DisplayNameSearch).HasMaxLength(GuestProfile.DisplayNameMaxLength).IsRequired();
        builder.Property(profile => profile.LegalName).HasMaxLength(GuestProfile.LegalNameMaxLength);
        builder.Property(profile => profile.LegalNameSearch).HasMaxLength(GuestProfile.LegalNameMaxLength);
        builder.Property(profile => profile.Email).HasMaxLength(GuestProfile.EmailMaxLength);
        builder.Property(profile => profile.EmailSearch).HasMaxLength(GuestProfile.EmailMaxLength);
        builder.Property(profile => profile.Phone).HasMaxLength(GuestProfile.PhoneMaxLength);
        builder.Property(profile => profile.PhoneSearch).HasMaxLength(GuestProfile.PhoneMaxLength);
        builder.Property(profile => profile.NationalityCountryCode)
            .HasMaxLength(GuestProfile.CountryCodeLength)
            .IsFixedLength();
        builder.Property(profile => profile.PreferredLanguageTag).HasMaxLength(GuestProfile.LanguageTagMaxLength);
        builder.Property(profile => profile.Notes).HasMaxLength(GuestProfile.NotesMaxLength);
        builder.Property(profile => profile.Status).HasConversion<int>().IsRequired();
        builder.Property(profile => profile.Version).IsConcurrencyToken().IsRequired();
        builder.Property(profile => profile.ProjectionOrdinal).ValueGeneratedOnAdd().IsRequired();
        builder.HasIndex(profile => profile.ProjectionOrdinal).IsUnique();
        builder.Property(profile => profile.CreatedBy).HasMaxLength(GuestProfile.ActorIdMaxLength).IsRequired();
        builder.Property(profile => profile.LastChangedBy).HasMaxLength(GuestProfile.ActorIdMaxLength).IsRequired();
        builder.HasIndex(profile => new
        {
            profile.ScopeId,
            profile.OriginPropertyId,
            profile.Status,
            profile.DisplayNameSearch,
            profile.Id
        });
        builder.HasIndex(profile => new { profile.ScopeId, profile.EmailSearch });
        builder.HasIndex(profile => new { profile.ScopeId, profile.PhoneSearch });
        builder.Ignore(profile => profile.DomainEvents);
    }
}

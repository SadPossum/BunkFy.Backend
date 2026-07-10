namespace Properties.Persistence.Configurations;

using Properties.Domain.Aggregates;
using Properties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("properties");
        builder.HasKey(property => property.Id);
        builder.Property(property => property.Name)
            .HasConversion(name => name.Value, value => PropertyName.Create(value).Value)
            .HasMaxLength(Property.PropertyNameMaxLength)
            .IsRequired();
        builder.Property(property => property.Code)
            .HasConversion(code => code.Value, value => PropertyCode.Create(value).Value)
            .HasMaxLength(Property.PropertyCodeMaxLength)
            .IsRequired();
        builder.Property(property => property.TimeZoneId)
            .HasConversion(timeZone => timeZone.Value, value => PropertyTimeZoneId.Create(value).Value)
            .HasMaxLength(Property.TimeZoneIdMaxLength)
            .IsRequired();
        builder.Property(property => property.Status).HasConversion<int>().IsRequired();
        builder.Property(property => property.Version)
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(property => property.ProjectionOrdinal)
            .ValueGeneratedOnAdd()
            .IsRequired();
        builder.HasAlternateKey(property => new { property.ScopeId, property.Id });
        builder.HasIndex(property => new { property.ScopeId, property.Code }).IsUnique();
        builder.HasIndex(property => property.ProjectionOrdinal).IsUnique();
    }
}

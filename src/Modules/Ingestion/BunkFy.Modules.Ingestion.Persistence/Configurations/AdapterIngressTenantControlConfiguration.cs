namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class AdapterIngressTenantControlConfiguration
    : IEntityTypeConfiguration<AdapterIngressTenantControl>
{
    public void Configure(EntityTypeBuilder<AdapterIngressTenantControl> builder)
    {
        builder.ToTable("adapter_ingress_tenant_controls", table =>
        {
            table.HasCheckConstraint(
                "CK_adapter_ingress_tenant_controls_identity",
                "\"Id\" = \"ScopeId\"");
            table.HasCheckConstraint(
                "CK_adapter_ingress_tenant_controls_lifecycle",
                "(\"IsSuspended\" AND \"SuspendedAtUtc\" IS NOT NULL AND " +
                "(\"ResumedAtUtc\" IS NULL OR \"SuspendedAtUtc\" > \"ResumedAtUtc\")) OR " +
                "(NOT \"IsSuspended\" AND \"SuspendedAtUtc\" IS NOT NULL AND " +
                "\"ResumedAtUtc\" IS NOT NULL AND \"ResumedAtUtc\" >= \"SuspendedAtUtc\")");
            table.HasCheckConstraint(
                "CK_adapter_ingress_tenant_controls_version",
                "\"Version\" >= 1");
        });
        builder.HasKey(control => control.Id);
        builder.Property(control => control.Id).HasMaxLength(128).ValueGeneratedNever();
        builder.Property(control => control.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(control => control.LastReasonCode)
            .HasMaxLength(AdapterIngressTenantControl.ReasonCodeMaxLength)
            .IsRequired();
        builder.Property(control => control.LastChangedBy)
            .HasMaxLength(AdapterIngressTenantControl.ActorMaxLength)
            .IsRequired();
        builder.Property(control => control.Version)
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(control => control.ScopeId).IsUnique();
        builder.Ignore(control => control.DomainEvents);
    }
}

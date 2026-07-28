namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class AdapterIngressGlobalControlConfiguration
    : IEntityTypeConfiguration<AdapterIngressGlobalControl>
{
    public void Configure(EntityTypeBuilder<AdapterIngressGlobalControl> builder)
    {
        builder.ToTable("adapter_ingress_global_controls", table =>
        {
            table.HasCheckConstraint(
                "CK_adapter_ingress_global_controls_singleton",
                "\"Id\" = 'adapter-ingress'");
            table.HasCheckConstraint(
                "CK_adapter_ingress_global_controls_lifecycle",
                "(\"IsStopped\" AND \"StoppedAtUtc\" IS NOT NULL AND " +
                "(\"ResumedAtUtc\" IS NULL OR \"StoppedAtUtc\" > \"ResumedAtUtc\")) OR " +
                "(NOT \"IsStopped\" AND \"StoppedAtUtc\" IS NOT NULL AND " +
                "\"ResumedAtUtc\" IS NOT NULL AND \"ResumedAtUtc\" >= \"StoppedAtUtc\")");
            table.HasCheckConstraint(
                "CK_adapter_ingress_global_controls_version",
                "\"Version\" >= 1");
        });
        builder.HasKey(control => control.Id);
        builder.Property(control => control.Id)
            .HasMaxLength(AdapterIngressGlobalControl.SingletonId.Length)
            .ValueGeneratedNever();
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
        builder.Ignore(control => control.DomainEvents);
    }
}

namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class AdapterIngressCredentialConfiguration
    : IEntityTypeConfiguration<AdapterIngressCredential>
{
    public void Configure(EntityTypeBuilder<AdapterIngressCredential> builder)
    {
        builder.ToTable("adapter_ingress_credentials", table =>
        {
            table.HasCheckConstraint(
                "CK_adapter_ingress_credentials_secret_digest",
                "\"SecretHashAlgorithm\" = 'sha256-v1' AND octet_length(\"SecretHash\") = 32");
            table.HasCheckConstraint(
                "CK_adapter_ingress_credentials_lifecycle",
                "(\"State\" IN (1, 3) AND \"RevokedBy\" IS NULL AND \"RevokedAtUtc\" IS NULL) OR " +
                "(\"State\" = 2 AND \"RevokedBy\" IS NOT NULL AND \"RevokedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_adapter_ingress_credentials_expiry",
                "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_adapter_ingress_credentials_slot",
                "\"Slot\" BETWEEN 1 AND 5");
        });
        builder.HasKey(credential => credential.Id);
        builder.HasAlternateKey(credential => new { credential.ScopeId, credential.Id });
        builder.Property(credential => credential.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(credential => credential.Label)
            .HasMaxLength(AdapterIngressCredential.LabelMaxLength).IsRequired();
        builder.Property(credential => credential.SecretHashAlgorithm)
            .HasMaxLength(AdapterIngressCredential.HashAlgorithmMaxLength).IsRequired();
        builder.Property(credential => credential.SecretHash)
            .HasMaxLength(AdapterIngressCredential.SecretHashLength).IsRequired();
        builder.Property(credential => credential.State).HasConversion<int>().IsRequired();
        builder.Property(credential => credential.Slot).IsRequired();
        builder.Property(credential => credential.CreatedBy)
            .HasMaxLength(AdapterIngressCredential.ActorMaxLength).IsRequired();
        builder.Property(credential => credential.RevokedBy)
            .HasMaxLength(AdapterIngressCredential.ActorMaxLength);
        builder.Property(credential => credential.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(credential => new
        {
            credential.ScopeId,
            credential.ConnectionId,
            credential.State,
            credential.ExpiresAtUtc
        });
        builder.HasIndex(credential => new
        {
            credential.ScopeId,
            credential.ConnectionId,
            credential.Slot
        })
            .IsUnique()
            .HasFilter("\"State\" = 1");
        builder.HasIndex(credential => new
        {
            credential.ScopeId,
            credential.ConnectionId,
            credential.CreatedAtUtc
        });
        builder.HasOne<AdapterConnection>()
            .WithMany()
            .HasForeignKey(credential => new { credential.ScopeId, credential.ConnectionId })
            .HasPrincipalKey(connection => new { connection.ScopeId, connection.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(credential => credential.DomainEvents);
    }
}

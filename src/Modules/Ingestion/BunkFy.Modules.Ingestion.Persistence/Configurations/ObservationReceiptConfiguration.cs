namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Runs;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ObservationReceiptConfiguration : IEntityTypeConfiguration<ObservationReceipt>
{
    public void Configure(EntityTypeBuilder<ObservationReceipt> builder)
    {
        builder.ToTable("observation_receipts", table => table.HasCheckConstraint(
            "CK_observation_receipts_policy_evidence",
            "(\"JurisdictionPolicyId\" IS NULL AND \"PolicyOperatingCountryCode\" IS NULL AND " +
            "\"JurisdictionPolicyVersion\" IS NULL AND \"PolicyDataRegionId\" IS NULL AND " +
            "\"PolicyTransferProfileId\" IS NULL AND \"PolicyRetentionPolicyId\" IS NULL AND " +
            "\"PolicyRetentionPolicyVersion\" IS NULL AND \"PolicyContentSha256\" IS NULL AND " +
            "\"PolicyPurposeCode\" IS NULL AND \"PolicyProcessingSurface\" IS NULL AND " +
            "\"PolicySourceProvenance\" IS NULL AND \"PolicyEffectiveAtUtc\" IS NULL AND " +
            "\"PolicyExpiresAtUtc\" IS NULL AND \"PolicyEvaluatedAtUtc\" IS NULL) OR " +
            "(\"JurisdictionPolicyId\" IS NOT NULL AND \"PolicyOperatingCountryCode\" IS NOT NULL AND " +
            "\"JurisdictionPolicyVersion\" IS NOT NULL AND \"PolicyDataRegionId\" IS NOT NULL AND " +
            "\"PolicyTransferProfileId\" IS NOT NULL AND \"PolicyRetentionPolicyId\" IS NOT NULL AND " +
            "\"PolicyRetentionPolicyVersion\" IS NOT NULL AND \"PolicyContentSha256\" IS NOT NULL AND " +
            "\"PolicyPurposeCode\" IS NOT NULL AND \"PolicyProcessingSurface\" IS NOT NULL AND " +
            "\"PolicySourceProvenance\" IS NOT NULL AND \"PolicyEffectiveAtUtc\" IS NOT NULL AND " +
            "\"PolicyExpiresAtUtc\" IS NOT NULL AND \"PolicyEvaluatedAtUtc\" IS NOT NULL AND " +
            "\"JurisdictionPolicyVersion\" > 0 AND \"PolicyRetentionPolicyVersion\" > 0 AND " +
            "char_length(\"PolicyOperatingCountryCode\") = 2 AND char_length(\"PolicyContentSha256\") = 64 AND " +
            "\"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND " +
            "\"PolicyEvaluatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND " +
            "\"PolicyEvaluatedAtUtc\" < \"PolicyExpiresAtUtc\")"));
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(receipt => receipt.SourceRecordType).HasMaxLength(ObservationReceipt.SourceRecordTypeMaxLength).IsRequired();
        builder.Property(receipt => receipt.ExternalId).HasMaxLength(ObservationReceipt.ExternalIdMaxLength).IsRequired();
        builder.Property(receipt => receipt.SourceRevision).HasMaxLength(ObservationReceipt.SourceRevisionMaxLength);
        builder.Property(receipt => receipt.DeduplicationKey).HasMaxLength(ObservationReceipt.DeduplicationKeyMaxLength).IsRequired();
        builder.Property(receipt => receipt.ContentHash).HasMaxLength(ObservationReceipt.ContentHashLength).IsFixedLength().IsRequired();
        builder.Property(receipt => receipt.RawPayloadRetentionState).HasConversion<int>().IsRequired();
        builder.Property(receipt => receipt.RawPayloadVersion).IsConcurrencyToken().IsRequired();
        builder.Property(receipt => receipt.State).HasConversion<int>().IsRequired();
        builder.Property(receipt => receipt.RejectionReason).HasMaxLength(ObservationReceipt.RejectionReasonMaxLength);
        builder.Property(receipt => receipt.ParserType).HasMaxLength(ObservationReceipt.ParserTypeMaxLength);
        builder.OwnsOne(receipt => receipt.CountryPolicyEvidence, evidence =>
        {
            evidence.Property(value => value.OperatingCountryCode)
                .HasColumnName("PolicyOperatingCountryCode")
                .HasMaxLength(ObservationCountryPolicyEvidence.CountryCodeLength);
            evidence.Property(value => value.PolicyId)
                .HasColumnName("JurisdictionPolicyId")
                .HasMaxLength(ObservationCountryPolicyEvidence.PolicyKeyMaxLength);
            evidence.Property(value => value.PolicyVersion).HasColumnName("JurisdictionPolicyVersion");
            evidence.Property(value => value.DataRegionId)
                .HasColumnName("PolicyDataRegionId")
                .HasMaxLength(ObservationCountryPolicyEvidence.PolicyKeyMaxLength);
            evidence.Property(value => value.TransferProfileId)
                .HasColumnName("PolicyTransferProfileId")
                .HasMaxLength(ObservationCountryPolicyEvidence.PolicyKeyMaxLength);
            evidence.Property(value => value.RetentionPolicyId)
                .HasColumnName("PolicyRetentionPolicyId")
                .HasMaxLength(ObservationCountryPolicyEvidence.PolicyKeyMaxLength);
            evidence.Property(value => value.RetentionPolicyVersion).HasColumnName("PolicyRetentionPolicyVersion");
            evidence.Property(value => value.ContentSha256)
                .HasColumnName("PolicyContentSha256")
                .HasMaxLength(ObservationCountryPolicyEvidence.ContentSha256Length)
                .IsFixedLength();
            evidence.Property(value => value.PurposeCode)
                .HasColumnName("PolicyPurposeCode")
                .HasMaxLength(ObservationCountryPolicyEvidence.PolicyKeyMaxLength);
            evidence.Property(value => value.ProcessingSurface)
                .HasColumnName("PolicyProcessingSurface")
                .HasMaxLength(ObservationCountryPolicyEvidence.ProcessingSurfaceMaxLength);
            evidence.Property(value => value.SourceProvenance)
                .HasColumnName("PolicySourceProvenance")
                .HasMaxLength(ObservationCountryPolicyEvidence.PolicyKeyMaxLength);
            evidence.Property(value => value.PolicyEffectiveAtUtc).HasColumnName("PolicyEffectiveAtUtc");
            evidence.Property(value => value.PolicyExpiresAtUtc).HasColumnName("PolicyExpiresAtUtc");
            evidence.Property(value => value.EvaluatedAtUtc).HasColumnName("PolicyEvaluatedAtUtc");
        });
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.ConnectionId, receipt.OperationId }).IsUnique();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.ConnectionId, receipt.DeduplicationKey }).IsUnique();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.ConnectionId, receipt.State, receipt.ReceivedAtUtc });
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.RawPayloadRetentionState,
            receipt.RawPayloadRetainUntilUtc,
            receipt.RawPayloadPurgeStartedAtUtc
        });
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.SourceReceiptId, receipt.ReceivedAtUtc });
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ActiveReprocessingAttemptId,
            receipt.ReprocessingReservationExpiresAtUtc
        });
        builder.HasOne<AdapterConnection>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.ConnectionId })
            .HasPrincipalKey(connection => new { connection.ScopeId, connection.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IngestionRun>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.RunId })
            .HasPrincipalKey(run => new { run.ScopeId, run.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ObservationReceipt>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.SourceReceiptId })
            .HasPrincipalKey(receipt => new { receipt.ScopeId, receipt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ObservationReprocessingAttempt>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.ReprocessingAttemptId })
            .HasPrincipalKey(attempt => new { attempt.ScopeId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}

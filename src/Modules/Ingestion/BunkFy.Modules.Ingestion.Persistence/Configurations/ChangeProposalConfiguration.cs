namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ChangeProposalConfiguration : IEntityTypeConfiguration<ChangeProposal>
{
    public void Configure(EntityTypeBuilder<ChangeProposal> builder)
    {
        builder.ToTable("change_proposals");
        builder.HasKey(proposal => proposal.Id);
        builder.HasAlternateKey(proposal => new { proposal.ScopeId, proposal.Id });
        builder.Property(proposal => proposal.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(proposal => proposal.ReasonCode).HasMaxLength(ChangeProposal.ReasonCodeMaxLength).IsRequired();
        builder.Property(proposal => proposal.Diff).HasMaxLength(ChangeProposal.DiffMaxLength);
        builder.Property(proposal => proposal.State).HasConversion<int>().IsRequired();
        builder.Property(proposal => proposal.DecisionActor).HasMaxLength(ChangeProposal.ActorMaxLength);
        builder.Property(proposal => proposal.DecisionReason).HasMaxLength(ChangeProposal.ReasonMaxLength);
        builder.Property(proposal => proposal.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(proposal => new { proposal.ScopeId, proposal.ReceiptId }).IsUnique();
        builder.HasIndex(proposal => new { proposal.ScopeId, proposal.PropertyId, proposal.State, proposal.CreatedAtUtc });
        builder.HasIndex(proposal => new { proposal.ScopeId, proposal.ReservationId, proposal.CreatedAtUtc });
        builder.HasIndex(proposal => new
        {
            proposal.ScopeId,
            proposal.ConnectionId,
            proposal.SensitiveDataRetainUntilUtc,
            proposal.SensitiveDataRedactedAtUtc
        });
        builder.HasOne<AdapterConnection>()
            .WithMany()
            .HasForeignKey(proposal => new { proposal.ScopeId, proposal.ConnectionId })
            .HasPrincipalKey(connection => new { connection.ScopeId, connection.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ObservationReceipt>()
            .WithMany()
            .HasForeignKey(proposal => new { proposal.ScopeId, proposal.ReceiptId })
            .HasPrincipalKey(receipt => new { receipt.ScopeId, receipt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(proposal => proposal.DomainEvents);
    }
}

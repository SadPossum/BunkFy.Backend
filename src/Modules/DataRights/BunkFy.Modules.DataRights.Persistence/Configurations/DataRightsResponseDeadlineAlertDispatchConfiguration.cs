namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsResponseDeadlineAlertDispatchConfiguration
    : IEntityTypeConfiguration<DataRightsResponseDeadlineAlertDispatchReceipt>
{
    public void Configure(
        EntityTypeBuilder<DataRightsResponseDeadlineAlertDispatchReceipt> builder)
    {
        builder.ToTable("response_deadline_alert_dispatches", table =>
        {
            table.HasCheckConstraint(
                "CK_response_deadline_alert_dispatches_kind",
                "\"AlertKind\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_response_deadline_alert_dispatches_timing",
                "(\"AlertKind\" = 1 AND \"DispatchedAtUtc\" < \"DueAtUtc\") OR " +
                "(\"AlertKind\" = 2 AND \"DispatchedAtUtc\" >= \"DueAtUtc\")");
        });
        builder.HasKey(dispatch => dispatch.Id);
        builder.HasAlternateKey(dispatch => new
        {
            dispatch.ScopeId,
            dispatch.Id
        });
        builder.Property(dispatch => dispatch.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(dispatch => dispatch.AlertKind)
            .HasConversion<int>()
            .IsRequired();
        builder.HasIndex(dispatch => new
        {
            dispatch.ScopeId,
            dispatch.CaseId,
            dispatch.AlertKind
        }).IsUnique();
        builder.HasIndex(dispatch => new
        {
            dispatch.ScopeId,
            dispatch.AlertKind,
            dispatch.DispatchedAtUtc
        });
        builder.HasOne<DataRightsCase>()
            .WithMany()
            .HasForeignKey(dispatch => new
            {
                dispatch.ScopeId,
                dispatch.CaseId
            })
            .HasPrincipalKey(dataRightsCase => new
            {
                dataRightsCase.ScopeId,
                dataRightsCase.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

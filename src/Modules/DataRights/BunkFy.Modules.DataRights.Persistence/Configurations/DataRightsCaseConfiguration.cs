namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsCaseConfiguration : IEntityTypeConfiguration<DataRightsCase>
{
    public void Configure(EntityTypeBuilder<DataRightsCase> builder)
    {
        builder.ToTable("cases", table =>
        {
            table.HasCheckConstraint("CK_data_rights_cases_version", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_data_rights_cases_kind", "\"Kind\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_operations",
                "\"RequestedOperations\" BETWEEN 1 AND 31");
            table.HasCheckConstraint(
                "CK_data_rights_cases_requester",
                "\"RequesterRelationship\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_requester_scope",
                "(\"Kind\" = 1 AND \"RequesterRelationship\" IN (1, 2, 3)) OR " +
                "(\"Kind\" = 2 AND \"RequesterRelationship\" IN (3, 4))");
            table.HasCheckConstraint(
                "CK_data_rights_cases_verification",
                "\"VerificationStatus\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_routing",
                "\"RoutingStatus\" IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_status",
                "\"Status\" BETWEEN 1 AND 11");
            table.HasCheckConstraint(
                "CK_data_rights_cases_property_scope",
                "(\"Kind\" = 1 AND \"PropertyId\" IS NOT NULL) OR " +
                "(\"Kind\" = 2 AND \"PropertyId\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_timestamps",
                "\"LastChangedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "(\"DueAtUtc\" IS NULL OR \"DueAtUtc\" >= \"CreatedAtUtc\")");
            table.HasCheckConstraint(
                "CK_data_rights_cases_created_by",
                "length(trim(\"CreatedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_data_rights_cases_last_changed_by",
                "length(trim(\"LastChangedBy\")) > 0");
        });
        builder.HasKey(dataRightsCase => dataRightsCase.Id);
        builder.HasAlternateKey(dataRightsCase => new
        {
            dataRightsCase.ScopeId,
            dataRightsCase.Id
        });
        builder.Property(dataRightsCase => dataRightsCase.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.Kind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.RequestedOperations)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.RequesterRelationship)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.VerificationStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.RoutingStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.CreatedBy)
            .HasMaxLength(DataRightsCase.ActorIdMaxLength)
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.LastChangedBy)
            .HasMaxLength(DataRightsCase.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(dataRightsCase => new
        {
            dataRightsCase.ScopeId,
            dataRightsCase.PropertyId,
            dataRightsCase.Status,
            dataRightsCase.CreatedAtUtc,
            dataRightsCase.Id
        });
        builder.OwnsMany(dataRightsCase => dataRightsCase.SelectedSubjects, subjects =>
        {
            subjects.ToTable("selected_subjects", table =>
            {
                table.HasCheckConstraint(
                    "CK_data_rights_selected_subjects_owner",
                    "length(trim(\"OwnerKey\")) > 0");
                table.HasCheckConstraint(
                    "CK_data_rights_selected_subjects_record_type",
                    "length(trim(\"RecordType\")) > 0");
                table.HasCheckConstraint(
                    "CK_data_rights_selected_subjects_record_version",
                    "\"RecordVersion\" >= 1");
                table.HasCheckConstraint(
                    "CK_data_rights_selected_subjects_selected_by",
                    "length(trim(\"SelectedBy\")) > 0");
            });
            subjects.WithOwner().HasForeignKey("CaseId");
            subjects.Property<Guid>("CaseId");
            subjects.HasKey(
                "CaseId",
                nameof(DataRightsSubjectCoordinate.OwnerKey),
                nameof(DataRightsSubjectCoordinate.RecordType),
                nameof(DataRightsSubjectCoordinate.RecordId));
            subjects.Property(subject => subject.OwnerKey)
                .HasMaxLength(DataRightsSubjectCoordinate.OwnerKeyMaxLength)
                .IsRequired();
            subjects.Property(subject => subject.RecordType)
                .HasMaxLength(DataRightsSubjectCoordinate.RecordTypeMaxLength)
                .IsRequired();
            subjects.Property(subject => subject.RecordId).ValueGeneratedNever();
            subjects.Property(subject => subject.RecordVersion).IsRequired();
            subjects.Property(subject => subject.SelectedBy)
                .HasMaxLength(DataRightsCase.ActorIdMaxLength)
                .IsRequired();
            subjects.Property(subject => subject.SelectedAtUtc).IsRequired();
        });
        builder.Navigation(dataRightsCase => dataRightsCase.SelectedSubjects)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(dataRightsCase => dataRightsCase.DomainEvents);
    }
}

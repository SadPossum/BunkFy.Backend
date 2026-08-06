namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationManagementOperationConfiguration
    : IEntityTypeConfiguration<ReservationManagementOperation>
{
    public void Configure(EntityTypeBuilder<ReservationManagementOperation> builder)
    {
        builder.ToTable("management_operations", table =>
        {
            table.HasCheckConstraint(
                "CK_management_operations_business_date",
                "(\"Kind\" = 1 AND \"BusinessDate\" IS NULL) OR " +
                "(\"Kind\" IN (2, 3, 4) AND \"BusinessDate\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_management_operations_kind",
                "\"Kind\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_management_operations_expected_version",
                "\"ExpectedVersion\" > 0");
        });
        builder.HasKey(operation => new
        {
            operation.ScopeId,
            operation.ReservationId,
            operation.Id
        });
        builder.Property(operation => operation.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(operation => operation.Kind)
            .HasConversion<int>()
            .IsRequired();
        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(operation => new
            {
                operation.ScopeId,
                operation.ReservationId
            })
            .HasPrincipalKey(reservation => new
            {
                reservation.ScopeId,
                reservation.Id
            })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(operation => new
        {
            operation.ScopeId,
            operation.PropertyId,
            operation.CreatedAtUtc,
            operation.Id
        });
    }
}

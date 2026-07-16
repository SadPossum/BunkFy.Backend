namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationArrivalReminderConfiguration
    : IEntityTypeConfiguration<ReservationArrivalReminder>
{
    public void Configure(EntityTypeBuilder<ReservationArrivalReminder> builder)
    {
        builder.ToTable("arrival_reminders", table =>
        {
            table.HasCheckConstraint("CK_arrival_reminders_details_revision", "\"DetailsRevision\" >= 1");
            table.HasCheckConstraint("CK_arrival_reminders_lead_time", "\"LeadTimeMinutes\" > 0");
            table.HasCheckConstraint(
                "CK_arrival_reminders_dispatch_state",
                "(\"State\" = 2 AND \"DispatchedAtUtc\" IS NOT NULL) OR " +
                "(\"State\" <> 2 AND \"DispatchedAtUtc\" IS NULL)");
        });
        builder.HasKey(reminder => reminder.Id);
        builder.HasAlternateKey(reminder => new { reminder.ScopeId, reminder.Id });
        builder.Property(reminder => reminder.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(reminder => reminder.TimeZoneId)
            .HasMaxLength(ReservationsContractLimits.TimeZoneIdMaxLength)
            .IsRequired();
        builder.Property(reminder => reminder.ExpectedArrivalTime)
            .HasColumnType("time(0) without time zone");
        builder.Property(reminder => reminder.State).HasConversion<int>().IsRequired();
        builder.Property(reminder => reminder.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(reminder => new
        {
            reminder.ScopeId,
            reminder.ReservationId,
            reminder.DetailsRevision,
            reminder.TimeZoneId,
            reminder.LeadTimeMinutes
        }).IsUnique();
        builder.HasIndex(reminder => new { reminder.ScopeId, reminder.State, reminder.DueAtUtc });
        builder.HasIndex(reminder => new { reminder.ScopeId, reminder.PropertyId, reminder.State });
    }
}

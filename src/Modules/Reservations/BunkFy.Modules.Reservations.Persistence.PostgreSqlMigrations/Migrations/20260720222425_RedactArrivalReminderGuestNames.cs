using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class RedactArrivalReminderGuestNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE reservations.outbox_messages
                SET "Payload" = jsonb_set(
                    "Payload"::jsonb,
                    '{primaryGuestName}',
                    to_jsonb('A guest'::text),
                    false)::text
                WHERE "EventType" = 'BunkFy.Modules.Reservations.Contracts.ReservationArrivalReminderDueIntegrationEvent'
                  AND "Version" = 1
                  AND "Payload"::jsonb ? 'primaryGuestName';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Redacted personal data cannot and must not be reconstructed.
        }
    }
}

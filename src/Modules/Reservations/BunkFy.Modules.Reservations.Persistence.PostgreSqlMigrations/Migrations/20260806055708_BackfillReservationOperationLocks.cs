using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class BackfillReservationOperationLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "reservations"."reservation_operation_locks"
                    ("Id", "ReservationId", "Revision", "ScopeId")
                SELECT
                    gen_random_uuid(),
                    reservation."Id",
                    1,
                    reservation."ScopeId"
                FROM "reservations"."reservations" AS reservation
                ON CONFLICT ("ScopeId", "ReservationId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Provisioned lock rows are valid operational state and remain reusable.
        }
    }
}

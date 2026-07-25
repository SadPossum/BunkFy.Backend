using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDataRightsDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailSearch",
                schema: "reservations",
                table: "reservations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmailSearch",
                schema: "reservations",
                table: "reservations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPhoneSearch",
                schema: "reservations",
                table: "reservations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPrimaryGuestNameSearch",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneSearch",
                schema: "reservations",
                table: "reservations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryGuestNameSearch",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE reservations.reservations
                SET
                    "PrimaryGuestNameSearch" = upper(btrim("PrimaryGuestName")),
                    "EmailSearch" = CASE
                        WHEN nullif(btrim("Email"), '') IS NULL THEN NULL
                        ELSE upper(btrim("Email"))
                    END,
                    "PhoneSearch" = CASE
                        WHEN nullif(btrim("Phone"), '') IS NULL THEN NULL
                        ELSE upper(btrim("Phone"))
                    END,
                    "PendingPrimaryGuestNameSearch" = CASE
                        WHEN nullif(btrim("PendingPrimaryGuestName"), '') IS NULL THEN NULL
                        ELSE upper(btrim("PendingPrimaryGuestName"))
                    END,
                    "PendingEmailSearch" = CASE
                        WHEN nullif(btrim("PendingEmail"), '') IS NULL THEN NULL
                        ELSE upper(btrim("PendingEmail"))
                    END,
                    "PendingPhoneSearch" = CASE
                        WHEN nullif(btrim("PendingPhone"), '') IS NULL THEN NULL
                        ELSE upper(btrim("PendingPhone"))
                    END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryGuestNameSearch",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ScopeId_PropertyId_EmailSearch_Id",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "ScopeId", "PropertyId", "EmailSearch", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ScopeId_PropertyId_PendingEmailSearch_Id",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "ScopeId", "PropertyId", "PendingEmailSearch", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ScopeId_PropertyId_PendingPhoneSearch_Id",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "ScopeId", "PropertyId", "PendingPhoneSearch", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ScopeId_PropertyId_PhoneSearch_Id",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "ScopeId", "PropertyId", "PhoneSearch", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reservations_ScopeId_PropertyId_EmailSearch_Id",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_ScopeId_PropertyId_PendingEmailSearch_Id",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_ScopeId_PropertyId_PendingPhoneSearch_Id",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_ScopeId_PropertyId_PhoneSearch_Id",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "EmailSearch",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingEmailSearch",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingPhoneSearch",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingPrimaryGuestNameSearch",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PhoneSearch",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PrimaryGuestNameSearch",
                schema: "reservations",
                table: "reservations");
        }
    }
}

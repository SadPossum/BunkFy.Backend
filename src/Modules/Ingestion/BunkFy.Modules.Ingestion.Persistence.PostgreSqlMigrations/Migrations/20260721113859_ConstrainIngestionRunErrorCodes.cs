using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ConstrainIngestionRunErrorCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ingestion."runs"
                SET "ErrorMessage" = CASE
                    WHEN "State" IN (1, 2) THEN NULL
                    WHEN "State" IN (3, 4, 5)
                        AND length(btrim("ErrorMessage")) BETWEEN 1 AND 200
                        AND lower(btrim("ErrorMessage")) ~ '^[a-z0-9][a-z0-9._-]{0,199}$'
                        THEN lower(btrim("ErrorMessage"))
                    WHEN "State" IN (3, 4, 5) THEN 'ingestion.legacy-adapter-failure'
                    ELSE NULL
                END;
                """);

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                schema: "ingestion",
                table: "runs",
                newName: "ErrorCode");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "ingestion",
                table: "runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_runs_error_code_format",
                schema: "ingestion",
                table: "runs",
                sql: "\"ErrorCode\" IS NULL OR \"ErrorCode\" ~ '^[a-z0-9][a-z0-9._-]{0,199}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_runs_error_code_lifecycle",
                schema: "ingestion",
                table: "runs",
                sql: "(\"State\" IN (1, 2) AND \"ErrorCode\" IS NULL) OR (\"State\" IN (3, 4, 5) AND \"ErrorCode\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_runs_error_code_format",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_runs_error_code_lifecycle",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "ingestion",
                table: "runs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "ErrorCode",
                schema: "ingestion",
                table: "runs",
                newName: "ErrorMessage");
        }
    }
}

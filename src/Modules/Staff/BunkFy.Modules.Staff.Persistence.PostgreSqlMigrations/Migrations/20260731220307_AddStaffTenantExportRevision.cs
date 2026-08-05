using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffTenantExportRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_revisions",
                schema: "staff",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_revisions", x => x.ScopeId);
                    table.CheckConstraint("CK_staff_tenant_revision_positive", "\"Revision\" > 0");
                });

            migrationBuilder.Sql(
                """
                CREATE TRIGGER "TR_staff_retention_anonymisation_receipts_append_only"
                BEFORE UPDATE OR DELETE
                ON "staff"."staff_retention_anonymisation_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_receipt_mutation();

                CREATE FUNCTION "staff".prevent_tombstone_deletion()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'Staff anonymisation tombstones cannot be deleted';
                END;
                $function$;

                CREATE TRIGGER "TR_staff_anonymisation_tombstones_no_delete"
                BEFORE DELETE
                ON "staff"."staff_anonymisation_tombstones"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_tombstone_deletion();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_retention_anonymisation_receipts_append_only"
                ON "staff"."staff_retention_anonymisation_receipts";

                DROP TRIGGER IF EXISTS
                    "TR_staff_anonymisation_tombstones_no_delete"
                ON "staff"."staff_anonymisation_tombstones";

                DROP FUNCTION IF EXISTS "staff".prevent_tombstone_deletion();
                """);

            migrationBuilder.DropTable(
                name: "tenant_revisions",
                schema: "staff");
        }
    }
}

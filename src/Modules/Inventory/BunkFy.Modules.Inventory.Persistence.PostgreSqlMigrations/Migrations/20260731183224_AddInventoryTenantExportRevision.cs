using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTenantExportRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_revisions",
                schema: "inventory",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_revisions", x => x.ScopeId);
                    table.CheckConstraint("CK_inventory_tenant_revision_positive", "\"Revision\" > 0");
                });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION inventory.reject_anonymisation_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'inventory anonymisation receipts are append-only';
                END;
                $$;

                CREATE TRIGGER allocation_anonymisation_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON inventory.allocation_anonymisation_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    inventory.reject_anonymisation_receipt_mutation();

                CREATE TRIGGER allocation_anonymisation_restore_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON inventory.allocation_anonymisation_restore_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    inventory.reject_anonymisation_receipt_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    allocation_anonymisation_receipts_append_only
                ON inventory.allocation_anonymisation_receipts;

                DROP TRIGGER IF EXISTS
                    allocation_anonymisation_restore_receipts_append_only
                ON inventory.allocation_anonymisation_restore_receipts;

                DROP FUNCTION IF EXISTS
                    inventory.reject_anonymisation_receipt_mutation();
                """);

            migrationBuilder.DropTable(
                name: "tenant_revisions",
                schema: "inventory");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionTenantExportRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_revisions",
                schema: "ingestion",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_revisions", x => x.ScopeId);
                    table.CheckConstraint("CK_ingestion_tenant_revision_positive", "\"Revision\" > 0");
                });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION ingestion.reject_anonymisation_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'ingestion anonymisation receipts are append-only';
                END;
                $$;

                CREATE TRIGGER anonymisation_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON ingestion.anonymisation_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    ingestion.reject_anonymisation_receipt_mutation();

                CREATE FUNCTION ingestion.reject_anonymisation_tombstone_delete()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'ingestion anonymisation tombstones cannot be deleted';
                END;
                $$;

                CREATE TRIGGER anonymisation_tombstones_no_delete
                BEFORE DELETE
                ON ingestion.anonymisation_tombstones
                FOR EACH ROW
                EXECUTE FUNCTION
                    ingestion.reject_anonymisation_tombstone_delete();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    anonymisation_receipts_append_only
                ON ingestion.anonymisation_receipts;

                DROP TRIGGER IF EXISTS
                    anonymisation_tombstones_no_delete
                ON ingestion.anonymisation_tombstones;

                DROP FUNCTION IF EXISTS
                    ingestion.reject_anonymisation_receipt_mutation();

                DROP FUNCTION IF EXISTS
                    ingestion.reject_anonymisation_tombstone_delete();
                """);

            migrationBuilder.DropTable(
                name: "tenant_revisions",
                schema: "ingestion");
        }
    }
}

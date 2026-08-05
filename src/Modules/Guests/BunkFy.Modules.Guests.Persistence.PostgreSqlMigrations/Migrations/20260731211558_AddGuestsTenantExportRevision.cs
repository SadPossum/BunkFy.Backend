using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestsTenantExportRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_revisions",
                schema: "guests",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_revisions", x => x.ScopeId);
                    table.CheckConstraint("CK_guests_tenant_revision_positive", "\"Revision\" > 0");
                });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION guests.reject_guest_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'guest data-rights receipts are append-only';
                END;
                $$;

                CREATE TRIGGER guest_data_rights_correction_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON guests.guest_data_rights_correction_receipts
                FOR EACH ROW
                EXECUTE FUNCTION guests.reject_guest_receipt_mutation();

                CREATE TRIGGER guest_processing_restriction_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON guests.guest_processing_restriction_receipts
                FOR EACH ROW
                EXECUTE FUNCTION guests.reject_guest_receipt_mutation();

                CREATE TRIGGER guest_data_hold_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON guests.data_hold_receipts
                FOR EACH ROW
                EXECUTE FUNCTION guests.reject_guest_receipt_mutation();

                CREATE TRIGGER guest_anonymisation_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON guests.guest_anonymisation_receipts
                FOR EACH ROW
                EXECUTE FUNCTION guests.reject_guest_receipt_mutation();

                CREATE TRIGGER guest_anonymisation_restore_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON guests.guest_anonymisation_restore_receipts
                FOR EACH ROW
                EXECUTE FUNCTION guests.reject_guest_receipt_mutation();

                CREATE TRIGGER guest_retention_anonymisation_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON guests.guest_retention_anonymisation_receipts
                FOR EACH ROW
                EXECUTE FUNCTION guests.reject_guest_receipt_mutation();

                CREATE FUNCTION guests.reject_guest_tombstone_deletion()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'guest anonymisation tombstones cannot be deleted';
                END;
                $$;

                CREATE TRIGGER guest_anonymisation_tombstones_delete_only
                BEFORE DELETE
                ON guests.guest_anonymisation_tombstones
                FOR EACH ROW
                EXECUTE FUNCTION guests.reject_guest_tombstone_deletion();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    guest_data_rights_correction_receipts_append_only
                ON guests.guest_data_rights_correction_receipts;

                DROP TRIGGER IF EXISTS
                    guest_processing_restriction_receipts_append_only
                ON guests.guest_processing_restriction_receipts;

                DROP TRIGGER IF EXISTS
                    guest_data_hold_receipts_append_only
                ON guests.data_hold_receipts;

                DROP TRIGGER IF EXISTS
                    guest_anonymisation_receipts_append_only
                ON guests.guest_anonymisation_receipts;

                DROP TRIGGER IF EXISTS
                    guest_anonymisation_restore_receipts_append_only
                ON guests.guest_anonymisation_restore_receipts;

                DROP TRIGGER IF EXISTS
                    guest_retention_anonymisation_receipts_append_only
                ON guests.guest_retention_anonymisation_receipts;

                DROP TRIGGER IF EXISTS
                    guest_anonymisation_tombstones_delete_only
                ON guests.guest_anonymisation_tombstones;

                DROP FUNCTION IF EXISTS
                    guests.reject_guest_receipt_mutation();

                DROP FUNCTION IF EXISTS
                    guests.reject_guest_tombstone_deletion();
                """);

            migrationBuilder.DropTable(
                name: "tenant_revisions",
                schema: "guests");
        }
    }
}

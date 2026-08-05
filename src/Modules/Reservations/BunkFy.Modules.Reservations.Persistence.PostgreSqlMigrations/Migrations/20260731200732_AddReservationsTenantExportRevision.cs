using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationsTenantExportRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_revisions",
                schema: "reservations",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_revisions", x => x.ScopeId);
                    table.CheckConstraint("CK_reservations_tenant_revision_positive", "\"Revision\" > 0");
                });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION reservations.reject_reservation_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'reservation data-rights receipts are append-only';
                END;
                $$;

                CREATE TRIGGER reservation_data_rights_correction_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON reservations.reservation_data_rights_correction_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    reservations.reject_reservation_receipt_mutation();

                CREATE TRIGGER reservation_processing_restriction_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON reservations.reservation_processing_restriction_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    reservations.reject_reservation_receipt_mutation();

                CREATE TRIGGER reservation_data_hold_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON reservations.reservation_data_hold_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    reservations.reject_reservation_receipt_mutation();

                CREATE TRIGGER reservation_anonymisation_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON reservations.reservation_anonymisation_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    reservations.reject_reservation_receipt_mutation();

                CREATE TRIGGER reservation_anonymisation_restore_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON reservations.reservation_anonymisation_restore_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    reservations.reject_reservation_receipt_mutation();

                CREATE TRIGGER reservation_retention_anonymisation_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON reservations.reservation_retention_anonymisation_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    reservations.reject_reservation_receipt_mutation();

                CREATE FUNCTION reservations.reject_reservation_tombstone_deletion()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'reservation anonymisation tombstones cannot be deleted';
                END;
                $$;

                CREATE TRIGGER reservation_anonymisation_tombstones_delete_only
                BEFORE DELETE
                ON reservations.reservation_anonymisation_tombstones
                FOR EACH ROW
                EXECUTE FUNCTION
                    reservations.reject_reservation_tombstone_deletion();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    reservation_data_rights_correction_receipts_append_only
                ON reservations.reservation_data_rights_correction_receipts;

                DROP TRIGGER IF EXISTS
                    reservation_processing_restriction_receipts_append_only
                ON reservations.reservation_processing_restriction_receipts;

                DROP TRIGGER IF EXISTS
                    reservation_data_hold_receipts_append_only
                ON reservations.reservation_data_hold_receipts;

                DROP TRIGGER IF EXISTS
                    reservation_anonymisation_receipts_append_only
                ON reservations.reservation_anonymisation_receipts;

                DROP TRIGGER IF EXISTS
                    reservation_anonymisation_restore_receipts_append_only
                ON reservations.reservation_anonymisation_restore_receipts;

                DROP TRIGGER IF EXISTS
                    reservation_retention_anonymisation_receipts_append_only
                ON reservations.reservation_retention_anonymisation_receipts;

                DROP TRIGGER IF EXISTS
                    reservation_anonymisation_tombstones_delete_only
                ON reservations.reservation_anonymisation_tombstones;

                DROP FUNCTION IF EXISTS
                    reservations.reject_reservation_receipt_mutation();

                DROP FUNCTION IF EXISTS
                    reservations.reject_reservation_tombstone_deletion();
                """);

            migrationBuilder.DropTable(
                name: "tenant_revisions",
                schema: "reservations");
        }
    }
}

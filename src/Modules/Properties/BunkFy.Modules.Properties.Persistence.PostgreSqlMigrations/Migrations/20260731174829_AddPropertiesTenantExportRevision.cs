using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertiesTenantExportRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_revisions",
                schema: "properties",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_revisions", x => x.ScopeId);
                    table.CheckConstraint("CK_properties_tenant_revision_positive", "\"Revision\" > 0");
                });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION properties.reject_property_governance_revision_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'property governance revisions are append-only';
                END;
                $$;

                CREATE TRIGGER property_governance_revisions_append_only
                BEFORE UPDATE OR DELETE
                ON properties.property_governance_revisions
                FOR EACH ROW
                EXECUTE FUNCTION properties.reject_property_governance_revision_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS property_governance_revisions_append_only
                ON properties.property_governance_revisions;

                DROP FUNCTION IF EXISTS
                    properties.reject_property_governance_revision_mutation();
                """);

            migrationBuilder.DropTable(
                name: "tenant_revisions",
                schema: "properties");
        }
    }
}

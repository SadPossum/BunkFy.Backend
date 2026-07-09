using Catalog.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CatalogDbContext))]
    [Migration("20260705121130_AddInboxStatusUnknown")]
    public partial class AddInboxStatusUnknown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "catalog"."inbox_messages"
                SET "Status" = "Status" + 1
                WHERE "Status" IN (0, 1, 2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "catalog"."inbox_messages"
                SET "Status" = "Status" - 1
                WHERE "Status" IN (1, 2, 3, 4);
                """);
        }
    }
}

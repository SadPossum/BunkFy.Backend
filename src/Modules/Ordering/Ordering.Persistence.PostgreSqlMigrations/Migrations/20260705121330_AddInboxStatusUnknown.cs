using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ordering.Persistence;

#nullable disable

namespace Ordering.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OrderingDbContext))]
    [Migration("20260705121330_AddInboxStatusUnknown")]
    public partial class AddInboxStatusUnknown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ordering"."inbox_messages"
                SET "Status" = "Status" + 1
                WHERE "Status" IN (0, 1, 2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ordering"."inbox_messages"
                SET "Status" = "Status" - 1
                WHERE "Status" IN (1, 2, 3, 4);
                """);
        }
    }
}

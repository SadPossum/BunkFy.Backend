using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAdapterIngressCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adapter_ingress_credentials",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SecretHashAlgorithm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SecretHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAuthenticatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adapter_ingress_credentials", x => x.Id);
                    table.UniqueConstraint("AK_adapter_ingress_credentials_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_adapter_ingress_credentials_expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_adapter_ingress_credentials_lifecycle", "(\"State\" IN (1, 3) AND \"RevokedBy\" IS NULL AND \"RevokedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"RevokedBy\" IS NOT NULL AND \"RevokedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_adapter_ingress_credentials_secret_digest", "\"SecretHashAlgorithm\" = 'sha256-v1' AND octet_length(\"SecretHash\") = 32");
                    table.CheckConstraint("CK_adapter_ingress_credentials_slot", "\"Slot\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_adapter_ingress_credentials_adapter_connections_ScopeId_Con~",
                        columns: x => new { x.ScopeId, x.ConnectionId },
                        principalSchema: "ingestion",
                        principalTable: "adapter_connections",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adapter_ingress_credentials_ScopeId_ConnectionId_CreatedAtU~",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                columns: new[] { "ScopeId", "ConnectionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_adapter_ingress_credentials_ScopeId_ConnectionId_Slot",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                columns: new[] { "ScopeId", "ConnectionId", "Slot" },
                unique: true,
                filter: "\"State\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_adapter_ingress_credentials_ScopeId_ConnectionId_State_Expi~",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                columns: new[] { "ScopeId", "ConnectionId", "State", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adapter_ingress_credentials",
                schema: "ingestion");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAdapterIngressControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adapter_ingress_global_controls",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IsStopped = table.Column<bool>(type: "boolean", nullable: false),
                    LastReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StoppedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adapter_ingress_global_controls", x => x.Id);
                    table.CheckConstraint("CK_adapter_ingress_global_controls_lifecycle", "(\"IsStopped\" AND \"StoppedAtUtc\" IS NOT NULL AND (\"ResumedAtUtc\" IS NULL OR \"StoppedAtUtc\" > \"ResumedAtUtc\")) OR (NOT \"IsStopped\" AND \"StoppedAtUtc\" IS NOT NULL AND \"ResumedAtUtc\" IS NOT NULL AND \"ResumedAtUtc\" >= \"StoppedAtUtc\")");
                    table.CheckConstraint("CK_adapter_ingress_global_controls_singleton", "\"Id\" = 'adapter-ingress'");
                    table.CheckConstraint("CK_adapter_ingress_global_controls_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "adapter_ingress_tenant_controls",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    LastReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SuspendedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adapter_ingress_tenant_controls", x => x.Id);
                    table.CheckConstraint("CK_adapter_ingress_tenant_controls_identity", "\"Id\" = \"ScopeId\"");
                    table.CheckConstraint("CK_adapter_ingress_tenant_controls_lifecycle", "(\"IsSuspended\" AND \"SuspendedAtUtc\" IS NOT NULL AND (\"ResumedAtUtc\" IS NULL OR \"SuspendedAtUtc\" > \"ResumedAtUtc\")) OR (NOT \"IsSuspended\" AND \"SuspendedAtUtc\" IS NOT NULL AND \"ResumedAtUtc\" IS NOT NULL AND \"ResumedAtUtc\" >= \"SuspendedAtUtc\")");
                    table.CheckConstraint("CK_adapter_ingress_tenant_controls_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_adapter_ingress_tenant_controls_ScopeId",
                schema: "ingestion",
                table: "adapter_ingress_tenant_controls",
                column: "ScopeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adapter_ingress_global_controls",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "adapter_ingress_tenant_controls",
                schema: "ingestion");
        }
    }
}

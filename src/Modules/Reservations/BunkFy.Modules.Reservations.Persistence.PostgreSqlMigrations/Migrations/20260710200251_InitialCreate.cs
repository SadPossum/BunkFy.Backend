using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reservations");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Handler = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.Id, x.Handler });
                });

            migrationBuilder.CreateTable(
                name: "inventory_allocation_projections",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: true),
                    Departure = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_allocation_projections", x => x.Id);
                    table.UniqueConstraint("AK_inventory_allocation_projections_ScopeId_Id", x => new { x.ScopeId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "inventory_block_projections",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: true),
                    Departure = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_block_projections", x => x.Id);
                    table.UniqueConstraint("AK_inventory_block_projections_ScopeId_Id", x => new { x.ScopeId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "inventory_unit_projections",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    BedId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsTopologyActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSellable = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationVersion = table.Column<long>(type: "bigint", nullable: false),
                    UnitVersion = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_unit_projections", x => x.Id);
                    table.UniqueConstraint("AK_inventory_unit_projections_ScopeId_Id", x => new { x.ScopeId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "projection_rebuild_checkpoints",
                schema: "reservations",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ProcessedCount = table.Column<long>(type: "bigint", nullable: false),
                    WrittenCount = table.Column<long>(type: "bigint", nullable: false),
                    SkippedCount = table.Column<long>(type: "bigint", nullable: false),
                    FailedCount = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionVersion = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projection_rebuild_checkpoints", x => new { x.ScopeId, x.ProjectionName, x.RunId });
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllocationVersion = table.Column<long>(type: "bigint", nullable: true),
                    AllocationRejection = table.Column<int>(type: "integer", nullable: true),
                    ReleaseRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReleaseRejectionCode = table.Column<int>(type: "integer", nullable: true),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    Departure = table.Column<DateOnly>(type: "date", nullable: false),
                    PrimaryGuestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GuestCount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.Id);
                    table.UniqueConstraint("AK_reservations_ScopeId_Id", x => new { x.ScopeId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "inventory_allocation_unit_projections",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_allocation_unit_projections", x => new { x.ScopeId, x.AllocationId, x.Id });
                    table.ForeignKey(
                        name: "FK_inventory_allocation_unit_projections_inventory_allocation_~",
                        columns: x => new { x.ScopeId, x.AllocationId },
                        principalSchema: "reservations",
                        principalTable: "inventory_allocation_projections",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requested_inventory_units",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requested_inventory_units", x => new { x.ScopeId, x.ReservationId, x.Id });
                    table.ForeignKey(
                        name: "FK_requested_inventory_units_reservations_ScopeId_ReservationId",
                        columns: x => new { x.ScopeId, x.ReservationId },
                        principalSchema: "reservations",
                        principalTable: "reservations",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "reservations",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_allocation_projections_ScopeId_PropertyId_Status_~",
                schema: "reservations",
                table: "inventory_allocation_projections",
                columns: new[] { "ScopeId", "PropertyId", "Status", "Arrival", "Departure" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_allocation_unit_projections_ScopeId_Id_Allocation~",
                schema: "reservations",
                table: "inventory_allocation_unit_projections",
                columns: new[] { "ScopeId", "Id", "AllocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_block_projections_ScopeId_PropertyId_InventoryUni~",
                schema: "reservations",
                table: "inventory_block_projections",
                columns: new[] { "ScopeId", "PropertyId", "InventoryUnitId", "Status", "Arrival", "Departure" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_unit_projections_ScopeId_PropertyId_RoomId_IsSell~",
                schema: "reservations",
                table: "inventory_unit_projections",
                columns: new[] { "ScopeId", "PropertyId", "RoomId", "IsSellable" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntil~",
                schema: "reservations",
                table: "outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_requested_inventory_units_ScopeId_Id_ReservationId",
                schema: "reservations",
                table: "requested_inventory_units",
                columns: new[] { "ScopeId", "Id", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ScopeId_AllocationRequestId",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "ScopeId", "AllocationRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ScopeId_PropertyId_Status_Arrival_Departure",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "ScopeId", "PropertyId", "Status", "Arrival", "Departure" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ScopeId_SourceSystem_SourceReference",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "ScopeId", "SourceSystem", "SourceReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "inventory_allocation_unit_projections",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "inventory_block_projections",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "inventory_unit_projections",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "projection_rebuild_checkpoints",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "requested_inventory_units",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "inventory_allocation_projections",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "reservations");
        }
    }
}

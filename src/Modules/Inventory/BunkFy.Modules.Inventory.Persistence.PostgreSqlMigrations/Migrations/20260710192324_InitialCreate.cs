using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "allocations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    Departure = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Rejection = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ReleaseRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocations", x => x.Id);
                    table.UniqueConstraint("AK_allocations_ScopeId_Id", x => new { x.ScopeId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "bed_topology",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    DetailsVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bed_topology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "inventory",
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
                name: "inventory_units",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    BedId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsTopologyActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    DetailsVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    AvailabilityMutationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_units", x => x.Id);
                    table.UniqueConstraint("AK_inventory_units_ScopeId_Id", x => new { x.ScopeId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "inventory",
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
                schema: "inventory",
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
                name: "property_topology",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    DetailsVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    ProjectionOrdinal = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_topology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "room_configurations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesMode = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "room_topology",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BuildingLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FloorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    DetailsVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_topology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "allocation_units",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_units", x => new { x.ScopeId, x.AllocationId, x.Id });
                    table.ForeignKey(
                        name: "FK_allocation_units_allocations_ScopeId_AllocationId",
                        columns: x => new { x.ScopeId, x.AllocationId },
                        principalSchema: "inventory",
                        principalTable: "allocations",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "manual_blocks",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    Departure = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manual_blocks_inventory_units_ScopeId_InventoryUnitId",
                        columns: x => new { x.ScopeId, x.InventoryUnitId },
                        principalSchema: "inventory",
                        principalTable: "inventory_units",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_allocation_units_ScopeId_Id_AllocationId",
                schema: "inventory",
                table: "allocation_units",
                columns: new[] { "ScopeId", "Id", "AllocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_allocations_ScopeId_AllocationRequestId",
                schema: "inventory",
                table: "allocations",
                columns: new[] { "ScopeId", "AllocationRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_allocations_ScopeId_PropertyId_Status_Arrival_Departure",
                schema: "inventory",
                table: "allocations",
                columns: new[] { "ScopeId", "PropertyId", "Status", "Arrival", "Departure" });

            migrationBuilder.CreateIndex(
                name: "IX_allocations_ScopeId_ReservationId",
                schema: "inventory",
                table: "allocations",
                columns: new[] { "ScopeId", "ReservationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bed_topology_ScopeId_PropertyId_RoomId_Label",
                schema: "inventory",
                table: "bed_topology",
                columns: new[] { "ScopeId", "PropertyId", "RoomId", "Label" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "inventory",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_units_ScopeId_PropertyId_RoomId_Kind",
                schema: "inventory",
                table: "inventory_units",
                columns: new[] { "ScopeId", "PropertyId", "RoomId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_blocks_ScopeId_InventoryUnitId",
                schema: "inventory",
                table: "manual_blocks",
                columns: new[] { "ScopeId", "InventoryUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_blocks_ScopeId_PropertyId_InventoryUnitId_Status_Arr~",
                schema: "inventory",
                table: "manual_blocks",
                columns: new[] { "ScopeId", "PropertyId", "InventoryUnitId", "Status", "Arrival", "Departure" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntil~",
                schema: "inventory",
                table: "outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_property_topology_ProjectionOrdinal",
                schema: "inventory",
                table: "property_topology",
                column: "ProjectionOrdinal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_property_topology_ScopeId_Code",
                schema: "inventory",
                table: "property_topology",
                columns: new[] { "ScopeId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_room_configurations_ScopeId_PropertyId",
                schema: "inventory",
                table: "room_configurations",
                columns: new[] { "ScopeId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_room_topology_ScopeId_PropertyId_Name",
                schema: "inventory",
                table: "room_topology",
                columns: new[] { "ScopeId", "PropertyId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocation_units",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "bed_topology",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "manual_blocks",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "projection_rebuild_checkpoints",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "property_topology",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "room_configurations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "room_topology",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "allocations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_units",
                schema: "inventory");
        }
    }
}

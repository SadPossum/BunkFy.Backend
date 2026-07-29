using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffEmploymentGovernanceAndDataHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_data_holds",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    PlacedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_data_holds", x => x.Id);
                    table.UniqueConstraint("AK_staff_data_holds_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_data_holds_lifecycle", "(\"State\" = 1 AND \"Version\" = 1 AND \"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"Version\" = 2 AND \"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"PlacedAtUtc\")");
                    table.CheckConstraint("CK_staff_data_holds_state", "\"State\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_staff_data_holds_staff_members_ScopeId_StaffMemberId",
                        columns: x => new { x.ScopeId, x.StaffMemberId },
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_employment_governance",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GovernanceContractVersion = table.Column<int>(type: "integer", nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    OperatingCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    PolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    DataRegionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TransferProfileId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RetentionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RetentionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    PolicyContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PolicyEffectiveAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PolicyExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PolicyEvaluatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfiguredBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConfiguredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_employment_governance", x => x.Id);
                    table.UniqueConstraint("AK_staff_employment_governance_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_employment_governance_contract", "\"GovernanceContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_employment_governance_policy", "char_length(\"OperatingCountryCode\") = 2 AND \"PolicyVersion\" >= 1 AND \"RetentionPolicyVersion\" >= 1 AND char_length(\"PolicyContentSha256\") = 64 AND \"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND \"PolicyEvaluatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND \"PolicyEvaluatedAtUtc\" < \"PolicyExpiresAtUtc\" AND \"ConfiguredAtUtc\" >= \"PolicyEvaluatedAtUtc\" AND \"ConfiguredAtUtc\" < \"PolicyExpiresAtUtc\"");
                    table.CheckConstraint("CK_staff_employment_governance_versions", "\"SelectedStaffVersion\" >= 1 AND \"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_staff_employment_governance_staff_members_ScopeId_Id",
                        columns: x => new { x.ScopeId, x.Id },
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_employment_governance_change_receipts",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    GovernanceContractVersion = table.Column<int>(type: "integer", nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    PreviousGovernanceVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingGovernanceVersion = table.Column<long>(type: "bigint", nullable: false),
                    PolicyContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AcknowledgementsSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReceiptSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_employment_governance_change_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_employment_governance_change_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_employment_governance_receipts_contract", "\"GovernanceContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_employment_governance_receipts_digests", "char_length(\"PolicyContentSha256\") = 64 AND char_length(\"AcknowledgementsSha256\") = 64 AND char_length(\"RequestSha256\") = 64 AND char_length(\"ReceiptSha256\") = 64");
                    table.CheckConstraint("CK_staff_employment_governance_receipts_versions", "\"SelectedStaffVersion\" >= 1 AND \"PreviousGovernanceVersion\" >= 0 AND \"ResultingGovernanceVersion\" = \"PreviousGovernanceVersion\" + 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_operation_locks",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_operation_locks", x => x.Id);
                    table.UniqueConstraint("AK_staff_operation_locks_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_operation_locks_coordinate", "\"Id\" = \"StaffMemberId\"");
                    table.CheckConstraint("CK_staff_operation_locks_revision", "\"Revision\" >= 1");
                    table.ForeignKey(
                        name: "FK_staff_operation_locks_staff_members_ScopeId_StaffMemberId",
                        columns: x => new { x.ScopeId, x.StaffMemberId },
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_data_hold_receipts",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingHoldVersion = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_data_hold_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_data_hold_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_data_hold_receipts_action", "\"Action\" IN (1, 2)");
                    table.CheckConstraint("CK_staff_data_hold_receipts_versions", "\"SelectedStaffVersion\" >= 1 AND ((\"Action\" = 1 AND \"ResultingHoldVersion\" = 1) OR (\"Action\" = 2 AND \"ResultingHoldVersion\" = 2))");
                    table.ForeignKey(
                        name: "FK_staff_data_hold_receipts_staff_data_holds_ScopeId_HoldId",
                        columns: x => new { x.ScopeId, x.HoldId },
                        principalSchema: "staff",
                        principalTable: "staff_data_holds",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_employment_governance_acknowledgements",
                schema: "staff",
                columns: table => new
                {
                    AcknowledgementId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcknowledgementVersion = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_employment_governance_acknowledgements", x => new { x.ScopeId, x.StaffMemberId, x.AcknowledgementId, x.AcknowledgementVersion });
                    table.ForeignKey(
                        name: "FK_staff_employment_governance_acknowledgements_staff_employme~",
                        columns: x => new { x.ScopeId, x.StaffMemberId },
                        principalSchema: "staff",
                        principalTable: "staff_employment_governance",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_data_hold_receipts_ScopeId_HoldId_Action",
                schema: "staff",
                table: "staff_data_hold_receipts",
                columns: new[] { "ScopeId", "HoldId", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_data_hold_receipts_ScopeId_IdempotencyKey",
                schema: "staff",
                table: "staff_data_hold_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_data_hold_receipts_ScopeId_StaffMemberId_CompletedAtU~",
                schema: "staff",
                table: "staff_data_hold_receipts",
                columns: new[] { "ScopeId", "StaffMemberId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_data_holds_ScopeId_StaffMemberId_State_PlacedAtUtc_Id",
                schema: "staff",
                table: "staff_data_holds",
                columns: new[] { "ScopeId", "StaffMemberId", "State", "PlacedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_employment_governance_acknowledgements_ScopeId_StaffM~",
                schema: "staff",
                table: "staff_employment_governance_acknowledgements",
                columns: new[] { "ScopeId", "StaffMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_employment_governance_change_receipts_ScopeId_Idempot~",
                schema: "staff",
                table: "staff_employment_governance_change_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_employment_governance_change_receipts_ScopeId_StaffMe~",
                schema: "staff",
                table: "staff_employment_governance_change_receipts",
                columns: new[] { "ScopeId", "StaffMemberId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_operation_locks_ScopeId_StaffMemberId",
                schema: "staff",
                table: "staff_operation_locks",
                columns: new[] { "ScopeId", "StaffMemberId" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "staff"."staff_operation_locks"
                    ("Id", "StaffMemberId", "Revision", "ScopeId")
                SELECT
                    member."Id",
                    member."Id",
                    1,
                    member."ScopeId"
                FROM "staff"."staff_members" AS member;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "staff"."staff_employment_governance"
                    ) OR EXISTS (
                        SELECT 1
                        FROM "staff"."staff_employment_governance_change_receipts"
                    ) OR EXISTS (
                        SELECT 1
                        FROM "staff"."staff_data_holds"
                    ) OR EXISTS (
                        SELECT 1
                        FROM "staff"."staff_data_hold_receipts"
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while Staff employment-governance or data-hold records exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "staff_data_hold_receipts",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_employment_governance_acknowledgements",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_employment_governance_change_receipts",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_operation_locks",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_data_holds",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_employment_governance",
                schema: "staff");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRightsRestoreCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restore_checkpoints",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSequence = table.Column<long>(type: "bigint", nullable: false),
                    EntrySha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StorageMacSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    IntegrityKeyVersion = table.Column<int>(type: "integer", nullable: false),
                    CheckpointMacSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeSnapshotSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LastReconciledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restore_checkpoints", x => x.Id);
                    table.CheckConstraint("CK_data_rights_restore_checkpoint_confirmation", "\"IntegrityKeyVersion\" >= 0 AND \"LastReconciledAtUtc\" <> '-infinity'");
                    table.CheckConstraint("CK_data_rights_restore_checkpoint_digests", "char_length(\"EntrySha256\") = 64 AND char_length(\"StorageMacSha256\") = 64 AND char_length(\"CheckpointMacSha256\") = 64 AND char_length(\"ScopeSnapshotSha256\") = 64");
                    table.CheckConstraint("CK_data_rights_restore_checkpoint_sequence", "\"TenantSequence\" >= 0 AND \"Version\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_restore_checkpoints_ScopeId",
                schema: "data-rights",
                table: "restore_checkpoints",
                column: "ScopeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restore_checkpoints",
                schema: "data-rights");
        }
    }
}

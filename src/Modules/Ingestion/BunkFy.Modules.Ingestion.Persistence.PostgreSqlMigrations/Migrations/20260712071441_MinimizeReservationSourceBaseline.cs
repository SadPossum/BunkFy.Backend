using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class MinimizeReservationSourceBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastAppliedNormalizedSnapshot",
                schema: "ingestion",
                table: "reservation_source_links",
                newName: "LastAppliedOperationalBaseline");

            migrationBuilder.Sql(
                """
                DO $migration$
                DECLARE
                    source_link record;
                    snapshot jsonb;
                    operation_kind text;
                    arrival_text text;
                    departure_text text;
                    units jsonb;
                    normalized_units jsonb;
                BEGIN
                    FOR source_link IN
                        SELECT "Id", "LastAppliedOperationalBaseline" AS baseline
                        FROM ingestion.reservation_source_links
                        WHERE "LastAppliedOperationalBaseline" IS NOT NULL
                    LOOP
                        BEGIN
                            snapshot := source_link.baseline::jsonb;
                            operation_kind := lower(COALESCE(snapshot ->> 'Kind', snapshot ->> 'kind', ''));
                            arrival_text := COALESCE(snapshot ->> 'Arrival', snapshot ->> 'arrival');
                            departure_text := COALESCE(snapshot ->> 'Departure', snapshot ->> 'departure');
                            units := COALESCE(snapshot -> 'InventoryUnitIds', snapshot -> 'inventoryUnitIds');

                            IF operation_kind NOT IN ('1', 'upsert') OR
                               arrival_text IS NULL OR departure_text IS NULL OR
                               arrival_text !~ '^\d{4}-\d{2}-\d{2}$' OR
                               departure_text !~ '^\d{4}-\d{2}-\d{2}$' OR
                               departure_text::date <= arrival_text::date OR
                               jsonb_typeof(units) IS DISTINCT FROM 'array' OR
                               jsonb_array_length(units) = 0
                            THEN
                                UPDATE ingestion.reservation_source_links
                                SET "LastAppliedOperationalBaseline" = NULL
                                WHERE "Id" = source_link."Id";
                                CONTINUE;
                            END IF;

                            SELECT jsonb_agg(to_jsonb(unit) ORDER BY unit)
                            INTO normalized_units
                            FROM (
                                SELECT DISTINCT lower(value) AS unit
                                FROM jsonb_array_elements_text(units)
                                WHERE value ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                                  AND value <> '00000000-0000-0000-0000-000000000000'
                            ) canonical_units;

                            IF normalized_units IS NULL OR
                               jsonb_array_length(normalized_units) <> jsonb_array_length(units)
                            THEN
                                UPDATE ingestion.reservation_source_links
                                SET "LastAppliedOperationalBaseline" = NULL
                                WHERE "Id" = source_link."Id";
                                CONTINUE;
                            END IF;

                            UPDATE ingestion.reservation_source_links
                            SET "LastAppliedOperationalBaseline" = jsonb_build_object(
                                'schemaVersion', 1,
                                'arrival', to_char(arrival_text::date, 'YYYY-MM-DD'),
                                'departure', to_char(departure_text::date, 'YYYY-MM-DD'),
                                'inventoryUnitIds', normalized_units)::text
                            WHERE "Id" = source_link."Id";
                        EXCEPTION WHEN OTHERS THEN
                            UPDATE ingestion.reservation_source_links
                            SET "LastAppliedOperationalBaseline" = NULL
                            WHERE "Id" = source_link."Id";
                        END;
                    END LOOP;
                END
                $migration$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastAppliedOperationalBaseline",
                schema: "ingestion",
                table: "reservation_source_links",
                newName: "LastAppliedNormalizedSnapshot");
        }
    }
}

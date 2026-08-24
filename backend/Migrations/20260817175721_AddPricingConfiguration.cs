using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FuelPriceRates",
                columns: table => new
                {
                    FuelPriceRateId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FuelType = table.Column<string>(type: "text", nullable: false),
                    PricePerLitre = table.Column<decimal>(type: "numeric", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelPriceRates", x => x.FuelPriceRateId);
                    table.CheckConstraint("ck_fpr_price_positive", "\"PricePerLitre\" > 0");
                    table.ForeignKey(
                        name: "FK_FuelPriceRates_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuelPriceRates_Users_SetByUserId",
                        column: x => x.SetByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleClassEfficiencies",
                columns: table => new
                {
                    VehicleClassEfficiencyId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ClassLabel = table.Column<string>(type: "text", nullable: false),
                    MinPayloadKg = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxPayloadKg = table.Column<decimal>(type: "numeric", nullable: true),
                    FuelConsumptionLPer100Km = table.Column<decimal>(type: "numeric", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleClassEfficiencies", x => x.VehicleClassEfficiencyId);
                    table.CheckConstraint("ck_vce_consumption_positive", "\"FuelConsumptionLPer100Km\" > 0");
                    table.CheckConstraint("ck_vce_payload_bounds", "\"MinPayloadKg\" >= 0 AND (\"MaxPayloadKg\" IS NULL OR \"MaxPayloadKg\" > \"MinPayloadKg\")");
                    table.ForeignKey(
                        name: "FK_VehicleClassEfficiencies_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleClassEfficiencies_Users_SetByUserId",
                        column: x => x.SetByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FuelPriceRates_DeletedByUserId",
                table: "FuelPriceRates",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelPriceRates_FuelType_EffectiveFrom",
                table: "FuelPriceRates",
                columns: new[] { "FuelType", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_FuelPriceRates_SetByUserId",
                table: "FuelPriceRates",
                column: "SetByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleClassEfficiencies_ClassLabel_EffectiveFrom",
                table: "VehicleClassEfficiencies",
                columns: new[] { "ClassLabel", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleClassEfficiencies_DeletedByUserId",
                table: "VehicleClassEfficiencies",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleClassEfficiencies_SetByUserId",
                table: "VehicleClassEfficiencies",
                column: "SetByUserId");

            // fn_deny_delete: unlike fn_deny_mutation (AddDatabaseConstraintsAndTriggers), which
            // blocks UPDATE and DELETE together for pure append-only history tables, these two
            // pricing-config tables ARE legitimately updated — the application-layer soft delete
            // (DeletedAt/DeletedByUserId) is itself an UPDATE. This trigger blocks only a literal
            // DELETE, as a database-level backstop; it never infers who issued the delete, since the
            // service layer is the sole source of the acting Admin's id.
            migrationBuilder.Sql("""
                CREATE FUNCTION fn_deny_delete() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION '% rows cannot be hard-deleted; soft-delete via DeletedAt/DeletedByUserId instead', TG_TABLE_NAME;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_deny_delete_fuelpricerates
                BEFORE DELETE ON "FuelPriceRates"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_delete();

                CREATE TRIGGER trg_deny_delete_vehicleclassefficiencies
                BEFORE DELETE ON "VehicleClassEfficiencies"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_delete();
                """);

            // fn_set_updated_at already exists (AddDatabaseConstraintsAndTriggers) — reused here,
            // not redefined. Backs UpdatedAt's ValueGeneratedOnAddOrUpdate() mapping in
            // FuelPriceRateConfiguration/VehicleClassEfficiencyConfiguration.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_set_updated_at_fuelpricerates
                BEFORE UPDATE ON "FuelPriceRates"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_vehicleclassefficiencies
                BEFORE UPDATE ON "VehicleClassEfficiencies"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_set_updated_at_fuelpricerates ON "FuelPriceRates";
                DROP TRIGGER IF EXISTS trg_set_updated_at_vehicleclassefficiencies ON "VehicleClassEfficiencies";
                DROP TRIGGER IF EXISTS trg_deny_delete_fuelpricerates ON "FuelPriceRates";
                DROP TRIGGER IF EXISTS trg_deny_delete_vehicleclassefficiencies ON "VehicleClassEfficiencies";
                DROP FUNCTION IF EXISTS fn_deny_delete();
                """);

            migrationBuilder.DropTable(
                name: "FuelPriceRates");

            migrationBuilder.DropTable(
                name: "VehicleClassEfficiencies");
        }
    }
}

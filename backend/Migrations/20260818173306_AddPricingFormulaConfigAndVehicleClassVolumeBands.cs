using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingFormulaConfigAndVehicleClassVolumeBands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaxVolumeM3",
                table: "VehicleClassEfficiencies",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinVolumeM3",
                table: "VehicleClassEfficiencies",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PricingFormulaConfigs",
                columns: table => new
                {
                    PricingFormulaConfigId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BaseFare = table.Column<decimal>(type: "numeric", nullable: false),
                    RatePerKg = table.Column<decimal>(type: "numeric", nullable: false),
                    DriverMaintenanceMarginAllowancePerKm = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_PricingFormulaConfigs", x => x.PricingFormulaConfigId);
                    table.CheckConstraint("ck_pfc_base_fare_bounds", "\"BaseFare\" >= 0");
                    table.CheckConstraint("ck_pfc_maintenance_allowance_bounds", "\"DriverMaintenanceMarginAllowancePerKm\" >= 0");
                    table.CheckConstraint("ck_pfc_rate_per_kg_bounds", "\"RatePerKg\" >= 0");
                    table.ForeignKey(
                        name: "FK_PricingFormulaConfigs_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingFormulaConfigs_Users_SetByUserId",
                        column: x => x.SetByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_vce_volume_bounds",
                table: "VehicleClassEfficiencies",
                sql: "\"MinVolumeM3\" >= 0 AND (\"MaxVolumeM3\" IS NULL OR \"MaxVolumeM3\" > \"MinVolumeM3\")");

            migrationBuilder.CreateIndex(
                name: "IX_PricingFormulaConfigs_DeletedByUserId",
                table: "PricingFormulaConfigs",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingFormulaConfigs_EffectiveFrom",
                table: "PricingFormulaConfigs",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_PricingFormulaConfigs_SetByUserId",
                table: "PricingFormulaConfigs",
                column: "SetByUserId");

            // fn_deny_delete() and fn_set_updated_at() already exist (AddPricingConfiguration /
            // AddDatabaseConstraintsAndTriggers) — reused here, not redefined. Same backstop pattern as
            // FuelPriceRates/VehicleClassEfficiencies: the service-layer soft delete is the only
            // legitimate write to DeletedAt/DeletedByUserId, and this trigger blocks a literal DELETE
            // as a database-level backstop, never inferring who issued it.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_deny_delete_pricingformulaconfigs
                BEFORE DELETE ON "PricingFormulaConfigs"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_delete();

                CREATE TRIGGER trg_set_updated_at_pricingformulaconfigs
                BEFORE UPDATE ON "PricingFormulaConfigs"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_set_updated_at_pricingformulaconfigs ON "PricingFormulaConfigs";
                DROP TRIGGER IF EXISTS trg_deny_delete_pricingformulaconfigs ON "PricingFormulaConfigs";
                """);

            migrationBuilder.DropTable(
                name: "PricingFormulaConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vce_volume_bounds",
                table: "VehicleClassEfficiencies");

            migrationBuilder.DropColumn(
                name: "MaxVolumeM3",
                table: "VehicleClassEfficiencies");

            migrationBuilder.DropColumn(
                name: "MinVolumeM3",
                table: "VehicleClassEfficiencies");
        }
    }
}

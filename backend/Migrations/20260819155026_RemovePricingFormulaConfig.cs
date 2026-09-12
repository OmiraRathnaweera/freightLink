using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemovePricingFormulaConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Explicit for clarity, though DROP TABLE would cascade-drop these triggers on its own
            // (mirrors the DROP TRIGGER-then-DROP TABLE order AddPricingFormulaConfigAndVehicleClassVolumeBands's
            // own Down() used).
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_set_updated_at_pricingformulaconfigs ON "PricingFormulaConfigs";
                DROP TRIGGER IF EXISTS trg_deny_delete_pricingformulaconfigs ON "PricingFormulaConfigs";
                """);

            migrationBuilder.DropTable(
                name: "PricingFormulaConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingFormulaConfigs",
                columns: table => new
                {
                    PricingFormulaConfigId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SetByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseFare = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DriverMaintenanceMarginAllowancePerKm = table.Column<decimal>(type: "numeric", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RatePerKg = table.Column<decimal>(type: "numeric", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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

            // fn_deny_delete() and fn_set_updated_at() are shared, pre-existing functions (see
            // AddPricingFormulaConfigAndVehicleClassVolumeBands) — reused here, not redefined.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_deny_delete_pricingformulaconfigs
                BEFORE DELETE ON "PricingFormulaConfigs"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_delete();

                CREATE TRIGGER trg_set_updated_at_pricingformulaconfigs
                BEFORE UPDATE ON "PricingFormulaConfigs"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();
                """);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class BoundPricingFormulaConfigMarginPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pfc_margin_percent_bounds",
                table: "PricingFormulaConfigs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pfc_margin_percent_bounds",
                table: "PricingFormulaConfigs",
                sql: "\"MarginPercent\" >= 0 AND \"MarginPercent\" <= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pfc_margin_percent_bounds",
                table: "PricingFormulaConfigs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pfc_margin_percent_bounds",
                table: "PricingFormulaConfigs",
                sql: "\"MarginPercent\" >= 0");
        }
    }
}

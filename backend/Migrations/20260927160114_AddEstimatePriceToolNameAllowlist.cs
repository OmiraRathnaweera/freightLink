using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimatePriceToolNameAllowlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_toolcall_allowlist",
                table: "ToolCalls");

            migrationBuilder.AddCheckConstraint(
                name: "ck_toolcall_allowlist",
                table: "ToolCalls",
                sql: "\"ToolName\" IN ('get_route_and_eta', 'estimate_price')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_toolcall_allowlist",
                table: "ToolCalls");

            migrationBuilder.AddCheckConstraint(
                name: "ck_toolcall_allowlist",
                table: "ToolCalls",
                sql: "\"ToolName\" IN ('get_route_and_eta')");
        }
    }
}

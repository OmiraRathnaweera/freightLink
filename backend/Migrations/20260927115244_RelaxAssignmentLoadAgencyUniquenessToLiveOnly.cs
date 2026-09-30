using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class RelaxAssignmentLoadAgencyUniquenessToLiveOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_assignment_load_agency",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "ux_assignment_load_agency",
                table: "Assignments",
                columns: new[] { "LoadId", "AgencyId" },
                unique: true,
                filter: "\"Status\" IN ('Proposed','Accepted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_assignment_load_agency",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "ux_assignment_load_agency",
                table: "Assignments",
                columns: new[] { "LoadId", "AgencyId" },
                unique: true);
        }
    }
}

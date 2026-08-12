using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameLoadFilesUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "uq_file_uploadedfileid",
                table: "LoadFiles",
                newName: "uq_loadfile_uploadedfileid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "uq_loadfile_uploadedfileid",
                table: "LoadFiles",
                newName: "uq_file_uploadedfileid");
        }
    }
}

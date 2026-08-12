using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameFilesTableToLoadFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Loads_LoadId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_UploadedFiles_UploadedFileId",
                table: "Files");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Files",
                table: "Files");

            migrationBuilder.RenameTable(
                name: "Files",
                newName: "LoadFiles");

            migrationBuilder.RenameIndex(
                name: "IX_Files_LoadId",
                table: "LoadFiles",
                newName: "IX_LoadFiles_LoadId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoadFiles",
                table: "LoadFiles",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_LoadFiles_Loads_LoadId",
                table: "LoadFiles",
                column: "LoadId",
                principalTable: "Loads",
                principalColumn: "LoadId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoadFiles_UploadedFiles_UploadedFileId",
                table: "LoadFiles",
                column: "UploadedFileId",
                principalTable: "UploadedFiles",
                principalColumn: "FileId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoadFiles_Loads_LoadId",
                table: "LoadFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_LoadFiles_UploadedFiles_UploadedFileId",
                table: "LoadFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoadFiles",
                table: "LoadFiles");

            migrationBuilder.RenameTable(
                name: "LoadFiles",
                newName: "Files");

            migrationBuilder.RenameIndex(
                name: "IX_LoadFiles_LoadId",
                table: "Files",
                newName: "IX_Files_LoadId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Files",
                table: "Files",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Loads_LoadId",
                table: "Files",
                column: "LoadId",
                principalTable: "Loads",
                principalColumn: "LoadId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_UploadedFiles_UploadedFileId",
                table: "Files",
                column: "UploadedFileId",
                principalTable: "UploadedFiles",
                principalColumn: "FileId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

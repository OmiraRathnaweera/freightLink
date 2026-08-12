using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentTypeAndRestructureLoadFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Users_UploadedByUserId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_UploadedByUserId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "uq_file_storagekey",
                table: "Files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_file_size",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "Files");

            migrationBuilder.RenameColumn(
                name: "UploadedByUserId",
                table: "Files",
                newName: "UploadedFileId");

            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "Files",
                newName: "AttachedAt");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "UploadedFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "uq_file_uploadedfileid",
                table: "Files",
                column: "UploadedFileId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_UploadedFiles_UploadedFileId",
                table: "Files",
                column: "UploadedFileId",
                principalTable: "UploadedFiles",
                principalColumn: "FileId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_UploadedFiles_UploadedFileId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "uq_file_uploadedfileid",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "UploadedFiles");

            migrationBuilder.RenameColumn(
                name: "UploadedFileId",
                table: "Files",
                newName: "UploadedByUserId");

            migrationBuilder.RenameColumn(
                name: "AttachedAt",
                table: "Files",
                newName: "UploadedAt");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Files",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "Files",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "Files",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "Files",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Files_UploadedByUserId",
                table: "Files",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "uq_file_storagekey",
                table: "Files",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_file_size",
                table: "Files",
                sql: "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 10485760");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Users_UploadedByUserId",
                table: "Files",
                column: "UploadedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

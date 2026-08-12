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
            // Added first so the backfill INSERT below can populate it from each existing Files
            // row's ContentType before that column is dropped from Files further down.
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "UploadedFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Dropped before the backfill UPDATE below repurposes Files.UploadedByUserId to hold an
            // UploadedFiles.FileId instead of a Users.UserId — otherwise that UPDATE would itself
            // violate this FK.
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Users_UploadedByUserId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_UploadedByUserId",
                table: "Files");

            // Backfill: create one UploadedFiles row per pre-existing Files row, carrying over its
            // storage metadata before FileName/StorageKey/SizeBytes/ContentType are dropped from
            // Files below. Every upload issued via CloudinaryFileStorageService uses Cloudinary's
            // "raw" resource type (see its class remarks), so that's used as the backfilled
            // ResourceType; Format is left null (the old schema never recorded it) and SecureUrl is
            // left blank (reconstructing it needs the Cloudinary cloud name, which isn't stored in
            // the database) — both are unavoidable gaps for rows that predate this table.
            migrationBuilder.Sql(
                """
                INSERT INTO "UploadedFiles" ("FileId", "UploadedByUserId", "PublicId", "SecureUrl", "Format", "Bytes", "ResourceType", "ContentType", "OriginalFileName", "UploadedAt")
                SELECT gen_random_uuid(), "UploadedByUserId", "StorageKey", '', NULL, "SizeBytes", 'raw', "ContentType", "FileName", "UploadedAt"
                FROM "Files";
                """);

            // Point each Files row at its newly-created UploadedFiles row via the still-named
            // UploadedByUserId column (renamed to UploadedFileId below) — correlated on StorageKey,
            // which was unique (see uq_file_storagekey, dropped next) and was just copied verbatim
            // into UploadedFiles.PublicId above, so the match is unambiguous.
            migrationBuilder.Sql(
                """
                UPDATE "Files" AS f
                SET "UploadedByUserId" = uf."FileId"
                FROM "UploadedFiles" AS uf
                WHERE uf."PublicId" = f."StorageKey";
                """);

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

using System;
using FreightLink.Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260927090000_AddAccountRecoveryAndEmailVerification")]
    public partial class AddAccountRecoveryAndEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerifiedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerificationTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationTokenHash",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordResetTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Existing accounts pre-date the verification feature. Marking them verified avoids
            // unexpectedly locking legitimate users out on deployment; only newly registered
            // accounts are required to complete the one-time verification flow.
            migrationBuilder.Sql("UPDATE \"Users\" SET \"EmailVerifiedAt\" = now() WHERE \"EmailVerifiedAt\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_user_email_verification_token",
                table: "Users",
                columns: new[] { "EmailVerificationTokenHash", "EmailVerificationTokenExpiresAt" },
                filter: "\"EmailVerificationTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_password_reset_token",
                table: "Users",
                columns: new[] { "PasswordResetTokenHash", "PasswordResetTokenExpiresAt" },
                filter: "\"PasswordResetTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_user_email_verification_token", table: "Users");
            migrationBuilder.DropIndex(name: "ix_user_password_reset_token", table: "Users");

            migrationBuilder.DropColumn(name: "EmailVerifiedAt", table: "Users");
            migrationBuilder.DropColumn(name: "EmailVerificationTokenExpiresAt", table: "Users");
            migrationBuilder.DropColumn(name: "EmailVerificationTokenHash", table: "Users");
            migrationBuilder.DropColumn(name: "PasswordResetTokenExpiresAt", table: "Users");
            migrationBuilder.DropColumn(name: "PasswordResetTokenHash", table: "Users");
        }
    }
}

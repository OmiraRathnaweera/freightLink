using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeInvoiceIssuedAtNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_due",
                table: "Invoices");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_TripId",
                table: "Invoices",
                newName: "uq_invoice_trip_id");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "IssuedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_due",
                table: "Invoices",
                sql: "\"DueDate\" IS NULL OR \"IssuedAt\" IS NULL OR \"DueDate\" >= (\"IssuedAt\" AT TIME ZONE 'UTC')::date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_due",
                table: "Invoices");

            migrationBuilder.RenameIndex(
                name: "uq_invoice_trip_id",
                table: "Invoices",
                newName: "IX_Invoices_TripId");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "IssuedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_due",
                table: "Invoices",
                sql: "\"DueDate\" IS NULL OR \"DueDate\" >= (\"IssuedAt\" AT TIME ZONE 'UTC')::date");
        }
    }
}

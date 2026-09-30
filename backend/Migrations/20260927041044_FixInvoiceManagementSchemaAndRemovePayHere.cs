using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <summary>
    /// Bundles two unrelated fixes because both were only discoverable by diffing the current model
    /// against the DB: (1) two previously-committed migrations (AddManualInvoiceManagementAndLineItems,
    /// AddInvoicePaidAtAndPaymentReference) were missing the [Migration] attribute EF needs to run
    /// them at all, so their invoice line-items/subtotal/tax/paid-at columns had never actually been
    /// created on any real database — this migration replaces those two dead files and creates that
    /// schema for real; (2) removes the PayHere payment-gateway tables (Payments,
    /// PaymentWebhookEvents) in favor of a Shipper-uploaded payment-proof file reference on Invoice.
    /// </summary>
    public partial class FixInvoiceManagementSchemaAndRemovePayHere : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Trips_TripId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            // fn_protect_webhook_receipt (see FixPaymentWebhookEventAppendOnlyTrigger) is a
            // standalone function, not owned by PaymentWebhookEvents — dropping the table above
            // cascade-drops its trigger but not the function itself. CASCADE here is a no-op safety
            // net (the trigger is already gone) rather than a real dependency drop.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_protect_webhook_receipt() CASCADE;");

            migrationBuilder.DropIndex(
                name: "uq_invoice_trip_id",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_amount",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "ux_dispute_open",
                table: "Disputes");

            migrationBuilder.AlterColumn<Guid>(
                name: "TripId",
                table: "Invoices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountTotal",
                table: "Invoices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Invoices",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentProofFileId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentProofUploadedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentProofUploadedByUserId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "Invoices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientRole",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Invoices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxTotal",
                table: "Invoices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Invoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByUserId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    InvoiceLineItemId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItems", x => x.InvoiceLineItemId);
                    table.CheckConstraint("ck_invoice_line_item_amount", "\"Amount\" >= 0");
                    table.CheckConstraint("ck_invoice_line_item_qty", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_invoice_line_item_tax_rate", "\"TaxRate\" >= 0");
                    table.CheckConstraint("ck_invoice_line_item_unit_price", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedByUserId",
                table: "Invoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PaymentProofFileId",
                table: "Invoices",
                column: "PaymentProofFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PaymentProofUploadedByUserId",
                table: "Invoices",
                column: "PaymentProofUploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_RecipientId",
                table: "Invoices",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UpdatedByUserId",
                table: "Invoices",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_VoidedByUserId",
                table: "Invoices",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "uq_invoice_trip_id",
                table: "Invoices",
                column: "TripId",
                unique: true,
                filter: "\"TripId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_amount",
                table: "Invoices",
                sql: "\"Amount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "ux_dispute_open",
                table: "Disputes",
                columns: new[] { "TripId", "Category" },
                unique: true,
                filter: "\"Status\" IN ('Open','UnderReview','Raised')");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_InvoiceId",
                table: "InvoiceLineItems",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Trips_TripId",
                table: "Invoices",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_UploadedFiles_PaymentProofFileId",
                table: "Invoices",
                column: "PaymentProofFileId",
                principalTable: "UploadedFiles",
                principalColumn: "FileId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_CreatedByUserId",
                table: "Invoices",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_PaymentProofUploadedByUserId",
                table: "Invoices",
                column: "PaymentProofUploadedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_RecipientId",
                table: "Invoices",
                column: "RecipientId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_UpdatedByUserId",
                table: "Invoices",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_VoidedByUserId",
                table: "Invoices",
                column: "VoidedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Trips_TripId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_UploadedFiles_PaymentProofFileId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_CreatedByUserId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_PaymentProofUploadedByUserId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_RecipientId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_UpdatedByUserId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_VoidedByUserId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "InvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CreatedByUserId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PaymentProofFileId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PaymentProofUploadedByUserId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_RecipientId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_UpdatedByUserId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_VoidedByUserId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "uq_invoice_trip_id",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_amount",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "ux_dispute_open",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountTotal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaymentProofFileId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaymentProofUploadedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaymentProofUploadedByUserId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RecipientId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RecipientRole",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TaxTotal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "Invoices");

            migrationBuilder.AlterColumn<Guid>(
                name: "TripId",
                table: "Invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    AttemptNo = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    GatewayRef = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.CheckConstraint("ck_payment_amount", "\"Amount\" > 0");
                    table.CheckConstraint("ck_payment_attempt", "\"AttemptNo\" >= 1");
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    PaymentWebhookEventId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    GatewayRef = table.Column<string>(type: "text", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "text", nullable: false),
                    RawPayloadHash = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.PaymentWebhookEventId);
                    table.CheckConstraint("ck_pwe_error", "\"ProcessingStatus\" <> 'Error' OR \"ErrorMessage\" IS NOT NULL");
                });

            // Recreates the function dropped in Up() for symmetry. Does NOT reattach
            // trg_protect_webhook_receipt to the recreated table above — reversing a feature
            // removal this far is not expected to happen in practice.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_protect_webhook_receipt() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'PaymentWebhookEvents rows cannot be deleted';
                    END IF;

                    IF NEW."PaymentWebhookEventId" IS DISTINCT FROM OLD."PaymentWebhookEventId"
                        OR NEW."GatewayRef" IS DISTINCT FROM OLD."GatewayRef"
                        OR NEW."RawPayloadHash" IS DISTINCT FROM OLD."RawPayloadHash"
                        OR NEW."SignatureValid" IS DISTINCT FROM OLD."SignatureValid"
                        OR NEW."ReceivedAt" IS DISTINCT FROM OLD."ReceivedAt"
                    THEN
                        RAISE EXCEPTION 'PaymentWebhookEvents raw receipt fields are immutable; only ProcessingStatus/ErrorMessage may change';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.CreateIndex(
                name: "uq_invoice_trip_id",
                table: "Invoices",
                column: "TripId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_amount",
                table: "Invoices",
                sql: "\"Amount\" > 0");

            migrationBuilder.CreateIndex(
                name: "ux_dispute_open",
                table: "Disputes",
                columns: new[] { "TripId", "Category" },
                unique: true,
                filter: "\"Status\" IN ('Open','UnderReview')");

            migrationBuilder.CreateIndex(
                name: "uq_payment_attempt",
                table: "Payments",
                columns: new[] { "InvoiceId", "AttemptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payment_gatewayref",
                table: "Payments",
                column: "GatewayRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_payment_success",
                table: "Payments",
                column: "InvoiceId",
                unique: true,
                filter: "\"Status\" = 'Success'");

            migrationBuilder.CreateIndex(
                name: "ix_pwe_payloadhash",
                table: "PaymentWebhookEvents",
                column: "RawPayloadHash");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Trips_TripId",
                table: "Invoices",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

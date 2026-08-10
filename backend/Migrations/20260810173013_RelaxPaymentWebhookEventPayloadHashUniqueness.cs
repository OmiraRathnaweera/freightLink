using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class RelaxPaymentWebhookEventPayloadHashUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_pwe_payloadhash",
                table: "PaymentWebhookEvents");

            migrationBuilder.CreateIndex(
                name: "ix_pwe_payloadhash",
                table: "PaymentWebhookEvents",
                column: "RawPayloadHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pwe_payloadhash",
                table: "PaymentWebhookEvents");

            migrationBuilder.CreateIndex(
                name: "uq_pwe_payloadhash",
                table: "PaymentWebhookEvents",
                column: "RawPayloadHash",
                unique: true);
        }
    }
}

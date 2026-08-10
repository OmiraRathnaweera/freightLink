using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixPaymentWebhookEventAppendOnlyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PaymentWebhookEvents is not a true append-only event log: ProcessingStatus/
            // ErrorMessage are designed to advance after insert (Received -> SignatureRejected/
            // Duplicate/Applied/Error). The blanket fn_deny_mutation trigger from the previous
            // migration incorrectly blocked that. Replace it with a dedicated trigger that keeps
            // the raw-receipt identity columns and DELETE immutable, while allowing only
            // ProcessingStatus/ErrorMessage to change.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_deny_mutation_paymentwebhookevents ON "PaymentWebhookEvents";

                CREATE FUNCTION fn_protect_webhook_receipt() RETURNS trigger AS $$
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

                CREATE TRIGGER trg_protect_webhook_receipt
                BEFORE UPDATE OR DELETE ON "PaymentWebhookEvents"
                FOR EACH ROW
                EXECUTE FUNCTION fn_protect_webhook_receipt();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_protect_webhook_receipt ON "PaymentWebhookEvents";
                DROP FUNCTION IF EXISTS fn_protect_webhook_receipt();

                CREATE TRIGGER trg_deny_mutation_paymentwebhookevents
                BEFORE UPDATE OR DELETE ON "PaymentWebhookEvents"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();
                """);
        }
    }
}

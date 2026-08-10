using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixNotificationEmailDeliveryCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_email_delivery",
                table: "Notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_email_delivery",
                table: "Notifications",
                sql: "\"Channel\" <> 'Email' OR \"DeliveryStatus\" <> 'Sent' OR (\"SentAt\" IS NOT NULL AND \"ProviderMessageId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_email_delivery",
                table: "Notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_email_delivery",
                table: "Notifications",
                sql: "\"Channel\" <> 'Email' OR \"DeliveryStatus\" IS NOT NULL");
        }
    }
}

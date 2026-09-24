using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowTripCascadeDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripEvents_Trips_TripId",
                table: "TripEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_TripEvidences_Trips_TripId",
                table: "TripEvidences");

            migrationBuilder.AddForeignKey(
                name: "FK_TripEvents_Trips_TripId",
                table: "TripEvents",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TripEvidences_Trips_TripId",
                table: "TripEvidences",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_deny_mutation_tripevents ON "TripEvents";
                DROP TRIGGER IF EXISTS trg_deny_mutation_tripevidences ON "TripEvidences";

                CREATE TRIGGER trg_deny_mutation_tripevents
                BEFORE UPDATE ON "TripEvents"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();

                CREATE TRIGGER trg_deny_mutation_tripevidences
                BEFORE UPDATE ON "TripEvidences"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_deny_mutation_tripevents ON "TripEvents";
                DROP TRIGGER IF EXISTS trg_deny_mutation_tripevidences ON "TripEvidences";

                CREATE TRIGGER trg_deny_mutation_tripevents
                BEFORE UPDATE OR DELETE ON "TripEvents"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();

                CREATE TRIGGER trg_deny_mutation_tripevidences
                BEFORE UPDATE OR DELETE ON "TripEvidences"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_TripEvents_Trips_TripId",
                table: "TripEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_TripEvidences_Trips_TripId",
                table: "TripEvidences");

            migrationBuilder.AddForeignKey(
                name: "FK_TripEvents_Trips_TripId",
                table: "TripEvents",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TripEvidences_Trips_TripId",
                table: "TripEvidences",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

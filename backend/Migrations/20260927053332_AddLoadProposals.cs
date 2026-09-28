using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoadProposals",
                columns: table => new
                {
                    LoadProposalId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    LoadId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ResponseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadProposals", x => x.LoadProposalId);
                    table.CheckConstraint("ck_loadproposal_price", "\"ProposedPrice\" > 0");
                    table.ForeignKey(
                        name: "FK_LoadProposals_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "AgencyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoadProposals_Loads_LoadId",
                        column: x => x.LoadId,
                        principalTable: "Loads",
                        principalColumn: "LoadId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoadProposals_Users_ProposedByUserId",
                        column: x => x.ProposedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoadProposals_AgencyId",
                table: "LoadProposals",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_LoadProposals_ProposedByUserId",
                table: "LoadProposals",
                column: "ProposedByUserId");

            migrationBuilder.CreateIndex(
                name: "ux_loadproposal_live_per_load_agency",
                table: "LoadProposals",
                columns: new[] { "LoadId", "AgencyId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_set_updated_at_loadproposals
                BEFORE UPDATE ON "LoadProposals"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_set_updated_at_loadproposals ON "LoadProposals";
                """);

            migrationBuilder.DropTable(
                name: "LoadProposals");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class RelaxAgentWorkflowRunAttemptMax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_awr_attempt",
                table: "AgentWorkflowRuns");

            migrationBuilder.AddCheckConstraint(
                name: "ck_awr_attempt",
                table: "AgentWorkflowRuns",
                sql: "\"AttemptNo\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_awr_attempt",
                table: "AgentWorkflowRuns");

            migrationBuilder.AddCheckConstraint(
                name: "ck_awr_attempt",
                table: "AgentWorkflowRuns",
                sql: "\"AttemptNo\" BETWEEN 1 AND 3");
        }
    }
}

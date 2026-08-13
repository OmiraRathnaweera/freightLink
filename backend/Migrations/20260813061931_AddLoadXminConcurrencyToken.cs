using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No DDL: "xmin" is a Postgres system column that already exists on every table —
            // ADD COLUMN "xmin" is rejected outright by Postgres ("column name conflicts with a
            // system column name", confirmed directly against a live instance). EF's migration
            // scaffolder doesn't know that and generates an AddColumn/DropColumn pair by default;
            // this migration exists only so the model snapshot picks up the new shadow property
            // (LoadConfiguration.cs, UseXminAsConcurrencyToken()) for future `migrations add`
            // diffs — there is nothing to actually apply against the database. Mirrors
            // 20260811061513_AddRefreshTokenXminConcurrencyToken.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

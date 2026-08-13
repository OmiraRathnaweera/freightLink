using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            // GIN trigram indexes over lower(column) — not the plain column — because
            // LoadService.GetListAsync's Search filter compiles to
            // `WHERE lower("Column") LIKE '%term%'` (see LoadService.cs), and Postgres can only use
            // an index whose indexed expression textually matches the query's expression. EF's
            // fluent index API has no way to express an index over lower(column) rather than a
            // mapped property, so these are raw SQL (see LoadConfiguration for the pointer back here).
            migrationBuilder.Sql(
                """CREATE INDEX ix_load_cargodescription_search_trgm ON "Loads" USING gin (lower("CargoDescription") gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX ix_load_referencecode_search_trgm ON "Loads" USING gin (lower("ReferenceCode") gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX ix_load_pickupaddress_search_trgm ON "Loads" USING gin (lower("PickupAddress") gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX ix_load_dropoffaddress_search_trgm ON "Loads" USING gin (lower("DropoffAddress") gin_trgm_ops);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS ix_load_dropoffaddress_search_trgm;""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS ix_load_pickupaddress_search_trgm;""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS ix_load_referencecode_search_trgm;""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS ix_load_cargodescription_search_trgm;""");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}

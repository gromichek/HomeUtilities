using HomeUtilities.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <summary>
    /// Local bookkeeping of the sync (T-031): the outbox of changed rows and the sync state. Both tables are deliberately not
    /// part of the EF model — they are never exported, imported or synced.
    /// </summary>
    [DbContext(typeof(HomeUtilitiesDbContext))]
    [Migration("20261102120000_AddSyncTables")]
    public partial class AddSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE TABLE IF NOT EXISTS \"SyncOutbox\" (\"TableName\" TEXT NOT NULL, \"RowId\" TEXT NOT NULL, \"Stamp\" INTEGER NOT NULL, PRIMARY KEY (\"TableName\", \"RowId\"));");
            migrationBuilder.Sql(
                "CREATE TABLE IF NOT EXISTS \"SyncState\" (\"Key\" TEXT NOT NULL PRIMARY KEY, \"Value\" TEXT NOT NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"SyncOutbox\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"SyncState\";");
        }
    }
}

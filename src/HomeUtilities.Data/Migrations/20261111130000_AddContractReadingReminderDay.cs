using HomeUtilities.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <summary>The day of the reading window a contract's reading reminder is shown on (T-047); null = first day.</summary>
    [DbContext(typeof(HomeUtilitiesDbContext))]
    [Migration("20261111130000_AddContractReadingReminderDay")]
    public partial class AddContractReadingReminderDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReadingReminderDay",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadingReminderDay",
                table: "Contracts");
        }
    }
}

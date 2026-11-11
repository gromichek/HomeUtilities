using HomeUtilities.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <summary>The monthly carry-over from the previous period on an equalized plan (T-046); null for existing plans.</summary>
    [DbContext(typeof(HomeUtilitiesDbContext))]
    [Migration("20261111120000_AddEqualizedCarryOver")]
    public partial class AddEqualizedCarryOver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CarryOverMonthly",
                table: "EqualizedPlans",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarryOverMonthly",
                table: "EqualizedPlans");
        }
    }
}

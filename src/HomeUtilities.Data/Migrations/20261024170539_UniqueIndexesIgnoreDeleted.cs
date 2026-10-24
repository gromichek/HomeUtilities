using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueIndexesIgnoreDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VatRates_Code",
                table: "VatRates");

            migrationBuilder.DropIndex(
                name: "IX_Categories_SystemKey",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_VatRates_Code",
                table: "VatRates",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_SystemKey",
                table: "Categories",
                column: "SystemKey",
                unique: true,
                filter: "\"SystemKey\" IS NOT NULL AND \"IsDeleted\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VatRates_Code",
                table: "VatRates");

            migrationBuilder.DropIndex(
                name: "IX_Categories_SystemKey",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_VatRates_Code",
                table: "VatRates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_SystemKey",
                table: "Categories",
                column: "SystemKey",
                unique: true,
                filter: "\"SystemKey\" IS NOT NULL");
        }
    }
}

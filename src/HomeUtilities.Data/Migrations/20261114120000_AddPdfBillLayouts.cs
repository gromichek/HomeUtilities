using HomeUtilities.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <summary>PDF bill layouts (T-055): how to read the header of a supplier's PDF bill; user data.</summary>
    [DbContext(typeof(HomeUtilitiesDbContext))]
    [Migration("20261114120000_AddPdfBillLayouts")]
    public partial class AddPdfBillLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfBillLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Markers = table.Column<string>(type: "TEXT", nullable: false),
                    Fields = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfBillLayouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfBillLayouts_UpdatedAt",
                table: "PdfBillLayouts",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfBillLayouts");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillTemplatesAndDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "Bills",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BillTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Net = table.Column<decimal>(type: "TEXT", nullable: true),
                    VatPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    Gross = table.Column<decimal>(type: "TEXT", nullable: true),
                    Lines = table.Column<string>(type: "TEXT", nullable: false),
                    CustomFields = table.Column<string>(type: "TEXT", nullable: false),
                    Schedule = table.Column<string>(type: "TEXT", nullable: false),
                    Timing = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDayOffset = table.Column<int>(type: "INTEGER", nullable: true),
                    RemindDaysBefore = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastGeneratedPeriod = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillTemplates_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillTemplates_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillTemplates_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillTemplates_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TemplateId",
                table: "Bills",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_BillTemplates_CategoryId",
                table: "BillTemplates",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BillTemplates_ContractId",
                table: "BillTemplates",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_BillTemplates_PropertyId",
                table: "BillTemplates",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_BillTemplates_SupplierId",
                table: "BillTemplates",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_BillTemplates_UpdatedAt",
                table: "BillTemplates",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Bills_TemplateId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "Bills");
        }
    }
}

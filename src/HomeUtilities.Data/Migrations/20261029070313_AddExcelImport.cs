using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExcelImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "MeterReadings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "Bills",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Profile = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SheetName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContractId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ImportedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ReadingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BillCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportBatches_ImportProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ImportProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ImportBatchId",
                table: "Payments",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_ImportBatchId",
                table: "MeterReadings",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_ImportBatchId",
                table: "Bills",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ContractId",
                table: "ImportBatches",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ProfileId",
                table: "ImportBatches",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_UpdatedAt",
                table: "ImportBatches",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfiles_UpdatedAt",
                table: "ImportProfiles",
                column: "UpdatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_ImportBatches_ImportBatchId",
                table: "Bills",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MeterReadings_ImportBatches_ImportBatchId",
                table: "MeterReadings",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ImportBatches_ImportBatchId",
                table: "Payments",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_ImportBatches_ImportBatchId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_MeterReadings_ImportBatches_ImportBatchId",
                table: "MeterReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_ImportBatches_ImportBatchId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "ImportProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ImportBatchId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_ImportBatchId",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_Bills_ImportBatchId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Bills");
        }
    }
}

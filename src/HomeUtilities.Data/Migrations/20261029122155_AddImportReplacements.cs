using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImportReplacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "Meters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacementCount",
                table: "ImportBatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "ContractMeters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meters_ImportBatchId",
                table: "Meters",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMeters_ImportBatchId",
                table: "ContractMeters",
                column: "ImportBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractMeters_ImportBatches_ImportBatchId",
                table: "ContractMeters",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Meters_ImportBatches_ImportBatchId",
                table: "Meters",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractMeters_ImportBatches_ImportBatchId",
                table: "ContractMeters");

            migrationBuilder.DropForeignKey(
                name: "FK_Meters_ImportBatches_ImportBatchId",
                table: "Meters");

            migrationBuilder.DropIndex(
                name: "IX_Meters_ImportBatchId",
                table: "Meters");

            migrationBuilder.DropIndex(
                name: "IX_ContractMeters_ImportBatchId",
                table: "ContractMeters");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Meters");

            migrationBuilder.DropColumn(
                name: "ReplacementCount",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "ContractMeters");
        }
    }
}

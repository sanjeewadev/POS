using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdjustmentToBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockAdjustmentLines_ItemVariants_ItemVariantId",
                table: "StockAdjustmentLines");

            migrationBuilder.RenameColumn(
                name: "ItemVariantId",
                table: "StockAdjustmentLines",
                newName: "ItemBatchId");

            migrationBuilder.RenameIndex(
                name: "IX_StockAdjustmentLines_ItemVariantId",
                table: "StockAdjustmentLines",
                newName: "IX_StockAdjustmentLines_ItemBatchId");

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 14, 8, 29, 31, 484, DateTimeKind.Local).AddTicks(8642));

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustmentLines_ItemBatches_ItemBatchId",
                table: "StockAdjustmentLines",
                column: "ItemBatchId",
                principalTable: "ItemBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockAdjustmentLines_ItemBatches_ItemBatchId",
                table: "StockAdjustmentLines");

            migrationBuilder.RenameColumn(
                name: "ItemBatchId",
                table: "StockAdjustmentLines",
                newName: "ItemVariantId");

            migrationBuilder.RenameIndex(
                name: "IX_StockAdjustmentLines_ItemBatchId",
                table: "StockAdjustmentLines",
                newName: "IX_StockAdjustmentLines_ItemVariantId");

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 14, 8, 2, 58, 159, DateTimeKind.Local).AddTicks(1928));

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustmentLines_ItemVariants_ItemVariantId",
                table: "StockAdjustmentLines",
                column: "ItemVariantId",
                principalTable: "ItemVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

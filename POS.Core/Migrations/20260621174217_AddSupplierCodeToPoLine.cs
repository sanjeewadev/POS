using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCodeToPoLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierItemCode",
                table: "PoLines",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 21, 23, 12, 17, 445, DateTimeKind.Local).AddTicks(3830));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierItemCode",
                table: "PoLines");

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 21, 19, 58, 36, 568, DateTimeKind.Local).AddTicks(2462));
        }
    }
}

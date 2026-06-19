using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddExpressItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpressItemLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    TabCategory = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayLabel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ButtonColorHex = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    TextColorHex = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    GridRow = table.Column<int>(type: "INTEGER", nullable: false),
                    GridColumn = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpressItemLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpressItemLayouts_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 17, 14, 17, 3, 707, DateTimeKind.Local).AddTicks(5232));

            migrationBuilder.CreateIndex(
                name: "IX_ExpressItemLayouts_ItemVariantId",
                table: "ExpressItemLayouts",
                column: "ItemVariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpressItemLayouts");

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 17, 11, 56, 22, 451, DateTimeKind.Local).AddTicks(6099));
        }
    }
}

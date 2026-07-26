using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260726090000_AddBatchSellingPriceOverrideFoundation")]
    public sealed class AddBatchSellingPriceOverrideFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasSellingPriceOverride",
                table: "ItemBatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CataloguePriceSourceSnapshot",
                table: "SalesLines",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyUnknown",
                collation: "NOCASE");

            // Legacy batch prices were mirror/snapshot fields with no explicit
            // override intent. Preserve the safe production behaviour by making
            // every existing batch master-priced and synchronizing the mirrors.
            migrationBuilder.Sql(@"
UPDATE ItemBatches
SET HasSellingPriceOverride = 0,
    RetailPrice = (
        SELECT ItemVariants.RetailPrice
        FROM ItemVariants
        WHERE ItemVariants.Id = ItemBatches.ItemVariantId
    ),
    WholesalePrice = CASE
        WHEN COALESCE((
            SELECT ItemVariants.WholesalePrice
            FROM ItemVariants
            WHERE ItemVariants.Id = ItemBatches.ItemVariantId
        ), 0) > 0
        THEN (
            SELECT ItemVariants.WholesalePrice
            FROM ItemVariants
            WHERE ItemVariants.Id = ItemBatches.ItemVariantId
        )
        ELSE (
            SELECT ItemVariants.RetailPrice
            FROM ItemVariants
            WHERE ItemVariants.Id = ItemBatches.ItemVariantId
        )
    END;");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_CataloguePriceSourceSnapshot",
                table: "SalesLines",
                column: "CataloguePriceSourceSnapshot");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesLines_CataloguePriceSourceSnapshot",
                table: "SalesLines");

            migrationBuilder.DropColumn(
                name: "CataloguePriceSourceSnapshot",
                table: "SalesLines");

            migrationBuilder.DropColumn(
                name: "HasSellingPriceOverride",
                table: "ItemBatches");
        }
    }
}

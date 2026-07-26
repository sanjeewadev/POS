using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Database.Setup.Migrations
{
    public partial class AddBatchSellingPriceOverrideFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasSellingPriceOverride",
                schema: "dbo",
                table: "ItemBatches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CataloguePriceSourceSnapshot",
                schema: "dbo",
                table: "SalesLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyUnknown",
                collation: "Latin1_General_100_CI_AS_SC");

            migrationBuilder.Sql(@"
UPDATE b
SET b.HasSellingPriceOverride = 0,
    b.RetailPrice = v.RetailPrice,
    b.WholesalePrice = CASE
        WHEN v.WholesalePrice > 0 THEN v.WholesalePrice
        ELSE v.RetailPrice
    END
FROM [dbo].[ItemBatches] AS b
INNER JOIN [dbo].[ItemVariants] AS v
    ON v.Id = b.ItemVariantId;");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_CataloguePriceSourceSnapshot",
                schema: "dbo",
                table: "SalesLines",
                column: "CataloguePriceSourceSnapshot");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesLines_CataloguePriceSourceSnapshot",
                schema: "dbo",
                table: "SalesLines");

            migrationBuilder.DropColumn(
                name: "CataloguePriceSourceSnapshot",
                schema: "dbo",
                table: "SalesLines");

            migrationBuilder.DropColumn(
                name: "HasSellingPriceOverride",
                schema: "dbo",
                table: "ItemBatches");
        }
    }
}

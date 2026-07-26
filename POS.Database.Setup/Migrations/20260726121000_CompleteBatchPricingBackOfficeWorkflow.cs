using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Database.Setup.Migrations
{
    public partial class CompleteBatchPricingBackOfficeWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangeAction",
                schema: "dbo",
                table: "PriceChangeHistories",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Legacy",
                collation: "Latin1_General_100_CI_AS_SC");

            migrationBuilder.AddColumn<string>(
                name: "OldPriceSource",
                schema: "dbo",
                table: "PriceChangeHistories",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyUnknown",
                collation: "Latin1_General_100_CI_AS_SC");

            migrationBuilder.AddColumn<string>(
                name: "NewPriceSource",
                schema: "dbo",
                table: "PriceChangeHistories",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyUnknown",
                collation: "Latin1_General_100_CI_AS_SC");

            migrationBuilder.AddColumn<string>(
                name: "SellingPriceAction",
                schema: "dbo",
                table: "GrnLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "UseCurrentMasterPrice",
                collation: "Latin1_General_100_CI_AS_SC");

            migrationBuilder.Sql(@"
UPDATE [dbo].[PriceChangeHistories]
SET ChangeAction = CASE
        WHEN PriceLevel = 'Master' THEN 'LegacyMasterChange'
        WHEN PriceLevel = 'Batch' THEN 'LegacyBatchChange'
        ELSE 'Legacy'
    END,
    OldPriceSource = CASE WHEN PriceLevel = 'Master' THEN 'Master' ELSE 'LegacyUnknown' END,
    NewPriceSource = CASE WHEN PriceLevel = 'Master' THEN 'Master' ELSE 'LegacyUnknown' END;");

            migrationBuilder.Sql(@"
UPDATE [dbo].[GrnLines]
SET SellingPriceAction = CASE
        WHEN UpdateSellingPrices = 1 THEN 'UpdateMasterPrice'
        ELSE 'UseCurrentMasterPrice'
    END;");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ChangeAction",
                schema: "dbo",
                table: "PriceChangeHistories",
                column: "ChangeAction");
            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_OldPriceSource",
                schema: "dbo",
                table: "PriceChangeHistories",
                column: "OldPriceSource");
            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_NewPriceSource",
                schema: "dbo",
                table: "PriceChangeHistories",
                column: "NewPriceSource");
            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_SellingPriceAction",
                schema: "dbo",
                table: "GrnLines",
                column: "SellingPriceAction");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_PriceChangeHistories_ChangeAction", schema: "dbo", table: "PriceChangeHistories");
            migrationBuilder.DropIndex(name: "IX_PriceChangeHistories_OldPriceSource", schema: "dbo", table: "PriceChangeHistories");
            migrationBuilder.DropIndex(name: "IX_PriceChangeHistories_NewPriceSource", schema: "dbo", table: "PriceChangeHistories");
            migrationBuilder.DropIndex(name: "IX_GrnLines_SellingPriceAction", schema: "dbo", table: "GrnLines");
            migrationBuilder.DropColumn(name: "ChangeAction", schema: "dbo", table: "PriceChangeHistories");
            migrationBuilder.DropColumn(name: "OldPriceSource", schema: "dbo", table: "PriceChangeHistories");
            migrationBuilder.DropColumn(name: "NewPriceSource", schema: "dbo", table: "PriceChangeHistories");
            migrationBuilder.DropColumn(name: "SellingPriceAction", schema: "dbo", table: "GrnLines");
        }
    }
}

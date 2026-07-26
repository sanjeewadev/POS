using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260726120000_CompleteBatchPricingBackOfficeWorkflow")]
    public sealed class CompleteBatchPricingBackOfficeWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangeAction",
                table: "PriceChangeHistories",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "Legacy",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "OldPriceSource",
                table: "PriceChangeHistories",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyUnknown",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "NewPriceSource",
                table: "PriceChangeHistories",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyUnknown",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "SellingPriceAction",
                table: "GrnLines",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "UseCurrentMasterPrice",
                collation: "NOCASE");

            migrationBuilder.Sql(@"
UPDATE PriceChangeHistories
SET ChangeAction = CASE
        WHEN PriceLevel = 'Master' THEN 'LegacyMasterChange'
        WHEN PriceLevel = 'Batch' THEN 'LegacyBatchChange'
        ELSE 'Legacy'
    END,
    OldPriceSource = CASE
        WHEN PriceLevel = 'Master' THEN 'Master'
        ELSE 'LegacyUnknown'
    END,
    NewPriceSource = CASE
        WHEN PriceLevel = 'Master' THEN 'Master'
        ELSE 'LegacyUnknown'
    END;");

            migrationBuilder.Sql(@"
UPDATE GrnLines
SET SellingPriceAction = CASE
        WHEN UpdateSellingPrices = 1 THEN 'UpdateMasterPrice'
        ELSE 'UseCurrentMasterPrice'
    END;");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ChangeAction",
                table: "PriceChangeHistories",
                column: "ChangeAction");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_OldPriceSource",
                table: "PriceChangeHistories",
                column: "OldPriceSource");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_NewPriceSource",
                table: "PriceChangeHistories",
                column: "NewPriceSource");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_SellingPriceAction",
                table: "GrnLines",
                column: "SellingPriceAction");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_PriceChangeHistories_ChangeAction", table: "PriceChangeHistories");
            migrationBuilder.DropIndex(name: "IX_PriceChangeHistories_OldPriceSource", table: "PriceChangeHistories");
            migrationBuilder.DropIndex(name: "IX_PriceChangeHistories_NewPriceSource", table: "PriceChangeHistories");
            migrationBuilder.DropIndex(name: "IX_GrnLines_SellingPriceAction", table: "GrnLines");
            migrationBuilder.DropColumn(name: "ChangeAction", table: "PriceChangeHistories");
            migrationBuilder.DropColumn(name: "OldPriceSource", table: "PriceChangeHistories");
            migrationBuilder.DropColumn(name: "NewPriceSource", table: "PriceChangeHistories");
            migrationBuilder.DropColumn(name: "SellingPriceAction", table: "GrnLines");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713120000_AddFreeIssueSupplierClaimCompletion")]
    public class AddFreeIssueSupplierClaimCompletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeApprovedByUserId",
                table: "SalesLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeApprovedRole",
                table: "SalesLines",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "FreeIssueAppliedBy",
                table: "SalesLines",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FreeIssueAppliedAt",
                table: "SalesLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeIssueRuleSnapshotJson",
                table: "SalesLines",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FreeIssueSnapshotStatus",
                table: "SalesLines",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyUnknown",
                collation: "NOCASE");

            migrationBuilder.AddColumn<int>(
                name: "FreeApprovedByUserId",
                table: "FreeItemClaimLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeApprovedRole",
                table: "FreeItemClaimLogs",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "FreeIssueAppliedBy",
                table: "FreeItemClaimLogs",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FreeIssueAppliedAt",
                table: "FreeItemClaimLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeIssueRuleSnapshotJson",
                table: "FreeItemClaimLogs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FreeIssueSnapshotStatus",
                table: "FreeItemClaimLogs",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyUnknown",
                collation: "NOCASE");

            migrationBuilder.Sql(@"
UPDATE FreeItemClaimLogs
SET ClaimStatus = 'Draft'
WHERE ClaimStatus = 'Pending';");

            migrationBuilder.Sql(@"
UPDATE SalesLines
SET SupplierClaimStatus = 'Draft'
WHERE SupplierClaimStatus = 'Pending';");

            migrationBuilder.DropIndex(
                name: "IX_FreeIssueRules_RuleName",
                table: "FreeIssueRules");

            // The original SQLite column uses BINARY collation. Create the unique
            // index explicitly with NOCASE so rule names are unique regardless of case
            // without rebuilding the historical table.
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IX_FreeIssueRules_RuleName
ON FreeIssueRules (RuleName COLLATE NOCASE);");

            migrationBuilder.DropIndex(
                name: "IX_FreeItemClaimLogs_SalesLineId",
                table: "FreeItemClaimLogs");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SalesLineId",
                table: "FreeItemClaimLogs",
                column: "SalesLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_FreeApprovedByUserId",
                table: "SalesLines",
                column: "FreeApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_FreeIssueSnapshotStatus",
                table: "SalesLines",
                column: "FreeIssueSnapshotStatus");

            migrationBuilder.CreateTable(
                name: "FreeItemClaimAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FreeItemClaimLogId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerReturnLineId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ClaimValueReduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeItemClaimAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreeItemClaimAdjustments_CustomerReturnLines_CustomerReturnLineId",
                        column: x => x.CustomerReturnLineId,
                        principalTable: "CustomerReturnLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FreeItemClaimAdjustments_FreeItemClaimLogs_FreeItemClaimLogId",
                        column: x => x.FreeItemClaimLogId,
                        principalTable: "FreeItemClaimLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimAdjustments_CustomerReturnLineId",
                table: "FreeItemClaimAdjustments",
                column: "CustomerReturnLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimAdjustments_FreeItemClaimLogId",
                table: "FreeItemClaimAdjustments",
                column: "FreeItemClaimLogId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FreeItemClaimAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_SalesLines_FreeApprovedByUserId",
                table: "SalesLines");

            migrationBuilder.DropIndex(
                name: "IX_SalesLines_FreeIssueSnapshotStatus",
                table: "SalesLines");

            migrationBuilder.DropIndex(
                name: "IX_FreeItemClaimLogs_SalesLineId",
                table: "FreeItemClaimLogs");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SalesLineId",
                table: "FreeItemClaimLogs",
                column: "SalesLineId");

            migrationBuilder.DropIndex(
                name: "IX_FreeIssueRules_RuleName",
                table: "FreeIssueRules");

            migrationBuilder.Sql(@"
CREATE INDEX IX_FreeIssueRules_RuleName
ON FreeIssueRules (RuleName);");

            migrationBuilder.Sql(@"
UPDATE FreeItemClaimLogs
SET ClaimStatus = 'Pending'
WHERE ClaimStatus = 'Draft';");

            migrationBuilder.Sql(@"
UPDATE SalesLines
SET SupplierClaimStatus = 'Pending'
WHERE SupplierClaimStatus = 'Draft';");

            migrationBuilder.DropColumn(name: "FreeApprovedByUserId", table: "SalesLines");
            migrationBuilder.DropColumn(name: "FreeApprovedRole", table: "SalesLines");
            migrationBuilder.DropColumn(name: "FreeIssueAppliedBy", table: "SalesLines");
            migrationBuilder.DropColumn(name: "FreeIssueAppliedAt", table: "SalesLines");
            migrationBuilder.DropColumn(name: "FreeIssueRuleSnapshotJson", table: "SalesLines");
            migrationBuilder.DropColumn(name: "FreeIssueSnapshotStatus", table: "SalesLines");

            migrationBuilder.DropColumn(name: "FreeApprovedByUserId", table: "FreeItemClaimLogs");
            migrationBuilder.DropColumn(name: "FreeApprovedRole", table: "FreeItemClaimLogs");
            migrationBuilder.DropColumn(name: "FreeIssueAppliedBy", table: "FreeItemClaimLogs");
            migrationBuilder.DropColumn(name: "FreeIssueAppliedAt", table: "FreeItemClaimLogs");
            migrationBuilder.DropColumn(name: "FreeIssueRuleSnapshotJson", table: "FreeItemClaimLogs");
            migrationBuilder.DropColumn(name: "FreeIssueSnapshotStatus", table: "FreeItemClaimLogs");
        }
    }
}

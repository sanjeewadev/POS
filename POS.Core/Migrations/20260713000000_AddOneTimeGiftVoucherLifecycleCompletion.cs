using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713000000_AddOneTimeGiftVoucherLifecycleCompletion")]
    public class AddOneTimeGiftVoucherLifecycleCompletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GiftVoucherIssueTotal",
                table: "SalesHeaders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "GiftVoucherAuthorizedBy",
                table: "SalesPayments",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PrintCount",
                table: "GiftVouchers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPrintedAt",
                table: "GiftVouchers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPrintedBy",
                table: "GiftVouchers",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StatusBeforeBlock",
                table: "GiftVouchers",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<int>(
                name: "SalesPaymentId",
                table: "GiftVoucherTransactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerReturnHeaderId",
                table: "GiftVoucherTransactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceReturnNo",
                table: "GiftVoucherTransactions",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceKey",
                table: "GiftVoucherTransactions",
                type: "TEXT",
                maxLength: 120,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "AuthorizedBy",
                table: "GiftVoucherTransactions",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "GiftVoucherRefundAmount",
                table: "CustomerReturnHeaders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ReplacementGiftVoucherId",
                table: "CustomerReturnHeaders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementGiftVoucherNo",
                table: "CustomerReturnHeaders",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.Sql(@"
UPDATE GiftVouchers
SET Status = 'Voided'
WHERE Status = 'Cancelled';");

            migrationBuilder.Sql(@"
UPDATE GiftVouchers
SET PrintCount = CASE
        WHEN PrintedAt IS NOT NULL OR PrintedBy <> '' THEN 1
        ELSE 0
    END,
    LastPrintedAt = PrintedAt,
    LastPrintedBy = PrintedBy;");

            migrationBuilder.Sql(@"
UPDATE SalesHeaders
SET GiftVoucherIssueTotal = ROUND(COALESCE((
    SELECT SUM(LineTotal)
    FROM SalesLines
    WHERE SalesLines.SalesHeaderId = SalesHeaders.Id
      AND SalesLines.IsGiftVoucherSale = 1
), 0), 2);");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_SalesPaymentId",
                table: "GiftVoucherTransactions",
                column: "SalesPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_CustomerReturnHeaderId",
                table: "GiftVoucherTransactions",
                column: "CustomerReturnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_ReferenceReturnNo",
                table: "GiftVoucherTransactions",
                column: "ReferenceReturnNo");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_ReferenceKey",
                table: "GiftVoucherTransactions",
                column: "ReferenceKey",
                unique: true,
                filter: "ReferenceKey IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_ReplacementGiftVoucherId",
                table: "CustomerReturnHeaders",
                column: "ReplacementGiftVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_ReplacementGiftVoucherNo",
                table: "CustomerReturnHeaders",
                column: "ReplacementGiftVoucherNo");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GiftVoucherTransactions_SalesPaymentId",
                table: "GiftVoucherTransactions");
            migrationBuilder.DropIndex(
                name: "IX_GiftVoucherTransactions_CustomerReturnHeaderId",
                table: "GiftVoucherTransactions");
            migrationBuilder.DropIndex(
                name: "IX_GiftVoucherTransactions_ReferenceReturnNo",
                table: "GiftVoucherTransactions");
            migrationBuilder.DropIndex(
                name: "IX_GiftVoucherTransactions_ReferenceKey",
                table: "GiftVoucherTransactions");
            migrationBuilder.DropIndex(
                name: "IX_CustomerReturnHeaders_ReplacementGiftVoucherId",
                table: "CustomerReturnHeaders");
            migrationBuilder.DropIndex(
                name: "IX_CustomerReturnHeaders_ReplacementGiftVoucherNo",
                table: "CustomerReturnHeaders");

            migrationBuilder.DropColumn(name: "GiftVoucherIssueTotal", table: "SalesHeaders");
            migrationBuilder.DropColumn(name: "GiftVoucherAuthorizedBy", table: "SalesPayments");
            migrationBuilder.DropColumn(name: "PrintCount", table: "GiftVouchers");
            migrationBuilder.DropColumn(name: "LastPrintedAt", table: "GiftVouchers");
            migrationBuilder.DropColumn(name: "LastPrintedBy", table: "GiftVouchers");
            migrationBuilder.DropColumn(name: "StatusBeforeBlock", table: "GiftVouchers");
            migrationBuilder.DropColumn(name: "SalesPaymentId", table: "GiftVoucherTransactions");
            migrationBuilder.DropColumn(name: "CustomerReturnHeaderId", table: "GiftVoucherTransactions");
            migrationBuilder.DropColumn(name: "ReferenceReturnNo", table: "GiftVoucherTransactions");
            migrationBuilder.DropColumn(name: "ReferenceKey", table: "GiftVoucherTransactions");
            migrationBuilder.DropColumn(name: "AuthorizedBy", table: "GiftVoucherTransactions");
            migrationBuilder.DropColumn(name: "GiftVoucherRefundAmount", table: "CustomerReturnHeaders");
            migrationBuilder.DropColumn(name: "ReplacementGiftVoucherId", table: "CustomerReturnHeaders");
            migrationBuilder.DropColumn(name: "ReplacementGiftVoucherNo", table: "CustomerReturnHeaders");
        }
    }
}

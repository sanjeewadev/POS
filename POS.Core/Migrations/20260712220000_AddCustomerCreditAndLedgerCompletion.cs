using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260712220000_AddCustomerCreditAndLedgerCompletion")]
    public class AddCustomerCreditAndLedgerCompletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalesHeaderId",
                table: "CustomerLedgers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerReturnHeaderId",
                table: "CustomerLedgers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerPaymentReceiptId",
                table: "CustomerLedgers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "CustomerLedgers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "CustomerLedgers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedAmount",
                table: "CustomerLedgers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OutstandingAmount",
                table: "CustomerLedgers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CustomerLedgers",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Open",
                collation: "NOCASE");

            migrationBuilder.AddColumn<decimal>(
                name: "AccountCreditAmount",
                table: "CustomerReturnHeaders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CashRefundAmount",
                table: "CustomerReturnHeaders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
UPDATE CustomerLedgers
SET OriginalAmount = ROUND(CASE WHEN DebitAmount > 0 THEN DebitAmount ELSE CreditAmount END, 2),
    AllocatedAmount = 0,
    OutstandingAmount = ROUND(CASE WHEN DebitAmount > 0 THEN DebitAmount ELSE 0 END, 2),
    Status = CASE WHEN DebitAmount > 0 THEN 'Open' ELSE 'Paid' END;");

            migrationBuilder.Sql(@"
UPDATE CustomerReturnHeaders
SET CashRefundAmount = TotalRefundAmount,
    AccountCreditAmount = 0
WHERE CashRefundAmount = 0 AND AccountCreditAmount = 0;");

            migrationBuilder.CreateTable(
                name: "CustomerPaymentReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiptNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    ReceiptToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerMasterId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferenceNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    BankOrCardType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    DestinationAccount = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProcessedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TerminalNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, collation: "NOCASE"),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CashMovementId = table.Column<int>(type: "INTEGER", nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentReceipts_CashMovements_CashMovementId",
                        column: x => x.CashMovementId,
                        principalTable: "CashMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentReceipts_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentReceipts_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedgerAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerMasterId = table.Column<int>(type: "INTEGER", nullable: false),
                    DebitLedgerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreditLedgerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerPaymentReceiptId = table.Column<int>(type: "INTEGER", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLedgerAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerAllocations_CustomerLedgers_CreditLedgerId",
                        column: x => x.CreditLedgerId,
                        principalTable: "CustomerLedgers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerAllocations_CustomerLedgers_DebitLedgerId",
                        column: x => x.DebitLedgerId,
                        principalTable: "CustomerLedgers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerAllocations_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerAllocations_CustomerPaymentReceipts_CustomerPaymentReceiptId",
                        column: x => x.CustomerPaymentReceiptId,
                        principalTable: "CustomerPaymentReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_DueDate",
                table: "CustomerLedgers",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_Status",
                table: "CustomerLedgers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_SalesHeaderId",
                table: "CustomerLedgers",
                column: "SalesHeaderId",
                unique: true,
                filter: "SalesHeaderId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_CustomerReturnHeaderId",
                table: "CustomerLedgers",
                column: "CustomerReturnHeaderId",
                unique: true,
                filter: "CustomerReturnHeaderId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_CustomerPaymentReceiptId",
                table: "CustomerLedgers",
                column: "CustomerPaymentReceiptId",
                unique: true,
                filter: "CustomerPaymentReceiptId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_ReceiptNo",
                table: "CustomerPaymentReceipts",
                column: "ReceiptNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_ReceiptToken",
                table: "CustomerPaymentReceipts",
                column: "ReceiptToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_CustomerMasterId",
                table: "CustomerPaymentReceipts",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_PaymentDate",
                table: "CustomerPaymentReceipts",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_PaymentMethod",
                table: "CustomerPaymentReceipts",
                column: "PaymentMethod");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_ShiftSessionId",
                table: "CustomerPaymentReceipts",
                column: "ShiftSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_CashMovementId",
                table: "CustomerPaymentReceipts",
                column: "CashMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerAllocations_DebitLedgerId_CreditLedgerId",
                table: "CustomerLedgerAllocations",
                columns: new[] { "DebitLedgerId", "CreditLedgerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerAllocations_CreditLedgerId",
                table: "CustomerLedgerAllocations",
                column: "CreditLedgerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerAllocations_CustomerMasterId",
                table: "CustomerLedgerAllocations",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerAllocations_CustomerPaymentReceiptId",
                table: "CustomerLedgerAllocations",
                column: "CustomerPaymentReceiptId");

            migrationBuilder.Sql(@"
INSERT OR IGNORE INTO DocumentSequences
    (DocumentType, Prefix, NextSequenceNumber, PaddingLength, UpdatedAt)
VALUES
    ('CPR', 'CPR-', 1, 6, datetime('now', 'localtime'));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CustomerLedgerAllocations");
            migrationBuilder.DropTable(name: "CustomerPaymentReceipts");

            migrationBuilder.DropIndex(name: "IX_CustomerLedgers_DueDate", table: "CustomerLedgers");
            migrationBuilder.DropIndex(name: "IX_CustomerLedgers_Status", table: "CustomerLedgers");
            migrationBuilder.DropIndex(name: "IX_CustomerLedgers_SalesHeaderId", table: "CustomerLedgers");
            migrationBuilder.DropIndex(name: "IX_CustomerLedgers_CustomerReturnHeaderId", table: "CustomerLedgers");
            migrationBuilder.DropIndex(name: "IX_CustomerLedgers_CustomerPaymentReceiptId", table: "CustomerLedgers");

            migrationBuilder.DropColumn(name: "SalesHeaderId", table: "CustomerLedgers");
            migrationBuilder.DropColumn(name: "CustomerReturnHeaderId", table: "CustomerLedgers");
            migrationBuilder.DropColumn(name: "CustomerPaymentReceiptId", table: "CustomerLedgers");
            migrationBuilder.DropColumn(name: "DueDate", table: "CustomerLedgers");
            migrationBuilder.DropColumn(name: "OriginalAmount", table: "CustomerLedgers");
            migrationBuilder.DropColumn(name: "AllocatedAmount", table: "CustomerLedgers");
            migrationBuilder.DropColumn(name: "OutstandingAmount", table: "CustomerLedgers");
            migrationBuilder.DropColumn(name: "Status", table: "CustomerLedgers");
            migrationBuilder.DropColumn(name: "AccountCreditAmount", table: "CustomerReturnHeaders");
            migrationBuilder.DropColumn(name: "CashRefundAmount", table: "CustomerReturnHeaders");

            migrationBuilder.Sql("DELETE FROM DocumentSequences WHERE DocumentType = 'CPR';");
        }
    }
}

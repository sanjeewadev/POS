using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260712150000_AddCashierCartLifecycleAndCheckoutSafety")]
    public class AddCashierCartLifecycleAndCheckoutSafety : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutToken",
                table: "SalesHeaders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardLastDigits",
                table: "SalesPayments",
                type: "TEXT",
                maxLength: 6,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeAmount",
                table: "SalesPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "EnteredBy",
                table: "SalesPayments",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TenderedAmount",
                table: "SalesPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TerminalNo",
                table: "SalesPayments",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.CreateTable(
                name: "CashierCartSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CartToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TerminalNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, collation: "NOCASE"),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    CustomerMasterId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerCodeSnapshot = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    CustomerNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CustomerTypeSnapshot = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    CustomerSnapshotJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    IsWholesaleMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    InvoiceDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Active", collation: "NOCASE"),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HeldAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RecalledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    HeldBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RecalledBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CancelledBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CancellationReasonCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    CancellationReasonText = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    RecallCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SalesHeaderId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierCartSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierCartSessions_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierCartSessions_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierCartSessions_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashierCartLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CashierCartSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LineType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemBatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierCartLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierCartLines_CashierCartSessions_CashierCartSessionId",
                        column: x => x.CashierCartSessionId,
                        principalTable: "CashierCartSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CheckoutToken",
                table: "SalesHeaders",
                column: "CheckoutToken",
                unique: true,
                filter: "\"CheckoutToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_ActiveOwner",
                table: "CashierCartSessions",
                columns: new[] { "TerminalNo", "ShiftSessionId", "CashierName" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_CartToken",
                table: "CashierCartSessions",
                column: "CartToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_CashierName_Status",
                table: "CashierCartSessions",
                columns: new[] { "CashierName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_CustomerMasterId",
                table: "CashierCartSessions",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_ReferenceNo",
                table: "CashierCartSessions",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_SalesHeaderId",
                table: "CashierCartSessions",
                column: "SalesHeaderId",
                unique: true,
                filter: "\"SalesHeaderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_ShiftSessionId",
                table: "CashierCartSessions",
                column: "ShiftSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_TerminalNo_ShiftSessionId_Status",
                table: "CashierCartSessions",
                columns: new[] { "TerminalNo", "ShiftSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_UpdatedAtUtc",
                table: "CashierCartSessions",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartLines_CashierCartSessionId_LineNumber",
                table: "CashierCartLines",
                columns: new[] { "CashierCartSessionId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartLines_ItemBatchId",
                table: "CashierCartLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartLines_ItemVariantId",
                table: "CashierCartLines",
                column: "ItemVariantId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CashierCartLines");
            migrationBuilder.DropTable(name: "CashierCartSessions");

            migrationBuilder.DropIndex(
                name: "IX_SalesHeaders_CheckoutToken",
                table: "SalesHeaders");

            migrationBuilder.DropColumn(name: "CheckoutToken", table: "SalesHeaders");
            migrationBuilder.DropColumn(name: "CardLastDigits", table: "SalesPayments");
            migrationBuilder.DropColumn(name: "ChangeAmount", table: "SalesPayments");
            migrationBuilder.DropColumn(name: "EnteredBy", table: "SalesPayments");
            migrationBuilder.DropColumn(name: "TenderedAmount", table: "SalesPayments");
            migrationBuilder.DropColumn(name: "TerminalNo", table: "SalesPayments");
        }
    }
}

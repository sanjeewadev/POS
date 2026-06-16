using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierAndShiftModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminalNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OpeningTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosingTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OpeningFloat = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalCashSales = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalCardSales = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalOtherSales = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalPaidIn = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalPaidOut = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExpectedClosingCash = table.Column<decimal>(type: "TEXT", nullable: false),
                    ActualClosingCash = table.Column<decimal>(type: "TEXT", nullable: true),
                    Variance = table.Column<decimal>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceNo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TerminalNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GrossTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    NetTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AmountTendered = table.Column<decimal>(type: "TEXT", nullable: false),
                    BalanceReturned = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesHeaders_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TillLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReferenceNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TillLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TillLedgers_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SalesHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemBatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemDescription = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    CostPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "TEXT", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsReturned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesLines_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesLines_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 14, 15, 57, 33, 143, DateTimeKind.Local).AddTicks(8271));

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_ShiftSessionId",
                table: "SalesHeaders",
                column: "ShiftSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_ItemBatchId",
                table: "SalesLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_SalesHeaderId",
                table: "SalesLines",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_TillLedgers_ShiftSessionId",
                table: "TillLedgers",
                column: "ShiftSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesLines");

            migrationBuilder.DropTable(
                name: "TillLedgers");

            migrationBuilder.DropTable(
                name: "SalesHeaders");

            migrationBuilder.DropTable(
                name: "ShiftSessions");

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 14, 8, 29, 31, 484, DateTimeKind.Local).AddTicks(8642));
        }
    }
}

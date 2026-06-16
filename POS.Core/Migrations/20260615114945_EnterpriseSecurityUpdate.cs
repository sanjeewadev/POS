using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseSecurityUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TillLedgers");

            migrationBuilder.DropColumn(
                name: "ActualClosingCash",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "ExpectedClosingCash",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "OpeningFloat",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "OpeningTime",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "TotalCardSales",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "TotalOtherSales",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "TotalPaidIn",
                table: "ShiftSessions");

            migrationBuilder.RenameColumn(
                name: "TotalPaidOut",
                table: "ShiftSessions",
                newName: "StartTime");

            migrationBuilder.RenameColumn(
                name: "ClosingTime",
                table: "ShiftSessions",
                newName: "EndTime");

            migrationBuilder.AlterColumn<decimal>(
                name: "Variance",
                table: "ShiftSessions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCashSales",
                table: "ShiftSessions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualCash",
                table: "ShiftSessions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCash",
                table: "ShiftSessions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningCash",
                table: "ShiftSessions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "SalesHeaders",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidTimestamp",
                table: "SalesHeaders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedBy",
                table: "SalesHeaders",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovementType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReasonCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReferenceVoucherNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashMovements_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    VatRegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CustomerType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CustomerGroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCreditLocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReturnHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReturnNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OriginalInvoiceNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TerminalNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalRefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundMethod = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturnHeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromoRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    FamilyType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsStackable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerMasterId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DocumentRef = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProcessedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerLedgers_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReturnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerReturnHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemDescription = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotalRefund = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReturnReason = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InventoryAction = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_CustomerReturnHeaders_CustomerReturnHeaderId",
                        column: x => x.CustomerReturnHeaderId,
                        principalTable: "CustomerReturnHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromoConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PromoRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetFamily = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetValue = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoConditions_PromoRules_PromoRuleId",
                        column: x => x.PromoRuleId,
                        principalTable: "PromoRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromoRewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PromoRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    RewardType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RewardValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoRewards_PromoRules_PromoRuleId",
                        column: x => x.PromoRuleId,
                        principalTable: "PromoRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 15, 17, 19, 44, 262, DateTimeKind.Local).AddTicks(211));

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ShiftSessionId",
                table: "CashMovements",
                column: "ShiftSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_CustomerMasterId",
                table: "CustomerLedgers",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_CustomerCode",
                table: "CustomerMasters",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_Phone",
                table: "CustomerMasters",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_ReturnNo",
                table: "CustomerReturnHeaders",
                column: "ReturnNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_CustomerReturnHeaderId",
                table: "CustomerReturnLines",
                column: "CustomerReturnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoConditions_PromoRuleId",
                table: "PromoConditions",
                column: "PromoRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoRewards_PromoRuleId",
                table: "PromoRewards",
                column: "PromoRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "CustomerLedgers");

            migrationBuilder.DropTable(
                name: "CustomerReturnLines");

            migrationBuilder.DropTable(
                name: "PromoConditions");

            migrationBuilder.DropTable(
                name: "PromoRewards");

            migrationBuilder.DropTable(
                name: "CustomerMasters");

            migrationBuilder.DropTable(
                name: "CustomerReturnHeaders");

            migrationBuilder.DropTable(
                name: "PromoRules");

            migrationBuilder.DropColumn(
                name: "ActualCash",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "ExpectedCash",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "OpeningCash",
                table: "ShiftSessions");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "SalesHeaders");

            migrationBuilder.DropColumn(
                name: "VoidTimestamp",
                table: "SalesHeaders");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "SalesHeaders");

            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "ShiftSessions",
                newName: "TotalPaidOut");

            migrationBuilder.RenameColumn(
                name: "EndTime",
                table: "ShiftSessions",
                newName: "ClosingTime");

            migrationBuilder.AlterColumn<decimal>(
                name: "Variance",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCashSales",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualClosingCash",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedClosingCash",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningFloat",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpeningTime",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCardSales",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalOtherSales",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPaidIn",
                table: "ShiftSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TillLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReferenceNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
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

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 14, 15, 57, 33, 143, DateTimeKind.Local).AddTicks(8271));

            migrationBuilder.CreateIndex(
                name: "IX_TillLedgers_ShiftSessionId",
                table: "TillLedgers",
                column: "ShiftSessionId");
        }
    }
}

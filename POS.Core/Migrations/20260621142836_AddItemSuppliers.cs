using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForcePasswordReset",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.CreateTable(
                name: "FreeItemClaimLogs",
                columns: table => new
                {
                    ClaimId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UnitCostAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReasonCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsRecoverable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CashierId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeItemClaimLogs", x => x.ClaimId);
                });

            migrationBuilder.CreateTable(
                name: "GiftVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    InitialAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActivationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SoldInInvoiceNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftVouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemSuppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastCostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinimumOrderQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSuppliers_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuoteNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CustomerPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TerminalNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    GrossTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationHeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    SettingKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.SettingKey);
                });

            migrationBuilder.CreateTable(
                name: "QuotationLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuotationHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ItemDescription = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationLines_QuotationHeaders_QuotationHeaderId",
                        column: x => x.QuotationHeaderId,
                        principalTable: "QuotationHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 21, 19, 58, 36, 568, DateTimeKind.Local).AddTicks(2462));

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_ItemVariantId",
                table: "ItemSuppliers",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_SupplierId",
                table: "ItemSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationHeaders_QuoteNo",
                table: "QuotationHeaders",
                column: "QuoteNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLines_QuotationHeaderId",
                table: "QuotationLines",
                column: "QuotationHeaderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreeItemClaimLogs");

            migrationBuilder.DropTable(
                name: "GiftVouchers");

            migrationBuilder.DropTable(
                name: "ItemSuppliers");

            migrationBuilder.DropTable(
                name: "QuotationLines");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "QuotationHeaders");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "ForcePasswordReset",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 17, 14, 17, 3, 707, DateTimeKind.Local).AddTicks(5232));
        }
    }
}

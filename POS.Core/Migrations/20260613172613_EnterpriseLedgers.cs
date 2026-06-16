using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseLedgers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTaxInclusive",
                table: "GrnHeaders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DocumentSequences",
                columns: table => new
                {
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    NextSequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PaddingLength = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSequences", x => x.DocumentType);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ReferenceDocument = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ReferenceDocument = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ChargeAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    BalanceAfterTransaction = table.Column<decimal>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsPaid = table.Column<bool>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierLedgers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "DocumentSequences",
                columns: new[] { "DocumentType", "NextSequenceNumber", "PaddingLength", "Prefix", "UpdatedAt" },
                values: new object[] { "GRN", 1, 5, "GRN-", new DateTime(2026, 6, 13, 22, 56, 13, 570, DateTimeKind.Local).AddTicks(1008) });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ItemVariantId",
                table: "InventoryTransactions",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReferenceDocument",
                table: "InventoryTransactions",
                column: "ReferenceDocument");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_ReferenceDocument",
                table: "SupplierLedgers",
                column: "ReferenceDocument");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_SupplierId",
                table: "SupplierLedgers",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSequences");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "SupplierLedgers");

            migrationBuilder.DropColumn(
                name: "IsTaxInclusive",
                table: "GrnHeaders");
        }
    }
}

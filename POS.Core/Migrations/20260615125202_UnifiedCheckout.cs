using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesHeaders_ShiftSessions_ShiftSessionId",
                table: "SalesHeaders");

            migrationBuilder.DropIndex(
                name: "IX_SalesHeaders_ShiftSessionId",
                table: "SalesHeaders");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "SalesHeaders");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "SalesHeaders");

            migrationBuilder.DropColumn(
                name: "VoidTimestamp",
                table: "SalesHeaders");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "SalesHeaders");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDiscount",
                table: "SalesHeaders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "NetTotal",
                table: "SalesHeaders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossTotal",
                table: "SalesHeaders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceReturned",
                table: "SalesHeaders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "AmountTendered",
                table: "SalesHeaders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "SalesHeaders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SalesPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SalesHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferenceNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BankOrCardType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesPayments_SalesHeaders_SalesHeaderId",
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
                value: new DateTime(2026, 6, 15, 18, 22, 1, 822, DateTimeKind.Local).AddTicks(3370));

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_SalesHeaderId",
                table: "SalesPayments",
                column: "SalesHeaderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesPayments");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "SalesHeaders");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDiscount",
                table: "SalesHeaders",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "NetTotal",
                table: "SalesHeaders",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossTotal",
                table: "SalesHeaders",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceReturned",
                table: "SalesHeaders",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "AmountTendered",
                table: "SalesHeaders",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "SalesHeaders",
                type: "INTEGER",
                nullable: true);

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

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 15, 17, 19, 44, 262, DateTimeKind.Local).AddTicks(211));

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_ShiftSessionId",
                table: "SalesHeaders",
                column: "ShiftSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesHeaders_ShiftSessions_ShiftSessionId",
                table: "SalesHeaders",
                column: "ShiftSessionId",
                principalTable: "ShiftSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyDiscountProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoyaltyCardNumber",
                table: "CustomerMasters",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LoyaltyDiscountExpiryDate",
                table: "CustomerMasters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyDiscountProfileId",
                table: "CustomerMasters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyPointsBalance",
                table: "CustomerMasters",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "LoyaltyDiscountProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DiscountType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyDiscountProfiles", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 17, 11, 56, 22, 451, DateTimeKind.Local).AddTicks(6099));

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_LoyaltyDiscountProfileId",
                table: "CustomerMasters",
                column: "LoyaltyDiscountProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ReferenceVoucherNo",
                table: "CashMovements",
                column: "ReferenceVoucherNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_Timestamp",
                table: "CashMovements",
                column: "Timestamp");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerMasters_LoyaltyDiscountProfiles_LoyaltyDiscountProfileId",
                table: "CustomerMasters",
                column: "LoyaltyDiscountProfileId",
                principalTable: "LoyaltyDiscountProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerMasters_LoyaltyDiscountProfiles_LoyaltyDiscountProfileId",
                table: "CustomerMasters");

            migrationBuilder.DropTable(
                name: "LoyaltyDiscountProfiles");

            migrationBuilder.DropIndex(
                name: "IX_CustomerMasters_LoyaltyDiscountProfileId",
                table: "CustomerMasters");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_ReferenceVoucherNo",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_Timestamp",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "LoyaltyCardNumber",
                table: "CustomerMasters");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscountExpiryDate",
                table: "CustomerMasters");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscountProfileId",
                table: "CustomerMasters");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointsBalance",
                table: "CustomerMasters");

            migrationBuilder.UpdateData(
                table: "DocumentSequences",
                keyColumn: "DocumentType",
                keyValue: "GRN",
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 15, 18, 22, 1, 822, DateTimeKind.Local).AddTicks(3370));
        }
    }
}

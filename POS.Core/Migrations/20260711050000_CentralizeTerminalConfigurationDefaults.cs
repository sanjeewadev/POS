using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260711050000_CentralizeTerminalConfigurationDefaults")]
    public class CentralizeTerminalConfigurationDefaults :
        Migration
    {
        protected override void Up(
            MigrationBuilder migrationBuilder)
        {
            RebuildTerminalSettingsTable(
                migrationBuilder,
                terminalNameDefault: string.Empty,
                receiptPrinterDefault: string.Empty,
                autoPrintDefault: false,
                cashDrawerDefault: false,
                openDrawerDefault: false);
        }

        protected override void Down(
            MigrationBuilder migrationBuilder)
        {
            RebuildTerminalSettingsTable(
                migrationBuilder,
                terminalNameDefault: "Cashier Terminal 01",
                receiptPrinterDefault: "POS-80",
                autoPrintDefault: true,
                cashDrawerDefault: true,
                openDrawerDefault: true);
        }

        private static void RebuildTerminalSettingsTable(
            MigrationBuilder migrationBuilder,
            string terminalNameDefault,
            string receiptPrinterDefault,
            bool autoPrintDefault,
            bool cashDrawerDefault,
            bool openDrawerDefault)
        {
            string safeTerminalNameDefault =
                EscapeSqlLiteral(
                    terminalNameDefault);

            string safeReceiptPrinterDefault =
                EscapeSqlLiteral(
                    receiptPrinterDefault);

            int autoPrintValue =
                autoPrintDefault ? 1 : 0;

            int cashDrawerValue =
                cashDrawerDefault ? 1 : 0;

            int openDrawerValue =
                openDrawerDefault ? 1 : 0;

            migrationBuilder.Sql(
                """
                DROP TABLE IF EXISTS "__TerminalSettings_Phase6";
                """);

            migrationBuilder.Sql(
                $"""
                CREATE TABLE "__TerminalSettings_Phase6"
                (
                    "Id" INTEGER NOT NULL
                        CONSTRAINT "PK_TerminalSettings"
                        PRIMARY KEY AUTOINCREMENT,

                    "TerminalNo" TEXT NOT NULL,

                    "TerminalName" TEXT NOT NULL
                        DEFAULT '{safeTerminalNameDefault}',

                    "MachineName" TEXT NOT NULL
                        DEFAULT '',

                    "Location" TEXT NOT NULL
                        DEFAULT 'Main Store',

                    "PrinterMode" TEXT NOT NULL
                        DEFAULT 'WindowsSpooler',

                    "ReceiptPrinterName" TEXT NOT NULL
                        DEFAULT '{safeReceiptPrinterDefault}',

                    "ReceiptPaperWidth" INTEGER NOT NULL
                        DEFAULT 80,

                    "AutoPrintReceipt" INTEGER NOT NULL
                        DEFAULT {autoPrintValue},

                    "ReceiptCopies" INTEGER NOT NULL
                        DEFAULT 1,

                    "EnableCashDrawer" INTEGER NOT NULL
                        DEFAULT {cashDrawerValue},

                    "DrawerKickCode" TEXT NOT NULL
                        DEFAULT '27,112,0,25,250',

                    "OpenDrawerAfterCashSale" INTEGER NOT NULL
                        DEFAULT {openDrawerValue},

                    "ScannerSuffixAction" TEXT NOT NULL
                        DEFAULT 'Enter',

                    "EnableScale" INTEGER NOT NULL
                        DEFAULT 0,

                    "ScaleComPort" TEXT NOT NULL
                        DEFAULT 'COM1',

                    "ScaleBaudRate" INTEGER NOT NULL
                        DEFAULT 9600,

                    "EnablePoleDisplay" INTEGER NOT NULL
                        DEFAULT 0,

                    "PoleDisplayComPort" TEXT NOT NULL
                        DEFAULT 'COM2',

                    "PoleWelcomeMessage" TEXT NOT NULL
                        DEFAULT 'WELCOME',

                    "EnableEftpos" INTEGER NOT NULL
                        DEFAULT 0,

                    "EftposProvider" TEXT NOT NULL
                        DEFAULT '',

                    "EftposPortOrIp" TEXT NOT NULL
                        DEFAULT '',

                    "IsActive" INTEGER NOT NULL
                        DEFAULT 1,

                    "CreatedAt" TEXT NOT NULL
                        DEFAULT CURRENT_TIMESTAMP,

                    "UpdatedAt" TEXT NULL,

                    "UpdatedBy" TEXT NOT NULL
                        DEFAULT '',

                    "AutoLockTimeoutMinutes" INTEGER NOT NULL
                        DEFAULT 10
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "__TerminalSettings_Phase6"
                (
                    "Id",
                    "TerminalNo",
                    "TerminalName",
                    "MachineName",
                    "Location",
                    "PrinterMode",
                    "ReceiptPrinterName",
                    "ReceiptPaperWidth",
                    "AutoPrintReceipt",
                    "ReceiptCopies",
                    "EnableCashDrawer",
                    "DrawerKickCode",
                    "OpenDrawerAfterCashSale",
                    "ScannerSuffixAction",
                    "EnableScale",
                    "ScaleComPort",
                    "ScaleBaudRate",
                    "EnablePoleDisplay",
                    "PoleDisplayComPort",
                    "PoleWelcomeMessage",
                    "EnableEftpos",
                    "EftposProvider",
                    "EftposPortOrIp",
                    "IsActive",
                    "CreatedAt",
                    "UpdatedAt",
                    "UpdatedBy",
                    "AutoLockTimeoutMinutes"
                )
                SELECT
                    "Id",
                    "TerminalNo",
                    "TerminalName",
                    "MachineName",
                    "Location",
                    "PrinterMode",
                    "ReceiptPrinterName",
                    "ReceiptPaperWidth",
                    "AutoPrintReceipt",
                    "ReceiptCopies",
                    "EnableCashDrawer",
                    "DrawerKickCode",
                    "OpenDrawerAfterCashSale",
                    "ScannerSuffixAction",
                    "EnableScale",
                    "ScaleComPort",
                    "ScaleBaudRate",
                    "EnablePoleDisplay",
                    "PoleDisplayComPort",
                    "PoleWelcomeMessage",
                    "EnableEftpos",
                    "EftposProvider",
                    "EftposPortOrIp",
                    "IsActive",
                    "CreatedAt",
                    "UpdatedAt",
                    "UpdatedBy",
                    "AutoLockTimeoutMinutes"
                FROM "TerminalSettings";
                """);

            migrationBuilder.Sql(
                """
                DROP TABLE "TerminalSettings";
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "__TerminalSettings_Phase6"
                RENAME TO "TerminalSettings";
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX
                    "IX_TerminalSettings_TerminalNo"
                ON "TerminalSettings" ("TerminalNo");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX
                    "IX_TerminalSettings_MachineName"
                ON "TerminalSettings" ("MachineName");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX
                    "IX_TerminalSettings_IsActive"
                ON "TerminalSettings" ("IsActive");
                """);
        }

        private static string EscapeSqlLiteral(
            string value)
        {
            return (value ?? string.Empty)
                .Replace(
                    "'",
                    "''",
                    StringComparison.Ordinal);
        }
    }
}

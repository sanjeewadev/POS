using System;
using POS.Core.Configuration;

namespace POS.Core.Models
{
    public class TerminalSettings
    {
        public int Id { get; set; }

        // =========================================================
        // TERMINAL IDENTITY
        // =========================================================

        public string TerminalNo { get; set; } =
            TerminalConfigurationDefaults.InitialTerminalNumber;

        public string TerminalName { get; set; } =
            TerminalConfigurationDefaults.BuildTerminalName(
                TerminalConfigurationDefaults.InitialTerminalNumber);

        public string MachineName { get; set; } = string.Empty;

        public string Location { get; set; } =
            TerminalConfigurationDefaults.DefaultLocation;

        // =========================================================
        // RECEIPT PRINTER
        // =========================================================

        public string PrinterMode { get; set; } =
            TerminalConfigurationDefaults.PrinterMode;

        public string ReceiptPrinterName { get; set; } = string.Empty;

        public int ReceiptPaperWidth { get; set; } =
            TerminalConfigurationDefaults.ReceiptPaperWidth;

        public bool AutoPrintReceipt { get; set; } = false;

        public int ReceiptCopies { get; set; } =
            TerminalConfigurationDefaults.ReceiptCopies;

        // =========================================================
        // CASH DRAWER
        // =========================================================

        public bool EnableCashDrawer { get; set; } = false;

        public string DrawerKickCode { get; set; } =
            TerminalConfigurationDefaults.DrawerKickCode;

        public bool OpenDrawerAfterCashSale { get; set; } = false;

        // =========================================================
        // BARCODE SCANNER
        // =========================================================

        public string ScannerSuffixAction { get; set; } = "Enter";

        // =========================================================
        // WEIGHING SCALE
        // =========================================================

        public bool EnableScale { get; set; } = false;

        public string ScaleComPort { get; set; } = "COM1";

        public int ScaleBaudRate { get; set; } = 9600;

        // =========================================================
        // CUSTOMER POLE DISPLAY
        // =========================================================

        public bool EnablePoleDisplay { get; set; } = false;

        public string PoleDisplayComPort { get; set; } = "COM2";

        public string PoleWelcomeMessage { get; set; } = "WELCOME";

        // =========================================================
        // EFTPOS / CARD TERMINAL
        // =========================================================

        public bool EnableEftpos { get; set; } = false;

        public string EftposProvider { get; set; } = string.Empty;

        public string EftposPortOrIp { get; set; } = string.Empty;

        // =========================================================
        // CASHIER SECURITY
        // =========================================================

        /// <summary>
        /// Minutes of inactivity before Cashier locks.
        /// 0 disables automatic locking. Manual lock remains available.
        /// </summary>
        public int AutoLockTimeoutMinutes { get; set; } =
            TerminalConfigurationDefaults.AutoLockTimeoutMinutes;

        // =========================================================
        // SYSTEM / AUDIT
        // =========================================================

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;
    }
}
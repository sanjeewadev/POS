using System;

namespace POS.Core.Models
{
    public class TerminalSettings
    {
        public int Id { get; set; }

        // =========================================================
        // TERMINAL IDENTITY
        // =========================================================

        public string TerminalNo { get; set; } = "01";

        public string TerminalName { get; set; } = "Cashier Terminal 01";

        public string MachineName { get; set; } = string.Empty;

        public string Location { get; set; } = "Main Store";

        // =========================================================
        // RECEIPT PRINTER
        // =========================================================

        public string PrinterMode { get; set; } = "WindowsSpooler";

        public string ReceiptPrinterName { get; set; } = "POS-80";

        public int ReceiptPaperWidth { get; set; } = 80;

        public bool AutoPrintReceipt { get; set; } = true;

        public int ReceiptCopies { get; set; } = 1;

        // =========================================================
        // CASH DRAWER
        // =========================================================

        public bool EnableCashDrawer { get; set; } = true;

        public string DrawerKickCode { get; set; } = "27,112,0,25,250";

        public bool OpenDrawerAfterCashSale { get; set; } = true;

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
        // SYSTEM / AUDIT
        // =========================================================

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;
    }
}
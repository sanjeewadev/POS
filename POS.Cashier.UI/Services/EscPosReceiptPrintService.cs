using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using POS.Core.Models;

namespace POS.Cashier.UI.Services
{
    public class EscPosReceiptPrintService : IReceiptPrintService
    {
        // ESC/POS commands
        private readonly byte[] ESC_INIT = { 27, 64 };
        private readonly byte[] ALIGN_LEFT = { 27, 97, 0 };
        private readonly byte[] ALIGN_CENTER = { 27, 97, 1 };
        private readonly byte[] ALIGN_RIGHT = { 27, 97, 2 };
        private readonly byte[] BOLD_ON = { 27, 69, 1 };
        private readonly byte[] BOLD_OFF = { 27, 69, 0 };
        private readonly byte[] PAPER_CUT = { 29, 86, 66, 0 };

        // Standard RJ11 cash drawer kick command
        private readonly byte[] DRAWER_KICK = { 27, 112, 0, 25, 250 };

        public Task PrintReceiptAsync(SalesHeader transaction, string printerName)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            return Task.Run(() =>
            {
                var bytes = new List<byte>();

                bytes.AddRange(ESC_INIT);

                // Header
                bytes.AddRange(ALIGN_CENTER);
                bytes.AddRange(BOLD_ON);
                AddText(bytes, "BANDULA TRADE CENTER\n");
                bytes.AddRange(BOLD_OFF);
                AddText(bytes, "No 123, Main Street\n");
                AddText(bytes, "Tel: 011-1234567\n\n");

                // Transaction meta
                bytes.AddRange(ALIGN_LEFT);
                AddText(bytes, $"Inv No : {SafeText(transaction.InvoiceNo, 24)}\n");
                AddText(bytes, $"Date   : {transaction.TransactionDate:yyyy-MM-dd HH:mm}\n");
                AddText(bytes, $"Cashier: {SafeText(transaction.CashierName, 24)}\n");
                AddText(bytes, "--------------------------------\n");

                // Line items
                AddText(bytes, "Item            Qty   Price   Total\n");
                AddText(bytes, "--------------------------------\n");

                if (transaction.SalesLines != null)
                {
                    foreach (var line in transaction.SalesLines)
                    {
                        string itemName = SafeText(line.ItemDescription, 15).PadRight(15);
                        string qty = line.Quantity.ToString("0.##").PadLeft(3);
                        string price = line.UnitPrice.ToString("0.00").PadLeft(7);
                        string total = line.LineTotal.ToString("0.00").PadLeft(7);

                        AddText(bytes, $"{itemName} {qty} {price} {total}\n");
                    }
                }

                AddText(bytes, "--------------------------------\n");

                // Totals
                bytes.AddRange(ALIGN_RIGHT);
                AddText(bytes, $"Gross Total: {transaction.GrossTotal,8:0.00}\n");

                if (transaction.TotalDiscount > 0m)
                    AddText(bytes, $"Discount   : {transaction.TotalDiscount,8:0.00}\n");

                bytes.AddRange(BOLD_ON);
                AddText(bytes, $"NET TOTAL  : {transaction.NetTotal,8:0.00}\n");
                bytes.AddRange(BOLD_OFF);
                AddText(bytes, "\n");

                AddText(bytes, $"Tendered ({SafeText(transaction.PaymentMethod, 10)}): {transaction.AmountTendered,8:0.00}\n");
                AddText(bytes, $"Change: {transaction.BalanceReturned,8:0.00}\n");

                // Footer
                bytes.AddRange(ALIGN_CENTER);
                AddText(bytes, "\nThank You! Come Again.\n\n\n\n\n");
                bytes.AddRange(PAPER_CUT);

                // Drawer kick only for cash sale
                if (string.Equals(transaction.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase))
                    bytes.AddRange(DRAWER_KICK);

                RawPrinterHelper.SendBytesToPrinter(printerName, bytes.ToArray(), "POS Receipt");
            });
        }

        public Task PrintQuotationAsync(QuotationPrintRequest request, string printerName)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Lines == null || request.Lines.Count == 0)
                throw new InvalidOperationException("Cannot print quotation because cart is empty.");

            return Task.Run(() =>
            {
                var bytes = new List<byte>();

                bytes.AddRange(ESC_INIT);

                // Header
                bytes.AddRange(ALIGN_CENTER);
                bytes.AddRange(BOLD_ON);
                AddText(bytes, "BANDULA TRADE CENTER\n");
                bytes.AddRange(BOLD_OFF);
                AddText(bytes, "No 123, Main Street\n");
                AddText(bytes, "Tel: 011-1234567\n\n");

                bytes.AddRange(BOLD_ON);
                AddText(bytes, "PRICE QUOTATION\n");
                bytes.AddRange(BOLD_OFF);
                AddText(bytes, "NOT A TAX INVOICE\n");
                AddText(bytes, "NO STOCK RESERVED\n\n");

                // Meta
                bytes.AddRange(ALIGN_LEFT);
                AddText(bytes, $"Quote No : {SafeText(request.QuotationNo, 24)}\n");
                AddText(bytes, $"Date     : {request.QuotationDate:yyyy-MM-dd HH:mm}\n");
                AddText(bytes, $"Cashier  : {SafeText(request.CashierName, 24)}\n");
                AddText(bytes, $"Terminal : {SafeText(request.TerminalNo, 10)}\n");
                AddText(bytes, $"Customer : {SafeText(request.CustomerName, 24)}\n");
                AddText(bytes, "--------------------------------\n");

                // Items
                foreach (var line in request.Lines)
                {
                    string itemName = SafeText(line.ItemDescription, 30);

                    AddText(bytes, $"{line.LineNo}. {itemName}\n");

                    if (!string.IsNullOrWhiteSpace(line.Barcode))
                        AddText(bytes, $"   Code : {SafeText(line.Barcode, 24)}\n");

                    AddText(bytes, $"   Qty  : {line.Quantity:0.###} {SafeText(line.Uom, 6)}\n");
                    AddText(bytes, $"   Price: {line.UnitPrice,10:0.00}\n");

                    if (line.DiscountAmount > 0m)
                        AddText(bytes, $"   Disc : {line.DiscountAmount,10:0.00}\n");

                    AddText(bytes, $"   Total: {line.LineTotal,10:0.00}\n");
                    AddText(bytes, "--------------------------------\n");
                }

                // Totals
                bytes.AddRange(ALIGN_RIGHT);
                AddText(bytes, $"Gross Total: {request.GrossTotal,10:0.00}\n");

                if (request.TotalDiscount > 0m)
                    AddText(bytes, $"Discount   : {request.TotalDiscount,10:0.00}\n");

                bytes.AddRange(BOLD_ON);
                AddText(bytes, $"NET TOTAL  : {request.NetTotal,10:0.00}\n");
                bytes.AddRange(BOLD_OFF);

                // Footer
                bytes.AddRange(ALIGN_CENTER);
                AddText(bytes, "\nPrices may change.\n");
                AddText(bytes, "Final stock availability must be\n");
                AddText(bytes, "confirmed at billing time.\n");
                AddText(bytes, "\nThank You.\n\n\n\n");

                bytes.AddRange(PAPER_CUT);

                // Important:
                // Quotation must NOT kick drawer.
                bool printed = RawPrinterHelper.SendBytesToPrinter(
                    printerName,
                    bytes.ToArray(),
                    "POS Quotation");

                if (!printed)
                    throw new InvalidOperationException($"Quotation print failed. Printer not available: {printerName}");
            });
        }

        public Task OpenCashDrawerAsync(string printerName)
        {
            return Task.Run(() =>
            {
                RawPrinterHelper.SendBytesToPrinter(printerName, DRAWER_KICK, "POS Drawer Kick");
            });
        }

        private static void AddText(List<byte> bytes, string text)
        {
            bytes.AddRange(Encoding.ASCII.GetBytes(text ?? string.Empty));
        }

        private static string SafeText(string? value, int maxLength)
        {
            string text = (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text.Length <= maxLength
                ? text
                : text.Substring(0, maxLength);
        }
    }

    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDocName = null!;

            [MarshalAs(UnmanagedType.LPStr)]
            public string pOutputFile = null!;

            [MarshalAs(UnmanagedType.LPStr)]
            public string pDataType = null!;
        }

        [DllImport("winspool.Drv",
            EntryPoint = "OpenPrinterA",
            SetLastError = true,
            CharSet = CharSet.Ansi,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter(
            [MarshalAs(UnmanagedType.LPStr)] string szPrinter,
            out IntPtr hPrinter,
            IntPtr pd);

        [DllImport("winspool.Drv",
            EntryPoint = "ClosePrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv",
            EntryPoint = "StartDocPrinterA",
            SetLastError = true,
            CharSet = CharSet.Ansi,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(
            IntPtr hPrinter,
            int level,
            [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv",
            EntryPoint = "EndDocPrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv",
            EntryPoint = "StartPagePrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv",
            EntryPoint = "EndPagePrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv",
            EntryPoint = "WritePrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(
            IntPtr hPrinter,
            IntPtr pBytes,
            int dwCount,
            out int dwWritten);

        public static bool SendBytesToPrinter(string printerName, byte[] data, string documentName = "POS Raw Print")
        {
            if (string.IsNullOrWhiteSpace(printerName))
                return false;

            if (data == null || data.Length == 0)
                return false;

            IntPtr unmanagedBytes = IntPtr.Zero;
            IntPtr printerHandle = IntPtr.Zero;

            try
            {
                unmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, unmanagedBytes, data.Length);

                if (!OpenPrinter(printerName.Normalize(), out printerHandle, IntPtr.Zero))
                    return false;

                var docInfo = new DOCINFOA
                {
                    pDocName = string.IsNullOrWhiteSpace(documentName)
                        ? "POS Raw Print"
                        : documentName,
                    pDataType = "RAW"
                };

                if (!StartDocPrinter(printerHandle, 1, docInfo))
                    return false;

                try
                {
                    if (!StartPagePrinter(printerHandle))
                        return false;

                    try
                    {
                        bool success = WritePrinter(
                            printerHandle,
                            unmanagedBytes,
                            data.Length,
                            out int written);

                        return success && written == data.Length;
                    }
                    finally
                    {
                        EndPagePrinter(printerHandle);
                    }
                }
                finally
                {
                    EndDocPrinter(printerHandle);
                }
            }
            finally
            {
                if (printerHandle != IntPtr.Zero)
                    ClosePrinter(printerHandle);

                if (unmanagedBytes != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(unmanagedBytes);
            }
        }
    }
}
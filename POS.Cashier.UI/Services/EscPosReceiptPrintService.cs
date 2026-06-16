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
        // --- ESC/POS HEX/BYTE COMMANDS ---
        private readonly byte[] ESC_INIT = { 27, 64 }; // Initialize Printer
        private readonly byte[] ALIGN_LEFT = { 27, 97, 0 };
        private readonly byte[] ALIGN_CENTER = { 27, 97, 1 };
        private readonly byte[] ALIGN_RIGHT = { 27, 97, 2 };
        private readonly byte[] BOLD_ON = { 27, 69, 1 };
        private readonly byte[] BOLD_OFF = { 27, 69, 0 };
        private readonly byte[] PAPER_CUT = { 29, 86, 66, 0 }; // Full Cut

        // Pin 2 Drawer Kick: 27 112 0 25 250 (Universal standard for RJ11 cash drawers)
        private readonly byte[] DRAWER_KICK = { 27, 112, 0, 25, 250 };

        public Task PrintReceiptAsync(SalesHeader transaction, string printerName)
        {
            return Task.Run(() =>
            {
                var bytes = new List<byte>();

                // 1. Initialize Printer
                bytes.AddRange(ESC_INIT);

                // 2. Header (Centered & Bold)
                bytes.AddRange(ALIGN_CENTER);
                bytes.AddRange(BOLD_ON);
                bytes.AddRange(Encoding.ASCII.GetBytes("BANDULA TRADE CENTER\n"));
                bytes.AddRange(BOLD_OFF);
                bytes.AddRange(Encoding.ASCII.GetBytes("No 123, Main Street\nTel: 011-1234567\n\n"));

                // 3. Transaction Meta (Left Aligned)
                bytes.AddRange(ALIGN_LEFT);
                bytes.AddRange(Encoding.ASCII.GetBytes($"Inv No : {transaction.InvoiceNo}\n"));
                bytes.AddRange(Encoding.ASCII.GetBytes($"Date   : {transaction.TransactionDate:yyyy-MM-dd HH:mm}\n"));
                bytes.AddRange(Encoding.ASCII.GetBytes($"Cashier: {transaction.CashierName}\n"));
                bytes.AddRange(Encoding.ASCII.GetBytes("--------------------------------\n"));

                // 4. Line Items (Fixed Width formatting for standard 80mm thermal paper)
                bytes.AddRange(Encoding.ASCII.GetBytes("Item            Qty   Price   Total\n"));
                bytes.AddRange(Encoding.ASCII.GetBytes("--------------------------------\n"));

                if (transaction.SalesLines != null)
                {
                    foreach (var line in transaction.SalesLines)
                    {
                        // Truncate or pad strings to keep columns perfectly aligned
                        string itemName = line.ItemDescription.Length > 15 ? line.ItemDescription.Substring(0, 15) : line.ItemDescription.PadRight(15);
                        string qty = line.Quantity.ToString("0.##").PadLeft(3);
                        string price = line.UnitPrice.ToString("0.00").PadLeft(7);
                        string total = line.LineTotal.ToString("0.00").PadLeft(7);

                        bytes.AddRange(Encoding.ASCII.GetBytes($"{itemName} {qty} {price} {total}\n"));
                    }
                }

                bytes.AddRange(Encoding.ASCII.GetBytes("--------------------------------\n"));

                // 5. Financial Totals (Right Aligned)
                bytes.AddRange(ALIGN_RIGHT);
                bytes.AddRange(Encoding.ASCII.GetBytes($"Gross Total: {transaction.GrossTotal,8:0.00}\n"));

                if (transaction.TotalDiscount > 0)
                {
                    bytes.AddRange(Encoding.ASCII.GetBytes($"Discount: {transaction.TotalDiscount,8:0.00}\n"));
                }

                bytes.AddRange(BOLD_ON);
                bytes.AddRange(Encoding.ASCII.GetBytes($"NET TOTAL: {transaction.NetTotal,8:0.00}\n"));
                bytes.AddRange(BOLD_OFF);
                bytes.AddRange(Encoding.ASCII.GetBytes("\n"));

                bytes.AddRange(Encoding.ASCII.GetBytes($"Tendered ({transaction.PaymentMethod}): {transaction.AmountTendered,8:0.00}\n"));
                bytes.AddRange(Encoding.ASCII.GetBytes($"Change: {transaction.BalanceReturned,8:0.00}\n"));

                // 6. Footer & Cut
                bytes.AddRange(ALIGN_CENTER);
                bytes.AddRange(Encoding.ASCII.GetBytes("\nThank You! Come Again.\n\n\n\n\n")); // Feed paper past tear-bar
                bytes.AddRange(PAPER_CUT);

                // 7. Cash Drawer Kick (Only kick if it's a cash transaction!)
                if (transaction.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                {
                    bytes.AddRange(DRAWER_KICK);
                }

                // 8. Fire the raw bytes directly to the hardware
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes.ToArray());
            });
        }

        public Task OpenCashDrawerAsync(string printerName)
        {
            return Task.Run(() =>
            {
                RawPrinterHelper.SendBytesToPrinter(printerName, DRAWER_KICK);
            });
        }
    }

    // =========================================================================
    // WINDOWS NATIVE API WRAPPER FOR RAW THERMAL PRINTING
    // Bypasses the WPF Print Spooler UI for instant zero-latency printing
    // =========================================================================
    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName = null!;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile = null!;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType = null!;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool SendBytesToPrinter(string szPrinterName, byte[] data)
        {
            IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
            Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
            bool bSuccess = false;

            try
            {
                var di = new DOCINFOA { pDocName = "POS Receipt", pDataType = "RAW" };
                if (OpenPrinter(szPrinterName.Normalize(), out IntPtr hPrinter, IntPtr.Zero))
                {
                    if (StartDocPrinter(hPrinter, 1, di))
                    {
                        if (StartPagePrinter(hPrinter))
                        {
                            bSuccess = WritePrinter(hPrinter, pUnmanagedBytes, data.Length, out _);
                            EndPagePrinter(hPrinter);
                        }
                        EndDocPrinter(hPrinter);
                    }
                    ClosePrinter(hPrinter);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pUnmanagedBytes);
            }
            return bSuccess;
        }
    }
}
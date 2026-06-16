using POS.Core.Interfaces;
using POS.Core.Models;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace POS.Hardware.Services
{
    public class ReceiptPrinterService : IReceiptPrinterService
    {
        // Change this to match your installed Windows printer name (e.g., "EPSON TM-T88V")
        private readonly string _printerName = "POS_Printer";

        public void PrintCashVoucher(CashMovement movement)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = _printerName;

            pd.PrintPage += (sender, e) =>
            {
                Graphics g = e.Graphics!;
                Font headerFont = new Font("Courier New", 14, FontStyle.Bold);
                Font bodyFont = new Font("Courier New", 10, FontStyle.Regular);
                Font boldFont = new Font("Courier New", 10, FontStyle.Bold);

                int y = 10;
                int lineOffset = 20;

                g.DrawString("BANDULA TRADE CENTER", headerFont, Brushes.Black, 10, y);
                y += lineOffset * 2;

                g.DrawString($"--- {movement.MovementType.ToUpper()} VOUCHER ---", boldFont, Brushes.Black, 10, y);
                y += lineOffset;

                g.DrawString($"Date   : {movement.Timestamp:yyyy-MM-dd HH:mm}", bodyFont, Brushes.Black, 10, y);
                y += lineOffset;
                g.DrawString($"Voucher: {movement.ReferenceVoucherNo}", bodyFont, Brushes.Black, 10, y);
                y += lineOffset;
                g.DrawString($"User   : {movement.CashierName}", bodyFont, Brushes.Black, 10, y);
                y += lineOffset * 2;

                g.DrawString($"Category: {movement.ReasonCategory}", bodyFont, Brushes.Black, 10, y);
                y += lineOffset;
                g.DrawString($"Amount  : Rs. {movement.Amount:N2}", boldFont, Brushes.Black, 10, y);
                y += lineOffset * 2;

                if (!string.IsNullOrWhiteSpace(movement.AuthorizedBy) && movement.AuthorizedBy != movement.CashierName)
                {
                    g.DrawString($"Auth By : {movement.AuthorizedBy}", bodyFont, Brushes.Black, 10, y);
                    y += lineOffset;
                }

                g.DrawString("_______________________", bodyFont, Brushes.Black, 10, y);
                y += lineOffset;
                g.DrawString("Manager Signature", bodyFont, Brushes.Black, 10, y);
            };

            try
            {
                pd.Print();
            }
            catch (Exception)
            {
                // In production, log this silently or notify the UI that the printer is offline
            }
        }

        public void OpenCashDrawer()
        {
            // Standard EPSON/Star Micronics ESC/POS Kick Code
            string drawerKickCode = "\x1B\x70\x00\x19\xFA";
            RawPrinterHelper.SendStringToPrinter(_printerName, drawerKickCode);
        }
    }

    // Standard Windows API wrapper to send raw byte strings directly to the printer port
    public static class RawPrinterHelper
    {
        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        public static bool SendStringToPrinter(string szPrinterName, string szString)
        {
            IntPtr pBytes;
            Int32 dwCount = szString.Length;
            pBytes = Marshal.StringToCoTaskMemAnsi(szString);
            bool bSuccess = SendBytesToPrinter(szPrinterName, pBytes, dwCount);
            Marshal.FreeCoTaskMem(pBytes);
            return bSuccess;
        }

        private static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, Int32 dwCount)
        {
            Int32 dwError = 0, dwWritten = 0;
            IntPtr hPrinter = new IntPtr(0);
            DOCINFOA di = new DOCINFOA { pDocName = "Cash Drawer Kick", pDataType = "RAW" };
            bool bSuccess = false;

            if (OpenPrinter(szPrinterName, out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            return bSuccess;
        }
    }
}
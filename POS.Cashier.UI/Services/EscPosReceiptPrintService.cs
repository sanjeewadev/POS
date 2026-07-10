using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using POS.Core.Models;

namespace POS.Cashier.UI.Services
{
    public sealed class EscPosReceiptPrintService :
        IReceiptPrintService
    {
        private static readonly byte[] EscInitialize =
        {
            27, 64
        };

        private static readonly byte[] AlignLeft =
        {
            27, 97, 0
        };

        private static readonly byte[] AlignCenter =
        {
            27, 97, 1
        };

        private static readonly byte[] AlignRight =
        {
            27, 97, 2
        };

        private static readonly byte[] BoldOn =
        {
            27, 69, 1
        };

        private static readonly byte[] BoldOff =
        {
            27, 69, 0
        };

        private static readonly byte[] PaperCut =
        {
            29, 86, 66, 0
        };

        private static readonly byte[] DrawerKick =
        {
            27, 112, 0, 25, 250
        };

        public Task PrintReceiptAsync(
            SalesHeader transaction,
            string printerName,
            int paperWidth)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(
                    nameof(transaction));
            }

            ValidatePrinterName(printerName);

            int columns =
                GetColumns(paperWidth);

            return Task.Run(() =>
            {
                var bytes = new List<byte>();

                bytes.AddRange(EscInitialize);

                bytes.AddRange(AlignCenter);
                bytes.AddRange(BoldOn);
                AddText(
                    bytes,
                    "BANDULA TRADE CENTER\n");
                bytes.AddRange(BoldOff);
                AddText(
                    bytes,
                    "No 123, Main Street\n");
                AddText(
                    bytes,
                    "Tel: 011-1234567\n\n");

                bytes.AddRange(AlignLeft);
                AddText(
                    bytes,
                    $"Inv No : " +
                    $"{SafeText(transaction.InvoiceNo, columns - 9)}\n");

                AddText(
                    bytes,
                    $"Date   : " +
                    $"{transaction.TransactionDate:yyyy-MM-dd HH:mm}\n");

                AddText(
                    bytes,
                    $"Cashier: " +
                    $"{SafeText(transaction.CashierName, columns - 9)}\n");

                AddSeparator(
                    bytes,
                    columns);

                if (transaction.SalesLines != null)
                {
                    foreach (SalesLine line in
                             transaction.SalesLines)
                    {
                        AddText(
                            bytes,
                            SafeText(
                                line.ItemDescription,
                                columns) +
                            "\n");

                        string quantityAndPrice =
                            $"{line.Quantity:0.###} x " +
                            $"{line.UnitPrice:0.00}";

                        string total =
                            line.LineTotal
                                .ToString("0.00");

                        AddText(
                            bytes,
                            BuildTwoColumnLine(
                                quantityAndPrice,
                                total,
                                columns) +
                            "\n");
                    }
                }

                AddSeparator(
                    bytes,
                    columns);

                bytes.AddRange(AlignRight);

                AddText(
                    bytes,
                    $"Gross Total: " +
                    $"{transaction.GrossTotal:0.00}\n");

                if (transaction.TotalDiscount > 0m)
                {
                    AddText(
                        bytes,
                        $"Discount   : " +
                        $"{transaction.TotalDiscount:0.00}\n");
                }

                bytes.AddRange(BoldOn);

                AddText(
                    bytes,
                    $"NET TOTAL  : " +
                    $"{transaction.NetTotal:0.00}\n");

                bytes.AddRange(BoldOff);

                AddText(
                    bytes,
                    $"Tendered (" +
                    $"{SafeText(transaction.PaymentMethod, 10)}): " +
                    $"{transaction.AmountTendered:0.00}\n");

                AddText(
                    bytes,
                    $"Change     : " +
                    $"{transaction.BalanceReturned:0.00}\n");

                bytes.AddRange(AlignCenter);

                AddText(
                    bytes,
                    "\nThank You! Come Again.\n\n\n");

                bytes.AddRange(PaperCut);

                bool printed =
                    RawPrinterHelper
                        .SendBytesToPrinter(
                            printerName,
                            bytes.ToArray(),
                            "POS Receipt");

                if (!printed)
                {
                    throw new InvalidOperationException(
                        $"Receipt print failed. " +
                        $"Printer not available: " +
                        $"{printerName}");
                }
            });
        }

        public Task PrintQuotationAsync(
            QuotationPrintRequest request,
            string printerName,
            int paperWidth)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    nameof(request));
            }

            if (request.Lines == null ||
                request.Lines.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot print quotation because the cart is empty.");
            }

            ValidatePrinterName(printerName);

            int columns =
                GetColumns(paperWidth);

            return Task.Run(() =>
            {
                var bytes = new List<byte>();

                bytes.AddRange(EscInitialize);

                bytes.AddRange(AlignCenter);
                bytes.AddRange(BoldOn);
                AddText(
                    bytes,
                    "BANDULA TRADE CENTER\n");
                bytes.AddRange(BoldOff);
                AddText(
                    bytes,
                    "No 123, Main Street\n");
                AddText(
                    bytes,
                    "Tel: 011-1234567\n\n");

                bytes.AddRange(BoldOn);
                AddText(
                    bytes,
                    "PRICE QUOTATION\n");
                bytes.AddRange(BoldOff);

                AddText(
                    bytes,
                    "NOT A TAX INVOICE\n");
                AddText(
                    bytes,
                    "NO STOCK RESERVED\n\n");

                bytes.AddRange(AlignLeft);

                AddText(
                    bytes,
                    $"Quote No : " +
                    $"{SafeText(request.QuotationNo, columns - 11)}\n");

                AddText(
                    bytes,
                    $"Date     : " +
                    $"{request.QuotationDate:yyyy-MM-dd HH:mm}\n");

                AddText(
                    bytes,
                    $"Cashier  : " +
                    $"{SafeText(request.CashierName, columns - 11)}\n");

                AddText(
                    bytes,
                    $"Terminal : " +
                    $"{SafeText(request.TerminalNo, columns - 11)}\n");

                AddText(
                    bytes,
                    $"Customer : " +
                    $"{SafeText(request.CustomerName, columns - 11)}\n");

                AddSeparator(
                    bytes,
                    columns);

                foreach (QuotationPrintLine line in
                         request.Lines)
                {
                    AddText(
                        bytes,
                        $"{line.LineNo}. " +
                        $"{SafeText(line.ItemDescription, columns - 3)}\n");

                    if (!string.IsNullOrWhiteSpace(
                            line.Barcode))
                    {
                        AddText(
                            bytes,
                            $"   Code : " +
                            $"{SafeText(line.Barcode, columns - 10)}\n");
                    }

                    string quantityAndPrice =
                        $"{line.Quantity:0.###} " +
                        $"{SafeText(line.Uom, 6)} x " +
                        $"{line.UnitPrice:0.00}";

                    string total =
                        line.LineTotal
                            .ToString("0.00");

                    AddText(
                        bytes,
                        BuildTwoColumnLine(
                            quantityAndPrice,
                            total,
                            columns) +
                        "\n");

                    if (line.DiscountAmount > 0m)
                    {
                        AddText(
                            bytes,
                            BuildTwoColumnLine(
                                "Discount",
                                line.DiscountAmount
                                    .ToString("0.00"),
                                columns) +
                            "\n");
                    }

                    AddSeparator(
                        bytes,
                        columns);
                }

                bytes.AddRange(AlignRight);

                AddText(
                    bytes,
                    $"Gross Total: " +
                    $"{request.GrossTotal:0.00}\n");

                if (request.TotalDiscount > 0m)
                {
                    AddText(
                        bytes,
                        $"Discount   : " +
                        $"{request.TotalDiscount:0.00}\n");
                }

                bytes.AddRange(BoldOn);

                AddText(
                    bytes,
                    $"NET TOTAL  : " +
                    $"{request.NetTotal:0.00}\n");

                bytes.AddRange(BoldOff);
                bytes.AddRange(AlignCenter);

                AddText(
                    bytes,
                    "\nPrices may change.\n");
                AddText(
                    bytes,
                    "Final stock availability must be confirmed at billing time.\n");
                AddText(
                    bytes,
                    "\nThank You.\n\n\n");

                bytes.AddRange(PaperCut);

                bool printed =
                    RawPrinterHelper
                        .SendBytesToPrinter(
                            printerName,
                            bytes.ToArray(),
                            "POS Quotation");

                if (!printed)
                {
                    throw new InvalidOperationException(
                        $"Quotation print failed. " +
                        $"Printer not available: " +
                        $"{printerName}");
                }
            });
        }

        public Task OpenCashDrawerAsync(
            string printerName)
        {
            ValidatePrinterName(printerName);

            return Task.Run(() =>
            {
                bool opened =
                    RawPrinterHelper
                        .SendBytesToPrinter(
                            printerName,
                            DrawerKick,
                            "POS Drawer Kick");

                if (!opened)
                {
                    throw new InvalidOperationException(
                        $"Cash drawer command failed. " +
                        $"Printer not available: " +
                        $"{printerName}");
                }
            });
        }

        private static int GetColumns(
            int paperWidth)
        {
            return paperWidth == 58
                ? 32
                : 42;
        }

        private static void ValidatePrinterName(
            string? printerName)
        {
            if (string.IsNullOrWhiteSpace(
                    printerName))
            {
                throw new InvalidOperationException(
                    "Receipt printer is not configured.");
            }
        }

        private static void AddSeparator(
            List<byte> bytes,
            int columns)
        {
            AddText(
                bytes,
                new string('-', columns) +
                "\n");
        }

        private static void AddText(
            List<byte> bytes,
            string text)
        {
            bytes.AddRange(
                Encoding.ASCII.GetBytes(
                    text ?? string.Empty));
        }

        private static string BuildTwoColumnLine(
            string left,
            string right,
            int columns)
        {
            string safeLeft =
                SafeText(
                    left,
                    columns);

            string safeRight =
                SafeText(
                    right,
                    columns);

            int spaces =
                columns -
                safeLeft.Length -
                safeRight.Length;

            if (spaces < 1)
            {
                int allowedLeft =
                    Math.Max(
                        0,
                        columns -
                        safeRight.Length -
                        1);

                safeLeft =
                    SafeText(
                        safeLeft,
                        allowedLeft);

                spaces = 1;
            }

            return safeLeft +
                   new string(' ', spaces) +
                   safeRight;
        }

        private static string SafeText(
            string? value,
            int maxLength)
        {
            string text =
                (value ?? string.Empty)
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Trim();

            if (maxLength <= 0)
                return string.Empty;

            return text.Length <= maxLength
                ? text
                : text.Substring(
                    0,
                    maxLength);
        }
    }

    public static class RawPrinterHelper
    {
        [StructLayout(
            LayoutKind.Sequential,
            CharSet = CharSet.Unicode)]
        private sealed class DocInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string DocumentName =
                string.Empty;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string? OutputFile;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string DataType =
                "RAW";
        }

        [DllImport(
            "winspool.Drv",
            EntryPoint = "OpenPrinterW",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        private static extern bool OpenPrinter(
            [MarshalAs(UnmanagedType.LPWStr)]
            string printerName,
            out IntPtr printerHandle,
            IntPtr defaults);

        [DllImport(
            "winspool.Drv",
            EntryPoint = "ClosePrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        private static extern bool ClosePrinter(
            IntPtr printerHandle);

        [DllImport(
            "winspool.Drv",
            EntryPoint = "StartDocPrinterW",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(
            IntPtr printerHandle,
            int level,
            [In, MarshalAs(
                UnmanagedType.LPStruct)]
            DocInfo documentInfo);

        [DllImport(
            "winspool.Drv",
            EntryPoint = "EndDocPrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(
            IntPtr printerHandle);

        [DllImport(
            "winspool.Drv",
            EntryPoint = "StartPagePrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(
            IntPtr printerHandle);

        [DllImport(
            "winspool.Drv",
            EntryPoint = "EndPagePrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(
            IntPtr printerHandle);

        [DllImport(
            "winspool.Drv",
            EntryPoint = "WritePrinter",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        private static extern bool WritePrinter(
            IntPtr printerHandle,
            IntPtr bytes,
            int byteCount,
            out int bytesWritten);

        public static bool SendBytesToPrinter(
            string printerName,
            byte[] data,
            string documentName =
                "POS Raw Print")
        {
            if (string.IsNullOrWhiteSpace(
                    printerName))
            {
                return false;
            }

            if (data == null ||
                data.Length == 0)
            {
                return false;
            }

            IntPtr unmanagedBytes =
                IntPtr.Zero;

            IntPtr printerHandle =
                IntPtr.Zero;

            try
            {
                unmanagedBytes =
                    Marshal.AllocCoTaskMem(
                        data.Length);

                Marshal.Copy(
                    data,
                    0,
                    unmanagedBytes,
                    data.Length);

                if (!OpenPrinter(
                        printerName.Trim(),
                        out printerHandle,
                        IntPtr.Zero))
                {
                    return false;
                }

                var documentInfo =
                    new DocInfo
                    {
                        DocumentName =
                            string.IsNullOrWhiteSpace(
                                documentName)
                                ? "POS Raw Print"
                                : documentName.Trim()
                    };

                if (!StartDocPrinter(
                        printerHandle,
                        1,
                        documentInfo))
                {
                    return false;
                }

                try
                {
                    if (!StartPagePrinter(
                            printerHandle))
                    {
                        return false;
                    }

                    try
                    {
                        bool success =
                            WritePrinter(
                                printerHandle,
                                unmanagedBytes,
                                data.Length,
                                out int bytesWritten);

                        return success &&
                               bytesWritten ==
                               data.Length;
                    }
                    finally
                    {
                        EndPagePrinter(
                            printerHandle);
                    }
                }
                finally
                {
                    EndDocPrinter(
                        printerHandle);
                }
            }
            finally
            {
                if (printerHandle !=
                    IntPtr.Zero)
                {
                    ClosePrinter(
                        printerHandle);
                }

                if (unmanagedBytes !=
                    IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(
                        unmanagedBytes);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services.Documents;
using POS.Core.Utilities;

namespace POS.Cashier.UI.Services
{
    public sealed class EscPosReceiptPrintService :
        IReceiptPrintService
    {
        private readonly StoreSettingsRepository
            _storeSettingsRepository;

        private readonly SalesDocumentTextFormatter
            _salesDocumentFormatter;

        private readonly CustomerCreditNoteTextFormatter
            _customerCreditNoteFormatter;

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

        public EscPosReceiptPrintService(
            StoreSettingsRepository storeSettingsRepository,
            SalesDocumentTextFormatter salesDocumentFormatter,
            CustomerCreditNoteTextFormatter customerCreditNoteFormatter)
        {
            _storeSettingsRepository =
                storeSettingsRepository;
            _salesDocumentFormatter =
                salesDocumentFormatter;
            _customerCreditNoteFormatter =
                customerCreditNoteFormatter;
        }

        public async Task<string> BuildReceiptPreviewAsync(
            SalesHeader transaction,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            StoreSettings storeSettings =
                await _storeSettingsRepository
                    .GetOrCreateDefaultAsync();

            return _salesDocumentFormatter.FormatReceipt(
                transaction,
                storeSettings,
                paperWidth,
                copyLabel);
        }

        public async Task<string> BuildTaxInvoicePreviewAsync(
            SalesHeader transaction,
            DateTime issuedAtUtc,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            StoreSettings storeSettings =
                await _storeSettingsRepository
                    .GetOrCreateDefaultAsync();

            return _salesDocumentFormatter.FormatTaxInvoice(
                transaction,
                storeSettings,
                issuedAtUtc,
                paperWidth,
                copyLabel);
        }

        public async Task PrintReceiptAsync(
            SalesHeader transaction,
            string printerName,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original)
        {
            ValidatePrinterName(printerName);

            string documentText =
                await BuildReceiptPreviewAsync(
                    transaction,
                    paperWidth,
                    copyLabel);

            await PrintTextDocumentAsync(
                documentText,
                printerName,
                "POS Sales Receipt");
        }

        public async Task PrintTaxInvoiceAsync(
            SalesHeader transaction,
            DateTime issuedAtUtc,
            string printerName,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original)
        {
            ValidatePrinterName(printerName);

            string documentText =
                await BuildTaxInvoicePreviewAsync(
                    transaction,
                    issuedAtUtc,
                    paperWidth,
                    copyLabel);

            await PrintTextDocumentAsync(
                documentText,
                printerName,
                "POS Tax Invoice");
        }

        public async Task<string> BuildCreditNotePreviewAsync(
            CustomerReturnHeader returnHeader,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original)
        {
            if (returnHeader == null)
                throw new ArgumentNullException(nameof(returnHeader));

            StoreSettings storeSettings =
                await _storeSettingsRepository
                    .GetOrCreateDefaultAsync();

            return _customerCreditNoteFormatter.FormatCreditNote(
                returnHeader,
                storeSettings,
                paperWidth,
                copyLabel);
        }

        public async Task PrintCreditNoteAsync(
            CustomerReturnHeader returnHeader,
            string printerName,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original)
        {
            ValidatePrinterName(printerName);

            string documentText =
                await BuildCreditNotePreviewAsync(
                    returnHeader,
                    paperWidth,
                    copyLabel);

            await PrintTextDocumentAsync(
                documentText,
                printerName,
                "POS Customer Credit Note");
        }

        private static Task PrintTextDocumentAsync(
            string documentText,
            string printerName,
            string documentName)
        {
            return Task.Run(() =>
            {
                var bytes = new List<byte>();
                AddText(bytes, documentText);
                AddText(bytes, "\n");
                bytes.AddRange(PaperCut);

                bool printed =
                    RawPrinterHelper.SendBytesToPrinter(
                        printerName,
                        bytes.ToArray(),
                        documentName);

                if (!printed)
                {
                    throw new InvalidOperationException(
                        $"Print failed. Printer not available: {printerName}");
                }
            });
        }

        public async Task PrintQuotationAsync(
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

            StoreSettings storeSettings =
                await _storeSettingsRepository
                    .GetOrCreateDefaultAsync();

            int columns =
                GetColumns(paperWidth);

            await Task.Run(() =>
            {
                var bytes = new List<byte>();

                bytes.AddRange(EscInitialize);

                AddStoreHeader(
                    bytes,
                    storeSettings,
                    columns);

                bytes.AddRange(AlignCenter);
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
                        $"{QuantityDisplayFormatter.Format(line.Quantity)} " +
                        $"{SafeText(line.Uom, 6)} x " +
                        $"{FormatMoney(line.UnitPrice, storeSettings)}";

                    string total =
                        FormatMoney(
                            line.LineTotal,
                            storeSettings);

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
                                FormatMoney(
                                    line.DiscountAmount,
                                    storeSettings),
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
                    $"{FormatMoney(request.GrossTotal, storeSettings)}\n");

                if (request.TotalDiscount > 0m)
                {
                    AddText(
                        bytes,
                        $"Discount   : " +
                        $"{FormatMoney(request.TotalDiscount, storeSettings)}\n");
                }

                bytes.AddRange(BoldOn);

                AddText(
                    bytes,
                    $"NET TOTAL  : " +
                    $"{FormatMoney(request.NetTotal, storeSettings)}\n");

                bytes.AddRange(BoldOff);
                bytes.AddRange(AlignCenter);

                AddText(
                    bytes,
                    "\nPrices may change.\n");

                AddText(
                    bytes,
                    "Final stock availability must be confirmed at billing time.\n");

                AddStoreFooter(
                    bytes,
                    storeSettings,
                    columns);

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

        public Task PrintTextAsync(
            string documentText,
            string printerName,
            string documentName)
        {
            ValidatePrinterName(printerName);

            if (string.IsNullOrWhiteSpace(documentText))
                throw new InvalidOperationException("The report is empty.");

            return PrintTextDocumentAsync(
                documentText,
                printerName,
                string.IsNullOrWhiteSpace(documentName)
                    ? "POS Report"
                    : documentName.Trim());
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

        private static void AddStoreHeader(
            List<byte> bytes,
            StoreSettings settings,
            int columns)
        {
            bytes.AddRange(AlignCenter);
            bytes.AddRange(BoldOn);

            string storeName =
                FirstNonEmpty(
                    settings.StoreName,
                    settings.LegalName,
                    "My Store");

            AddText(
                bytes,
                SafeText(
                    storeName,
                    columns) +
                "\n");

            bytes.AddRange(BoldOff);

            if (!string.IsNullOrWhiteSpace(
                    settings.LegalName) &&
                !string.Equals(
                    settings.LegalName.Trim(),
                    storeName,
                    StringComparison.OrdinalIgnoreCase))
            {
                AddText(
                    bytes,
                    SafeText(
                        settings.LegalName,
                        columns) +
                    "\n");
            }

            AddMultilineCentered(
                bytes,
                settings.ReceiptHeader,
                columns);

            AddOptionalCenteredLine(
                bytes,
                settings.AddressLine1,
                columns);

            AddOptionalCenteredLine(
                bytes,
                settings.AddressLine2,
                columns);

            string cityLine =
                JoinNonEmpty(
                    ", ",
                    settings.City,
                    settings.PostalCode);

            AddOptionalCenteredLine(
                bytes,
                cityLine,
                columns);

            AddOptionalCenteredLine(
                bytes,
                settings.Country,
                columns);

            if (!string.IsNullOrWhiteSpace(
                    settings.Phone))
            {
                AddOptionalCenteredLine(
                    bytes,
                    $"Tel: {settings.Phone.Trim()}",
                    columns);
            }

            if (!string.IsNullOrWhiteSpace(
                    settings.Email))
            {
                AddOptionalCenteredLine(
                    bytes,
                    $"Email: {settings.Email.Trim()}",
                    columns);
            }

            if (!string.IsNullOrWhiteSpace(
                    settings.Brn))
            {
                AddOptionalCenteredLine(
                    bytes,
                    $"BRN: {settings.Brn.Trim()}",
                    columns);
            }

            if (!string.IsNullOrWhiteSpace(
                    settings.TaxNo))
            {
                AddOptionalCenteredLine(
                    bytes,
                    $"VAT No: {settings.TaxNo.Trim()}",
                    columns);
            }

            AddText(bytes, "\n");
        }

        private static void AddStoreFooter(
            List<byte> bytes,
            StoreSettings settings,
            int columns)
        {
            bytes.AddRange(AlignCenter);
            AddText(bytes, "\n");

            string footer =
                string.IsNullOrWhiteSpace(
                    settings.ReceiptFooter)
                    ? "Thank You! Come Again."
                    : settings.ReceiptFooter;

            AddMultilineCentered(
                bytes,
                footer,
                columns);

            AddText(
                bytes,
                "\n\n");
        }

        private static void AddMultilineCentered(
            List<byte> bytes,
            string? value,
            int columns)
        {
            string text =
                (value ?? string.Empty)
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Trim();

            if (string.IsNullOrWhiteSpace(text))
                return;

            foreach (string line in
                     text.Split('\n'))
            {
                AddOptionalCenteredLine(
                    bytes,
                    line,
                    columns);
            }
        }

        private static void AddOptionalCenteredLine(
            List<byte> bytes,
            string? value,
            int columns)
        {
            string line =
                SafeText(
                    value,
                    columns);

            if (string.IsNullOrWhiteSpace(line))
                return;

            AddText(
                bytes,
                line + "\n");
        }

        private static string FormatMoney(
            decimal value,
            StoreSettings settings)
        {
            string symbol =
                string.IsNullOrWhiteSpace(
                    settings.CurrencySymbol)
                    ? "Rs."
                    : settings.CurrencySymbol.Trim();

            return $"{symbol} {value:0.00}";
        }

        private static string JoinNonEmpty(
            string separator,
            params string?[] values)
        {
            var parts =
                new List<string>();

            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add(value.Trim());
            }

            return string.Join(
                separator,
                parts);
        }

        private static string FirstNonEmpty(
            params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
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
            [MarshalAs(
                UnmanagedType.LPWStr)]
            public string DocName =
                string.Empty;

            [MarshalAs(
                UnmanagedType.LPWStr)]
            public string OutputFile =
                string.Empty;

            [MarshalAs(
                UnmanagedType.LPWStr)]
            public string DataType =
                "RAW";
        }

        [DllImport(
            "winspool.drv",
            EntryPoint = "OpenPrinterW",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        private static extern bool OpenPrinter(
            string printerName,
            out IntPtr printerHandle,
            IntPtr defaultPrinter);

        [DllImport(
            "winspool.drv",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        private static extern bool ClosePrinter(
            IntPtr printerHandle);

        [DllImport(
            "winspool.drv",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        private static extern bool StartDocPrinterW(
            IntPtr printerHandle,
            int level,
            [In]
            DocInfo documentInfo);

        [DllImport(
            "winspool.drv",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        private static extern bool EndDocPrinter(
            IntPtr printerHandle);

        [DllImport(
            "winspool.drv",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        private static extern bool StartPagePrinter(
            IntPtr printerHandle);

        [DllImport(
            "winspool.drv",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        private static extern bool EndPagePrinter(
            IntPtr printerHandle);

        [DllImport(
            "winspool.drv",
            SetLastError = true,
            ExactSpelling = true,
            CallingConvention =
                CallingConvention.StdCall)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        private static extern bool WritePrinter(
            IntPtr printerHandle,
            IntPtr bytes,
            int count,
            out int written);

        public static bool SendBytesToPrinter(
            string printerName,
            byte[] bytes,
            string documentName)
        {
            IntPtr printerHandle =
                IntPtr.Zero;

            IntPtr unmanagedBytes =
                IntPtr.Zero;

            try
            {
                if (!OpenPrinter(
                        printerName,
                        out printerHandle,
                        IntPtr.Zero))
                {
                    return false;
                }

                var documentInfo =
                    new DocInfo
                    {
                        DocName =
                            string.IsNullOrWhiteSpace(
                                documentName)
                                ? "POS Document"
                                : documentName,
                        DataType = "RAW"
                    };

                if (!StartDocPrinterW(
                        printerHandle,
                        1,
                        documentInfo))
                {
                    return false;
                }

                bool documentStarted = true;

                try
                {
                    if (!StartPagePrinter(
                            printerHandle))
                    {
                        return false;
                    }

                    bool pageStarted = true;

                    try
                    {
                        unmanagedBytes =
                            Marshal.AllocCoTaskMem(
                                bytes.Length);

                        Marshal.Copy(
                            bytes,
                            0,
                            unmanagedBytes,
                            bytes.Length);

                        return WritePrinter(
                                   printerHandle,
                                   unmanagedBytes,
                                   bytes.Length,
                                   out int written) &&
                               written == bytes.Length;
                    }
                    finally
                    {
                        if (pageStarted)
                            EndPagePrinter(
                                printerHandle);
                    }
                }
                finally
                {
                    if (documentStarted)
                        EndDocPrinter(
                            printerHandle);
                }
            }
            finally
            {
                if (unmanagedBytes !=
                    IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(
                        unmanagedBytes);
                }

                if (printerHandle !=
                    IntPtr.Zero)
                {
                    ClosePrinter(
                        printerHandle);
                }
            }
        }
    }
}

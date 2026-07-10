using POS.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace POS.Hardware.Services
{
    public sealed class TerminalHardwareService :
        ITerminalHardwareService
    {
        private static readonly byte[] EscInitialize =
        {
            27, 64
        };

        private static readonly byte[] AlignCenter =
        {
            27, 97, 1
        };

        private static readonly byte[] AlignLeft =
        {
            27, 97, 0
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

        public IReadOnlyList<string>
            GetInstalledPrinterNames()
        {
            return PrinterSettings.InstalledPrinters
                .Cast<string>()
                .Where(name =>
                    !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    name => name,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public Task PrintTestReceiptAsync(
            string printerName,
            int paperWidth)
        {
            ValidatePrinterName(printerName);

            int columns =
                paperWidth == 58
                    ? 32
                    : 42;

            return Task.Run(() =>
            {
                var bytes = new List<byte>();

                bytes.AddRange(EscInitialize);
                bytes.AddRange(AlignCenter);
                bytes.AddRange(BoldOn);
                AddText(
                    bytes,
                    "ADVANCED POS SYSTEM\n");
                bytes.AddRange(BoldOff);
                AddText(
                    bytes,
                    "TERMINAL PRINTER TEST\n");
                AddText(
                    bytes,
                    new string('-', columns) +
                    "\n");

                bytes.AddRange(AlignLeft);
                AddText(
                    bytes,
                    $"Printer : {SafeText(printerName, columns - 10)}\n");
                AddText(
                    bytes,
                    $"Paper   : {paperWidth} mm\n");
                AddText(
                    bytes,
                    $"Machine : {SafeText(Environment.MachineName, columns - 10)}\n");
                AddText(
                    bytes,
                    $"Time    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                AddText(
                    bytes,
                    new string('-', columns) +
                    "\n");

                bytes.AddRange(AlignCenter);
                AddText(
                    bytes,
                    "Printer test completed.\n\n\n");

                bytes.AddRange(PaperCut);

                TerminalRawPrinterTransport.SendOrThrow(
                    printerName,
                    bytes.ToArray(),
                    "Advanced POS Printer Test");
            });
        }

        public Task OpenCashDrawerAsync(
            string printerName)
        {
            ValidatePrinterName(printerName);

            return Task.Run(() =>
            {
                TerminalRawPrinterTransport.SendOrThrow(
                    printerName,
                    DrawerKick,
                    "Advanced POS Drawer Test");
            });
        }

        private static void ValidatePrinterName(
            string? printerName)
        {
            if (string.IsNullOrWhiteSpace(
                    printerName))
            {
                throw new InvalidOperationException(
                    "Select a Windows receipt printer first.");
            }
        }

        private static void AddText(
            List<byte> bytes,
            string text)
        {
            bytes.AddRange(
                Encoding.ASCII.GetBytes(
                    text ?? string.Empty));
        }

        private static string SafeText(
            string? value,
            int maxLength)
        {
            string safeValue =
                (value ?? string.Empty)
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Trim();

            if (safeValue.Length <= maxLength)
                return safeValue;

            return safeValue.Substring(
                0,
                Math.Max(0, maxLength));
        }
    }

    internal static class
        TerminalRawPrinterTransport
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

        public static void SendOrThrow(
            string printerName,
            byte[] data,
            string documentName)
        {
            if (string.IsNullOrWhiteSpace(
                    printerName))
            {
                throw new InvalidOperationException(
                    "Receipt printer is not configured.");
            }

            if (data == null ||
                data.Length == 0)
            {
                throw new InvalidOperationException(
                    "Printer command is empty.");
            }

            IntPtr printerHandle =
                IntPtr.Zero;

            IntPtr unmanagedBytes =
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
                    ThrowLastPrinterError(
                        $"Windows could not open printer '{printerName}'.");
                }

                var documentInfo =
                    new DocInfo
                    {
                        DocumentName =
                            string.IsNullOrWhiteSpace(
                                documentName)
                                ? "Advanced POS Print"
                                : documentName.Trim()
                    };

                if (!StartDocPrinter(
                        printerHandle,
                        1,
                        documentInfo))
                {
                    ThrowLastPrinterError(
                        "Windows could not start the printer document.");
                }

                bool documentStarted = true;

                try
                {
                    if (!StartPagePrinter(
                            printerHandle))
                    {
                        ThrowLastPrinterError(
                            "Windows could not start the printer page.");
                    }

                    bool pageStarted = true;

                    try
                    {
                        bool success =
                            WritePrinter(
                                printerHandle,
                                unmanagedBytes,
                                data.Length,
                                out int bytesWritten);

                        if (!success ||
                            bytesWritten !=
                                data.Length)
                        {
                            ThrowLastPrinterError(
                                "Windows could not send the complete printer command.");
                        }
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

        private static void ThrowLastPrinterError(
            string message)
        {
            int errorCode =
                Marshal.GetLastWin32Error();

            if (errorCode > 0)
            {
                throw new Win32Exception(
                    errorCode,
                    message);
            }

            throw new InvalidOperationException(
                message);
        }
    }
}

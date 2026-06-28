using POS.Core.Models;
using POS.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;
using DrawingBitmap = System.Drawing.Bitmap;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfImage = System.Windows.Controls.Image;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace POS.BackOffice.UI.Services
{
    public class WpfBarcodePrintService : IBarcodePrintService
    {
        private const double MmToDip = 3.779527559055118;

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public List<string> GetInstalledPrinters()
        {
            var printers = new List<string>();

            try
            {
                using var server = new LocalPrintServer();

                var queues = server.GetPrintQueues(new[]
                {
                    EnumeratedPrintQueueTypes.Local,
                    EnumeratedPrintQueueTypes.Connections
                });

                printers.AddRange(
                    queues
                        .Select(q => q.FullName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct()
                        .OrderBy(name => name));
            }
            catch
            {
                printers.Add("Microsoft Print to PDF");
            }

            return printers;
        }

        public async Task PrintLabelsAsync(
            List<BarcodePrintJobItem> items,
            LabelSettings settings)
        {
            ValidatePrintRequest(items, settings);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                double widthDip = settings.WidthMm * MmToDip;
                double heightDip = settings.HeightMm * MmToDip;

                if (widthDip <= 0 || heightDip <= 0)
                    throw new InvalidOperationException("Invalid label size.");

                var pageSize = new WpfSize(widthDip, heightDip);
                var fixedDocument = new FixedDocument();

                foreach (var item in items)
                {
                    ValidatePrintItem(item);

                    for (int i = 0; i < item.PrintQuantity; i++)
                    {
                        var pageContent = new PageContent();

                        var fixedPage = new FixedPage
                        {
                            Width = pageSize.Width,
                            Height = pageSize.Height,
                            Background = WpfBrushes.White
                        };

                        var labelVisual = CreateLabelVisual(item, settings, pageSize);

                        fixedPage.Children.Add(labelVisual);

                        fixedPage.Measure(pageSize);
                        fixedPage.Arrange(new Rect(new WpfPoint(0, 0), pageSize));
                        fixedPage.UpdateLayout();

                        ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
                        fixedDocument.Pages.Add(pageContent);
                    }
                }

                using var printServer = new LocalPrintServer();
                var printQueue = ResolvePrintQueue(printServer, settings.PrinterName);

                var printTicket = printQueue.DefaultPrintTicket;
                printTicket.PageMediaSize = new PageMediaSize(pageSize.Width, pageSize.Height);

                var writer = PrintQueue.CreateXpsDocumentWriter(printQueue);
                writer.Write(fixedDocument, printTicket);
            });
        }

        private static UIElement CreateLabelVisual(
            BarcodePrintJobItem item,
            LabelSettings settings,
            WpfSize pageSize)
        {
            var outer = new Border
            {
                Width = pageSize.Width,
                Height = pageSize.Height,
                Background = WpfBrushes.White,
                Padding = new Thickness(2)
            };

            var grid = new Grid
            {
                Width = Math.Max(1, pageSize.Width - 4),
                Height = Math.Max(1, pageSize.Height - 4)
            };

            outer.Child = grid;

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int row = 0;

            if (settings.PrintStoreName)
            {
                var storeText = new TextBlock
                {
                    Text = SafeTrim(settings.StoreName, 35),
                    FontSize = GetStoreFontSize(pageSize.Height),
                    FontWeight = FontWeights.Bold,
                    FontFamily = new WpfFontFamily("Arial"),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                Grid.SetRow(storeText, row++);
                grid.Children.Add(storeText);
            }

            if (settings.PrintItemName)
            {
                var itemText = new TextBlock
                {
                    Text = SafeTrim(item.ItemName, 42),
                    FontSize = GetItemFontSize(pageSize.Height),
                    FontFamily = new WpfFontFamily("Arial"),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 1, 0, 1)
                };

                Grid.SetRow(itemText, row++);
                grid.Children.Add(itemText);
            }

            var barcodePanel = CreateBarcodePanel(item, pageSize);
            Grid.SetRow(barcodePanel, 2);
            grid.Children.Add(barcodePanel);

            var footer = CreateFooterPanel(item, settings, pageSize);
            Grid.SetRow(footer, 3);
            grid.Children.Add(footer);

            return outer;
        }

        private static UIElement CreateBarcodePanel(
            BarcodePrintJobItem item,
            WpfSize pageSize)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            try
            {
                BarcodeFormat format = DetectBarcodeFormat(item.Barcode);

                int barcodeWidth = Math.Max(80, (int)(pageSize.Width * 0.90));
                int barcodeHeight = Math.Max(24, (int)(pageSize.Height * 0.38));

                var writer = new BarcodeWriter
                {
                    Format = format,
                    Options = new EncodingOptions
                    {
                        Width = barcodeWidth,
                        Height = barcodeHeight,
                        Margin = 0,
                        PureBarcode = true
                    }
                };

                using DrawingBitmap bitmap = writer.Write(item.Barcode);

                IntPtr hBitmap = bitmap.GetHbitmap();

                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    source.Freeze();

                    var image = new WpfImage
                    {
                        Source = source,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        MaxWidth = pageSize.Width * 0.92,
                        MaxHeight = pageSize.Height * 0.45
                    };

                    panel.Children.Add(image);
                }
                finally
                {
                    DeleteObject(hBitmap);
                }

                var barcodeText = new TextBlock
                {
                    Text = item.Barcode,
                    FontSize = GetBarcodeTextFontSize(pageSize.Height),
                    FontFamily = new WpfFontFamily("Arial"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 0)
                };

                panel.Children.Add(barcodeText);
            }
            catch
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "[Invalid Barcode]",
                    FontSize = 8,
                    Foreground = WpfBrushes.Red,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            return panel;
        }

        private static UIElement CreateFooterPanel(
            BarcodePrintJobItem item,
            LabelSettings settings,
            WpfSize pageSize)
        {
            var footer = new Grid
            {
                Margin = new Thickness(0, 1, 0, 0)
            };

            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (settings.PrintItemCode)
            {
                var codeText = new TextBlock
                {
                    Text = SafeTrim(item.ItemCode, 18),
                    FontSize = GetFooterFontSize(pageSize.Height),
                    FontFamily = new WpfFontFamily("Arial"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                Grid.SetColumn(codeText, 0);
                footer.Children.Add(codeText);
            }

            if (settings.PrintPrice)
            {
                var priceText = new TextBlock
                {
                    Text = $"Rs. {item.Price:N2}",
                    FontSize = GetPriceFontSize(pageSize.Height),
                    FontWeight = FontWeights.Bold,
                    FontFamily = new WpfFontFamily("Arial"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom
                };

                Grid.SetColumn(priceText, 1);
                footer.Children.Add(priceText);
            }

            return footer;
        }

        private static BarcodeFormat DetectBarcodeFormat(string barcode)
        {
            string value = NormalizeBarcode(barcode);

            if (value.Length == 13 && IsDigitsOnly(value) && IsValidEan13(value))
                return BarcodeFormat.EAN_13;

            if (value.Length == 12 && IsDigitsOnly(value) && IsValidUpcA(value))
                return BarcodeFormat.UPC_A;

            if (value.Length == 8 && IsDigitsOnly(value) && IsValidEan8(value))
                return BarcodeFormat.EAN_8;

            return BarcodeFormat.CODE_128;
        }

        private static void ValidatePrintRequest(
            List<BarcodePrintJobItem> items,
            LabelSettings settings)
        {
            if (settings == null)
                throw new InvalidOperationException("Print settings are missing.");

            if (string.IsNullOrWhiteSpace(settings.PrinterName))
                throw new InvalidOperationException("Please select a valid printer.");

            if (items == null || !items.Any())
                throw new InvalidOperationException("No labels are queued for printing.");

            if (settings.WidthMm < 20 || settings.WidthMm > 100)
                throw new InvalidOperationException("Label width must be between 20mm and 100mm.");

            if (settings.HeightMm < 10 || settings.HeightMm > 80)
                throw new InvalidOperationException("Label height must be between 10mm and 80mm.");

            if (settings.PrintStoreName && string.IsNullOrWhiteSpace(settings.StoreName))
                throw new InvalidOperationException("Store name is required when store name printing is enabled.");
        }

        private static void ValidatePrintItem(BarcodePrintJobItem item)
        {
            if (item == null)
                throw new InvalidOperationException("Print item is missing.");

            if (string.IsNullOrWhiteSpace(item.Barcode))
                throw new InvalidOperationException($"Barcode is missing for item '{item.ItemCode}'.");

            if (item.PrintQuantity <= 0)
                throw new InvalidOperationException($"Print quantity must be greater than zero for item '{item.ItemCode}'.");

            if (item.PrintQuantity > 5000)
                throw new InvalidOperationException($"Print quantity is too high for item '{item.ItemCode}'.");
        }

        private static PrintQueue ResolvePrintQueue(
            LocalPrintServer printServer,
            string printerName)
        {
            var queues = printServer.GetPrintQueues(new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            });

            var queue = queues.FirstOrDefault(q =>
                string.Equals(q.FullName, printerName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(q.Name, printerName, StringComparison.OrdinalIgnoreCase));

            if (queue == null)
                throw new InvalidOperationException($"Printer '{printerName}' was not found.");

            return queue;
        }

        private static bool IsValidEan13(string barcode)
        {
            string value = NormalizeBarcode(barcode);

            if (value.Length != 13 || !IsDigitsOnly(value))
                return false;

            string first12 = value.Substring(0, 12);
            int expected = CalculateEan13CheckDigit(first12);
            int actual = value[12] - '0';

            return expected == actual;
        }

        private static bool IsValidUpcA(string barcode)
        {
            string value = NormalizeBarcode(barcode);

            if (value.Length != 12 || !IsDigitsOnly(value))
                return false;

            int sumOdd = 0;
            int sumEven = 0;

            for (int i = 0; i < 11; i++)
            {
                int digit = value[i] - '0';

                if (i % 2 == 0)
                    sumOdd += digit;
                else
                    sumEven += digit;
            }

            int check = (10 - ((sumOdd * 3 + sumEven) % 10)) % 10;
            return check == value[11] - '0';
        }

        private static bool IsValidEan8(string barcode)
        {
            string value = NormalizeBarcode(barcode);

            if (value.Length != 8 || !IsDigitsOnly(value))
                return false;

            int sumOdd = 0;
            int sumEven = 0;

            for (int i = 0; i < 7; i++)
            {
                int digit = value[i] - '0';

                if (i % 2 == 0)
                    sumOdd += digit;
                else
                    sumEven += digit;
            }

            int check = (10 - ((sumOdd * 3 + sumEven) % 10)) % 10;
            return check == value[7] - '0';
        }

        private static int CalculateEan13CheckDigit(string first12Digits)
        {
            string value = NormalizeBarcode(first12Digits);

            if (value.Length != 12 || !IsDigitsOnly(value))
                throw new InvalidOperationException("EAN-13 check digit calculation requires exactly 12 digits.");

            int sum = 0;

            for (int i = 0; i < 12; i++)
            {
                int digit = value[i] - '0';
                sum += (i % 2 == 0) ? digit : digit * 3;
            }

            return (10 - (sum % 10)) % 10;
        }

        private static bool IsDigitsOnly(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (char c in value)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        private static string NormalizeBarcode(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string SafeTrim(string? value, int maxLength)
        {
            string text = (value ?? string.Empty).Trim();

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength);
        }

        private static double GetStoreFontSize(double labelHeight)
        {
            return labelHeight < 80 ? 7 : 9;
        }

        private static double GetItemFontSize(double labelHeight)
        {
            return labelHeight < 80 ? 6.5 : 8;
        }

        private static double GetBarcodeTextFontSize(double labelHeight)
        {
            return labelHeight < 80 ? 7 : 8.5;
        }

        private static double GetFooterFontSize(double labelHeight)
        {
            return labelHeight < 80 ? 6.5 : 8;
        }

        private static double GetPriceFontSize(double labelHeight)
        {
            return labelHeight < 80 ? 8 : 10;
        }
    }
}
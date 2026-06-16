using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using POS.Core.Models;
using POS.Core.Services;
using ZXing;
using ZXing.Windows.Compatibility; // Requires NuGet package

namespace POS.BackOffice.UI.Services
{
    public class WpfBarcodePrintService : IBarcodePrintService
    {
        // Conversion factor: 1 millimeter = ~3.7795 Device Independent Pixels (DIPs) in WPF
        private const double MmToDip = 3.779527559055118;

        public List<string> GetInstalledPrinters()
        {
            var printers = new List<string>();
            try
            {
                using var server = new LocalPrintServer();
                var printQueues = server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
                printers.AddRange(printQueues.Select(pq => pq.FullName));
            }
            catch (Exception)
            {
                // Fallback if PrintServer access is denied by Windows
                printers.Add("Microsoft Print to PDF");
            }
            return printers;
        }

        public async Task PrintLabelsAsync(List<BarcodePrintJobItem> items, LabelSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
                throw new InvalidOperationException("Please select a printer.");

            if (!items.Any())
                throw new InvalidOperationException("No items selected for printing.");

            // Run WPF rendering on the UI thread dispatcher
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 1. Create a spoolable batch document
                var fixedDoc = new FixedDocument();

                // Convert mm settings to WPF screen pixels
                double widthDip = settings.WidthMm * MmToDip;
                double heightDip = settings.HeightMm * MmToDip;
                var pageSize = new Size(widthDip, heightDip);

                // Initialize the Barcode Generator (Code128 is retail standard for alphanumeric)
                var barcodeWriter = new BarcodeWriter
                {
                    Format = BarcodeFormat.CODE_128,
                    Options = new ZXing.Common.EncodingOptions
                    {
                        Width = (int)widthDip,
                        Height = (int)(heightDip * 0.4), // Barcode takes up 40% of label height
                        Margin = 0,
                        PureBarcode = true // We will draw the text ourselves for better clarity
                    }
                };

                // 2. Build Pages for the Print Queue
                foreach (var item in items)
                {
                    // If they want 5 labels, generate 5 identical pages in the document
                    for (int i = 0; i < item.PrintQuantity; i++)
                    {
                        var pageContent = new PageContent();
                        var fixedPage = new FixedPage
                        {
                            Width = pageSize.Width,
                            Height = pageSize.Height,
                            Background = Brushes.White
                        };

                        // Build the visual label UI
                        var labelCanvas = CreateLabelVisual(item, settings, barcodeWriter, pageSize);

                        fixedPage.Children.Add(labelCanvas);

                        // Force WPF to measure and arrange the layout before printing
                        fixedPage.Measure(pageSize);
                        fixedPage.Arrange(new Rect(new Point(), pageSize));
                        fixedPage.UpdateLayout();

                        ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
                        fixedDoc.Pages.Add(pageContent);
                    }
                }

                // 3. Send to Print Spooler silently (No Dialog Box!)
                using var printServer = new LocalPrintServer();
                var printQueue = printServer.GetPrintQueue(settings.PrinterName);

                var printTicket = printQueue.DefaultPrintTicket;
                printTicket.PageMediaSize = new PageMediaSize(pageSize.Width, pageSize.Height);

                var writer = PrintQueue.CreateXpsDocumentWriter(printQueue);
                writer.Write(fixedDoc, printTicket);
            });
        }

        // --- THE VISUAL LABEL BUILDER ---
        private UIElement CreateLabelVisual(BarcodePrintJobItem item, LabelSettings settings, BarcodeWriter writer, Size pageSize)
        {
            var grid = new Grid
            {
                Width = pageSize.Width,
                Height = pageSize.Height,
                Margin = new Thickness(2) // 2px safe margin from edge
            };

            // Define 4 vertical rows: Store Name, Item Name, Barcode Image, Price
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int currentRow = 0;

            if (settings.PrintStoreName)
            {
                var txtStore = new TextBlock
                {
                    Text = settings.StoreName,
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new FontFamily("Arial")
                };
                Grid.SetRow(txtStore, currentRow++);
                grid.Children.Add(txtStore);
            }

            if (settings.PrintItemName)
            {
                var txtItem = new TextBlock
                {
                    Text = item.ItemName,
                    FontSize = 7,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxHeight = 12,
                    FontFamily = new FontFamily("Arial")
                };
                Grid.SetRow(txtItem, currentRow++);
                grid.Children.Add(txtItem);
            }

            // Generate and attach the Barcode Image
            try
            {
                var bitmap = writer.Write(item.Barcode);
                var imgBarcode = new Image
                {
                    Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        bitmap.GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                Grid.SetRow(imgBarcode, currentRow++);
                grid.Children.Add(imgBarcode);
            }
            catch
            {
                // Fallback if barcode generation fails (e.g. invalid characters)
                var txtError = new TextBlock { Text = "[Invalid Barcode]", FontSize = 8, Foreground = Brushes.Red, HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetRow(txtError, currentRow++);
                grid.Children.Add(txtError);
            }

            // Footer: Price and/or Item Code
            var footerPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            if (settings.PrintItemCode)
            {
                footerPanel.Children.Add(new TextBlock { Text = item.ItemCode, FontSize = 7, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Bottom });
            }

            if (settings.PrintPrice)
            {
                footerPanel.Children.Add(new TextBlock { Text = $"Rs. {item.Price:N2}", FontSize = 9, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Bottom });
            }

            Grid.SetRow(footerPanel, currentRow);
            grid.Children.Add(footerPanel);

            return grid;
        }
    }
}
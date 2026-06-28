using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class BarcodePrinterViewModel : ObservableObject
    {
        private readonly IBarcodePrintService _printService;
        private readonly BarcodePrinterRepository _printerRepository;

        // =========================================================
        // PRINTER SETTINGS
        // =========================================================

        [ObservableProperty]
        private BarcodeLabelSettingsDto _printConfig = new();

        [ObservableProperty]
        private string _selectedPrinter = string.Empty;

        public ObservableCollection<string> AvailablePrinters { get; } = new();

        // =========================================================
        // GRN INTAKE
        // =========================================================

        public ObservableCollection<BarcodeRecentGrnDto> RecentGrns { get; } = new();

        [ObservableProperty]
        private BarcodeRecentGrnDto? _selectedGrn;

        // =========================================================
        // MANUAL INTAKE
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private int _manualQty = 1;

        // =========================================================
        // PRINT QUEUE
        // =========================================================

        public ObservableCollection<BarcodePrintQueueItemDto> PrintQueue { get; } = new();

        [ObservableProperty]
        private int _totalLabelsToGenerate = 0;

        [ObservableProperty]
        private int _queueItemCount = 0;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public BarcodePrinterViewModel(
            IBarcodePrintService printService,
            BarcodePrinterRepository printerRepository)
        {
            _printService = printService;
            _printerRepository = printerRepository;

            PrintQueue.CollectionChanged += PrintQueue_CollectionChanged;

            _ = InitializeAsync();
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        private async Task InitializeAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading barcode printer page...";

            try
            {
                LoadInstalledPrinters();
                await LoadRecentGrnsAsync();

                StatusMessage = "Barcode printer page ready.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize barcode printer page.";

                MessageBox.Show(
                    $"Failed to initialize barcode printer page:\n\n{ex.Message}",
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LoadInstalledPrinters()
        {
            AvailablePrinters.Clear();

            var printers = _printService.GetInstalledPrinters();

            foreach (var printer in printers)
                AvailablePrinters.Add(printer);

            if (!AvailablePrinters.Any())
                return;

            string? preferredPrinter = AvailablePrinters.FirstOrDefault(p =>
                p.Contains("Zebra", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("TSC", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Xprinter", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Label", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Barcode", StringComparison.OrdinalIgnoreCase));

            SelectedPrinter = preferredPrinter ?? AvailablePrinters.First();
            PrintConfig.PrinterName = SelectedPrinter;
        }

        [RelayCommand]
        private async Task LoadRecentGrnsAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading recent posted GRNs...";

            try
            {
                RecentGrns.Clear();

                var grns = await _printerRepository.GetRecentPostedGrnsAsync(
                    daysBack: 30,
                    take: 50);

                foreach (var grn in grns)
                    RecentGrns.Add(grn);

                StatusMessage = $"Loaded {RecentGrns.Count} recent posted GRN(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Could not load recent GRNs.";

                MessageBox.Show(
                    $"Could not load recent GRNs:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedPrinterChanged(string value)
        {
            PrintConfig.PrinterName = value ?? string.Empty;
        }

        partial void OnManualQtyChanged(int value)
        {
            if (value < 1)
                ManualQty = 1;

            if (value > 5000)
                ManualQty = 5000;
        }

        // =========================================================
        // LOAD FROM GRN
        // =========================================================

        [RelayCommand]
        private async Task LoadGrnItemsAsync()
        {
            if (SelectedGrn == null)
            {
                MessageBox.Show(
                    "Please select a posted GRN first.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = $"Loading label queue from {SelectedGrn.GrnNumber}...";

            try
            {
                var rows = await _printerRepository.GetPrintQueueItemsForGrnAsync(
                    SelectedGrn.GrnHeaderId);

                int addedCount = 0;
                int missingBarcodeCount = 0;

                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.Barcode))
                        missingBarcodeCount++;

                    AddOrMergeQueueItem(row);
                    addedCount++;
                }

                UpdateTotalLabels();

                StatusMessage = $"Loaded {addedCount} GRN item(s) into print queue.";

                if (missingBarcodeCount > 0)
                {
                    MessageBox.Show(
                        $"{missingBarcodeCount} item(s) were added but do not have barcodes. Generate barcodes before printing.",
                        "Missing Barcodes",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load GRN label items.";

                MessageBox.Show(
                    $"Failed to load GRN lines:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // MANUAL ADD
        // =========================================================

        [RelayCommand]
        private async Task AddManualItemAsync()
        {
            string search = (SearchText ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(search))
                return;

            if (ManualQty <= 0)
            {
                MessageBox.Show(
                    "Print quantity must be at least 1.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = $"Searching item '{search}'...";

            try
            {
                var item = await _printerRepository.FindItemForPrintingAsync(search);

                if (item == null)
                {
                    MessageBox.Show(
                        $"Item, SKU, or barcode '{search}' was not found.",
                        "Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    StatusMessage = "Manual item not found.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(item.Barcode))
                {
                    MessageBox.Show(
                        $"Item '{item.DisplayName}' does not have a barcode. Use Barcode Management to assign or generate one first.",
                        "Missing Barcode",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    StatusMessage = "Item has no barcode.";
                    return;
                }

                item.PrintQuantity = ManualQty;
                item.SourceDocument = "MANUAL";

                AddOrMergeQueueItem(item);

                SearchText = string.Empty;
                ManualQty = 1;

                UpdateTotalLabels();

                StatusMessage = $"Added {item.ItemCode} to print queue.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Manual barcode search failed.";

                MessageBox.Show(
                    $"Search failed:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void AddOrMergeQueueItem(BarcodePrintQueueItemDto item)
        {
            var existing = PrintQueue.FirstOrDefault(q =>
                q.ItemVariantId == item.ItemVariantId &&
                q.Barcode == item.Barcode);

            if (existing != null)
            {
                existing.PrintQuantity += item.PrintQuantity;
                return;
            }

            item.PropertyChanged += QueueItem_PropertyChanged;
            PrintQueue.Add(item);
        }

        // =========================================================
        // QUEUE MANAGEMENT
        // =========================================================

        [RelayCommand]
        private void RemoveQueueItem(BarcodePrintQueueItemDto? item)
        {
            if (item == null)
                return;

            item.PropertyChanged -= QueueItem_PropertyChanged;
            PrintQueue.Remove(item);

            UpdateTotalLabels();

            StatusMessage = $"Removed {item.ItemCode} from print queue.";
        }

        [RelayCommand]
        private void ClearQueue()
        {
            if (!PrintQueue.Any())
                return;

            var confirm = MessageBox.Show(
                "Clear all items from the print queue?",
                "Clear Print Queue",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            foreach (var item in PrintQueue)
                item.PropertyChanged -= QueueItem_PropertyChanged;

            PrintQueue.Clear();

            UpdateTotalLabels();

            StatusMessage = "Print queue cleared.";
        }

        // =========================================================
        // PRINT
        // =========================================================

        [RelayCommand]
        private async Task PrintSelectedLabelsAsync()
        {
            var queue = PrintQueue.ToList();

            var errors = BarcodePrinterRepository.ValidatePrintQueue(queue);
            errors.AddRange(PrintConfig.ValidateForPrint());

            if (errors.Any())
            {
                MessageBox.Show(
                    "Cannot print labels because validation failed:\n\n" +
                    string.Join("\n", errors),
                    "Print Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var confirm = MessageBox.Show(
                $"Send {TotalLabelsToGenerate} label(s) to printer?\n\nPrinter: {PrintConfig.PrinterName}",
                "Confirm Label Printing",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = "Dispatching labels to printer...";

            try
            {
                var modelItems = queue
                    .Select(q => new BarcodePrintJobItem
                    {
                        ItemCode = q.ItemCode,
                        ItemName = q.DisplayName,
                        Barcode = q.Barcode,
                        Price = q.Price,
                        PrintQuantity = q.PrintQuantity
                    })
                    .ToList();

                var modelSettings = new LabelSettings
                {
                    PrinterName = PrintConfig.PrinterName,
                    WidthMm = (double)PrintConfig.WidthMm,
                    HeightMm = (double)PrintConfig.HeightMm,
                    PrintStoreName = PrintConfig.PrintStoreName,
                    StoreName = PrintConfig.StoreName,
                    PrintItemName = PrintConfig.PrintItemName,
                    PrintPrice = PrintConfig.PrintPrice,
                    PrintItemCode = PrintConfig.PrintItemCode
                };

                await _printService.PrintLabelsAsync(modelItems, modelSettings);

                StatusMessage = $"Dispatched {TotalLabelsToGenerate} label(s) to printer.";

                MessageBox.Show(
                    "Labels were dispatched to the printer successfully.",
                    "Print Spooler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Printer dispatch failed.";

                MessageBox.Show(
                    $"Printer dispatch failed:\n\n{ex.Message}",
                    "Printer Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // EVENTS / TOTALS
        // =========================================================

        private void PrintQueue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (BarcodePrintQueueItemDto item in e.NewItems)
                    item.PropertyChanged += QueueItem_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (BarcodePrintQueueItemDto item in e.OldItems)
                    item.PropertyChanged -= QueueItem_PropertyChanged;
            }

            UpdateTotalLabels();
        }

        private void QueueItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BarcodePrintQueueItemDto.PrintQuantity))
                UpdateTotalLabels();
        }

        private void UpdateTotalLabels()
        {
            TotalLabelsToGenerate = PrintQueue.Sum(x => x.PrintQuantity);
            QueueItemCount = PrintQueue.Count;
        }
    }
}
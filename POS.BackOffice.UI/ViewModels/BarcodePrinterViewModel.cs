using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
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
        private readonly StoreSettingsRepository _storeSettingsRepository;

        private const int RecentGrnDaysBack = 30;
        private const int RecentGrnTakeLimit = 50;

        private string _configuredStoreName = string.Empty;

        private bool _isBulkSelectionChanging;
        private bool _isUpdatingSelectAllFromQueue;

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

        public ICollectionView RecentGrnsView { get; }

        [ObservableProperty]
        private string _grnSearchText = string.Empty;

        [ObservableProperty]
        private bool _isGrnDropdownOpen = false;

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
        private BarcodePrintQueueItemDto? _selectedQueueItem;

        [ObservableProperty]
        private bool _isSelectAllChecked = false;

        [ObservableProperty]
        private int _totalLabelsToGenerate = 0;

        [ObservableProperty]
        private int _queueItemCount = 0;

        [ObservableProperty]
        private int _selectedQueueItemCount = 0;

        [ObservableProperty]
        private int _selectedLabelsToGenerate = 0;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public BarcodePrinterViewModel(
            IBarcodePrintService printService,
            BarcodePrinterRepository printerRepository,
            StoreSettingsRepository storeSettingsRepository)
        {
            _printService = printService ??
                throw new ArgumentNullException(
                    nameof(printService));

            _printerRepository = printerRepository ??
                throw new ArgumentNullException(
                    nameof(printerRepository));

            _storeSettingsRepository =
                storeSettingsRepository ??
                throw new ArgumentNullException(
                    nameof(storeSettingsRepository));

            PrintQueue.CollectionChanged += PrintQueue_CollectionChanged;

            RecentGrnsView = CollectionViewSource.GetDefaultView(RecentGrns);
            RecentGrnsView.Filter = FilterRecentGrns;

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
                await LoadStoreIdentityAsync();
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

        private async Task LoadStoreIdentityAsync()
        {
            StoreSettings settings =
                await _storeSettingsRepository
                    .GetOrCreateDefaultAsync();

            _configuredStoreName =
                !string.IsNullOrWhiteSpace(
                    settings.StoreName)
                    ? settings.StoreName.Trim()
                    : settings.LegalName?.Trim() ??
                      string.Empty;

            PrintConfig.StoreName =
                _configuredStoreName;
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
                    daysBack: RecentGrnDaysBack,
                    take: RecentGrnTakeLimit);

                foreach (var grn in grns)
                    RecentGrns.Add(grn);

                if (RecentGrns.Count > 0 && SelectedGrn == null)
                    SelectedGrn = RecentGrns.FirstOrDefault();

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

        partial void OnGrnSearchTextChanged(string value)
        {
            IsGrnDropdownOpen = true;
            RecentGrnsView.Refresh();
        }

        private bool FilterRecentGrns(object item)
        {
            if (string.IsNullOrWhiteSpace(GrnSearchText))
                return true;

            if (item is BarcodeRecentGrnDto grn)
            {
                return grn.GrnNumber.Contains(GrnSearchText, StringComparison.OrdinalIgnoreCase) ||
                       grn.SupplierName.Contains(GrnSearchText, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        partial void OnManualQtyChanged(int value)
        {
            if (value < 1)
            {
                ManualQty = 1;
                return;
            }

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
            StatusMessage = $"Loading batch labels from {SelectedGrn.GrnNumber}...";

            try
            {
                var rows = await _printerRepository.GetPrintQueueItemsForGrnAsync(
                    SelectedGrn.GrnHeaderId);

                if (!rows.Any())
                {
                    MessageBox.Show(
                        "This GRN has no printable batch barcode labels. Average-cost GENERAL stock lines are not printed here.",
                        "No Batch Labels",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    StatusMessage = "No printable GRN batch labels found.";
                    return;
                }

                int addedCount = 0;
                int alreadyPrintedCount = 0;

                foreach (var row in rows)
                {
                    if (row.BarcodePrintedCount > 0)
                        alreadyPrintedCount++;

                    AddOrMergeQueueItem(row);
                    addedCount++;
                }

                RefreshQueueCounters();

                StatusMessage = $"Loaded {addedCount} GRN batch label row(s) into print queue.";

                if (alreadyPrintedCount > 0)
                {
                    MessageBox.Show(
                        $"{alreadyPrintedCount} batch label row(s) were already printed before. They were loaded for reprint, but may have print quantity 0. Change Print Qty if you need to reprint.",
                        "Reprint Notice",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load GRN batch labels.";

                MessageBox.Show(
                    $"Failed to load GRN batch labels:\n\n{ex.Message}",
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
            StatusMessage = $"Searching barcode '{search}'...";

            try
            {
                var item = await _printerRepository.FindItemForPrintingAsync(search);

                if (item == null)
                {
                    MessageBox.Show(
                        $"Item, SKU, item barcode, or GRN batch barcode '{search}' was not found.",
                        "Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    StatusMessage = "Manual barcode search found no result.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(item.EffectiveBarcode))
                {
                    MessageBox.Show(
                        $"Item '{item.DisplayName}' does not have a printable barcode.",
                        "Missing Barcode",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    StatusMessage = "Selected item has no barcode.";
                    return;
                }

                item.PrintQuantity = ManualQty;
                item.IsSelected = true;

                if (string.IsNullOrWhiteSpace(item.SourceDocument))
                    item.SourceDocument = "MANUAL";

                AddOrMergeQueueItem(item);

                SearchText = string.Empty;
                ManualQty = 1;

                RefreshQueueCounters();

                StatusMessage = item.IsBatchLabel
                    ? $"Added GRN batch barcode {item.EffectiveBarcode} to print queue."
                    : $"Added item barcode {item.EffectiveBarcode} to print queue.";
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
                (q.ItemBatchId ?? 0) == (item.ItemBatchId ?? 0) &&
                q.EffectiveBarcode.Equals(item.EffectiveBarcode, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (item.PrintQuantity > 0)
                    existing.PrintQuantity += item.PrintQuantity;

                if (item.IsSelected)
                    existing.IsSelected = true;

                return;
            }

            PrintQueue.Add(item);
        }

        // =========================================================
        // QUEUE MANAGEMENT
        // =========================================================

        partial void OnIsSelectAllCheckedChanged(bool value)
        {
            if (_isUpdatingSelectAllFromQueue)
                return;

            _isBulkSelectionChanging = true;

            foreach (var item in PrintQueue)
                item.IsSelected = value;

            _isBulkSelectionChanging = false;

            RefreshQueueCounters();
        }

        [RelayCommand]
        private void SelectAllQueue()
        {
            IsSelectAllChecked = true;
        }

        [RelayCommand]
        private void UnselectAllQueue()
        {
            IsSelectAllChecked = false;
        }

        [RelayCommand]
        private void SetPrintQtyToReceived()
        {
            var selectedRows = PrintQueue
                .Where(q => q.IsSelected && q.IsBatchLabel)
                .ToList();

            if (!selectedRows.Any())
            {
                MessageBox.Show(
                    "Select one or more GRN batch rows first.",
                    "No Batch Rows Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            foreach (var item in selectedRows)
                item.PrintQuantity = ConvertQtyToLabels(item.ReceivedQty);

            RefreshQueueCounters();
            StatusMessage = "Print quantity reset to received quantity for selected batch rows.";
        }

        [RelayCommand]
        private void RemoveQueueItem(BarcodePrintQueueItemDto? item)
        {
            if (item == null)
                return;

            PrintQueue.Remove(item);
            RefreshQueueCounters();

            StatusMessage = $"Removed {item.DisplayName} from print queue.";
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

            PrintQueue.Clear();
            RefreshQueueCounters();

            StatusMessage = "Print queue cleared.";
        }

        // =========================================================
        // PRINT
        // =========================================================

        [RelayCommand]
        private async Task PrintSelectedLabelsAsync()
        {
            var selectedQueue = PrintQueue
                .Where(q => q.IsSelected && q.PrintQuantity > 0)
                .ToList();

            var errors = BarcodePrinterRepository.ValidatePrintQueue(selectedQueue);
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

            int labelCount = selectedQueue.Sum(q => q.PrintQuantity);

            var confirm = MessageBox.Show(
                $"Send {labelCount} selected label(s) to printer?\n\nPrinter: {PrintConfig.PrinterName}",
                "Confirm Label Printing",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = "Dispatching selected labels to printer...";

            try
            {
                var modelItems = selectedQueue
                    .Select(q => new BarcodePrintJobItem
                    {
                        ItemCode = q.IsBatchLabel
                            ? q.BatchDisplayText
                            : q.ItemCode,
                        ItemName = BuildPrintItemName(q),
                        Barcode = q.EffectiveBarcode,
                        Price = q.Price,
                        PrintQuantity = q.PrintQuantity
                    })
                    .ToList();

                PrintConfig.StoreName =
                    _configuredStoreName;

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

                await _printerRepository.MarkBatchLabelsPrintedAsync(
                    selectedQueue,
                    printedBy: "Admin");

                DateTime now = DateTime.Now;

                foreach (var item in selectedQueue.Where(q => q.IsBatchLabel))
                {
                    item.BarcodePrintedCount += item.PrintQuantity;
                    item.LastBarcodePrintedAt = now;
                    item.LastBarcodePrintedBy = "Admin";
                    item.PrintQuantity = 0;
                    item.IsSelected = false;
                }

                RefreshQueueCounters();

                StatusMessage = $"Dispatched {labelCount} label(s) to printer.";

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

        private static string BuildPrintItemName(BarcodePrintQueueItemDto item)
        {
            string name = item.DisplayName;

            if (!item.IsBatchLabel)
                return name;

            string batch = string.IsNullOrWhiteSpace(item.BatchNo)
                ? string.Empty
                : $" | B:{item.BatchNo.Trim()}";

            string expiry = item.ExpiryDate.HasValue
                ? $" | E:{item.ExpiryDate.Value:yyyy-MM-dd}"
                : string.Empty;

            return $"{name}{batch}{expiry}";
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

            RefreshQueueCounters();
        }

        private void QueueItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isBulkSelectionChanging)
                return;

            if (e.PropertyName == nameof(BarcodePrintQueueItemDto.PrintQuantity) ||
                e.PropertyName == nameof(BarcodePrintQueueItemDto.IsSelected))
            {
                RefreshQueueCounters();
            }
        }

        private void RefreshQueueCounters()
        {
            QueueItemCount = PrintQueue.Count;
            TotalLabelsToGenerate = PrintQueue.Sum(q => q.PrintQuantity);

            SelectedQueueItemCount = PrintQueue.Count(q => q.IsSelected);
            SelectedLabelsToGenerate = PrintQueue
                .Where(q => q.IsSelected)
                .Sum(q => q.PrintQuantity);

            _isUpdatingSelectAllFromQueue = true;
            IsSelectAllChecked = PrintQueue.Any() && SelectedQueueItemCount == PrintQueue.Count;
            _isUpdatingSelectAllFromQueue = false;
        }

        private static int ConvertQtyToLabels(decimal qty)
        {
            if (qty <= 0m)
                return 0;

            return (int)Math.Ceiling(qty);
        }
    }
}

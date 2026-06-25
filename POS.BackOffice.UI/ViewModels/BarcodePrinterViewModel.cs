using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class BarcodePrinterViewModel : ObservableObject
    {
        private readonly IBarcodePrintService _printService;
        private readonly ItemMasterRepository _itemRepository;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        // --- ZONE 1: PRINTER SETTINGS ---
        [ObservableProperty] private LabelSettings _printConfig = new();
        [ObservableProperty] private string _selectedPrinter = string.Empty;
        public ObservableCollection<string> AvailablePrinters { get; } = new();

        // --- ZONE 2: GRN INTAKE (Mode A) ---
        public ObservableCollection<GrnHeader> RecentGrns { get; } = new();
        [ObservableProperty] private GrnHeader? _selectedGrn;

        // --- ZONE 3: MANUAL INTAKE (Mode B) ---
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _manualQty = 1;

        // --- ZONE 4: THE PRINT QUEUE ---
        public ObservableCollection<BarcodePrintJobItem> PrintQueue { get; } = new();

        [ObservableProperty] private int _totalLabelsToGenerate = 0;

        public BarcodePrinterViewModel(
            IBarcodePrintService printService,
            ItemMasterRepository itemRepository,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _printService = printService;
            _itemRepository = itemRepository;
            _contextFactory = contextFactory;

            // Subscribe to queue changes so the Total Labels counter is always perfectly accurate
            PrintQueue.CollectionChanged += PrintQueue_CollectionChanged;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // 1. Load Installed Windows Printers
            var printers = _printService.GetInstalledPrinters();
            foreach (var p in printers) AvailablePrinters.Add(p);

            if (AvailablePrinters.Any())
            {
                // Auto-detect a thermal printer if one exists, otherwise pick the first available
                SelectedPrinter = AvailablePrinters.FirstOrDefault(p => p.Contains("Zebra") || p.Contains("TSC") || p.Contains("Xprinter") || p.Contains("Label"))
                                  ?? AvailablePrinters.First();
                PrintConfig.PrinterName = SelectedPrinter;
            }

            // 2. Load Recent Posted GRNs (Last 30 Days) directly from DB Context
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var thirtyDaysAgo = DateTime.Now.AddDays(-30);

                var grns = await context.GrnHeaders
                    .Where(g => g.Status == "Posted" && g.ReceivedDate >= thirtyDaysAgo)
                    .OrderByDescending(g => g.ReceivedDate)
                    .Take(50)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var grn in grns) RecentGrns.Add(grn);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load recent GRNs: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- LIVE QUEUE MATH ENGINE ---
        private void PrintQueue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (BarcodePrintJobItem item in e.NewItems)
                    item.PropertyChanged += Item_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (BarcodePrintJobItem item in e.OldItems)
                    item.PropertyChanged -= Item_PropertyChanged;
            }
            UpdateTotalLabels();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // If the user types a new number in the DataGrid, recalculate totals
            if (e.PropertyName == nameof(BarcodePrintJobItem.PrintQuantity))
            {
                UpdateTotalLabels();
            }
        }

        private void UpdateTotalLabels()
        {
            TotalLabelsToGenerate = PrintQueue.Sum(x => x.PrintQuantity);
        }

        partial void OnSelectedPrinterChanged(string value)
        {
            PrintConfig.PrinterName = value;
        }

        // --- INTAKE MODE A: LOAD FROM GRN ---
        [RelayCommand]
        private async Task LoadGrnItemsAsync()
        {
            if (SelectedGrn == null)
            {
                MessageBox.Show("Please select a GRN from the list.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var grnLines = await context.GrnLines
                    .Include(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                    .Where(l => l.GrnHeaderId == SelectedGrn.Id && l.ReceivedQty > 0)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var line in grnLines)
                {
                    var variant = line.ItemVariant;

                    // Prevent duplicates in queue by checking if variant is already there
                    var existing = PrintQueue.FirstOrDefault(q => q.Barcode == variant.Barcode);
                    if (existing != null)
                    {
                        existing.PrintQuantity += (int)line.ReceivedQty;
                    }
                    else
                    {
                        PrintQueue.Add(new BarcodePrintJobItem
                        {
                            ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                            ItemName = string.IsNullOrWhiteSpace(variant.VariantDescription)
                                ? variant.ItemParent?.ItemName ?? "Unknown Item"
                                : $"{variant.ItemParent?.ItemName} - {variant.VariantDescription}",
                            Barcode = variant.Barcode ?? string.Empty,
                            Price = variant.RetailPrice,
                            PrintQuantity = (int)line.ReceivedQty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load GRN lines: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- INTAKE MODE B: MANUAL SCAN ---
        [RelayCommand]
        private async Task AddManualItemAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            if (ManualQty <= 0)
            {
                MessageBox.Show("Print quantity must be at least 1.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var variant = await _itemRepository.GetItemByBarcodeAsync(SearchText.Trim());

                if (variant == null)
                {
                    MessageBox.Show($"Barcode '{SearchText}' not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(variant.Barcode))
                {
                    MessageBox.Show($"Item '{variant.ItemParent?.ItemName}' does not have a system barcode assigned. Please use Barcode Management to generate one.", "No Barcode", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existing = PrintQueue.FirstOrDefault(q => q.Barcode == variant.Barcode);
                if (existing != null)
                {
                    existing.PrintQuantity += ManualQty;
                }
                else
                {
                    PrintQueue.Add(new BarcodePrintJobItem
                    {
                        ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                        ItemName = string.IsNullOrWhiteSpace(variant.VariantDescription)
                            ? variant.ItemParent?.ItemName ?? "Unknown Item"
                            : $"{variant.ItemParent?.ItemName} - {variant.VariantDescription}",
                        Barcode = variant.Barcode ?? string.Empty,
                        Price = variant.RetailPrice,
                        PrintQuantity = ManualQty
                    });
                }

                SearchText = string.Empty;
                ManualQty = 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- QUEUE MANAGEMENT ---
        [RelayCommand]
        private void RemoveQueueItem(BarcodePrintJobItem item)
        {
            if (item != null)
            {
                PrintQueue.Remove(item);
            }
        }

        [RelayCommand]
        private void ClearQueue()
        {
            PrintQueue.Clear();
        }

        // --- HARDWARE DISPATCH ---
        [RelayCommand]
        private async Task PrintSelectedLabelsAsync()
        {
            if (!PrintQueue.Any())
            {
                MessageBox.Show("The print queue is empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var invalidItems = PrintQueue.Where(i => string.IsNullOrWhiteSpace(i.Barcode)).ToList();
            if (invalidItems.Any())
            {
                MessageBox.Show($"Cannot print labels. {invalidItems.Count} item(s) are missing valid barcode strings.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _printService.PrintLabelsAsync(PrintQueue.ToList(), PrintConfig);
                MessageBox.Show("Labels dispatched to the printer successfully!", "Print Spooler", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hardware dispatch failed: {ex.Message}", "Printer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class GrnViewModel : ObservableObject
    {
        private readonly GrnRepository _grnRepository;
        private readonly ItemMasterRepository _itemMasterRepository;

        // --- ZONE 1: HEADER & SUPPLIER ---
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private string _supplierInvoiceNo = string.Empty;
        [ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
        [ObservableProperty] private DateTime _receivedDate = DateTime.Now;
        [ObservableProperty] private DateTime _dueDate = DateTime.Now.AddDays(30);
        [ObservableProperty] private string _documentStatus = "DRAFT";
        [ObservableProperty] private string _remarks = string.Empty;

        // ENTERPRISE TOGGLES
        [ObservableProperty] private bool _isTaxInclusive = false;
        [ObservableProperty] private bool _amortizeFoc = true;

        // --- ZONE 3: ENTRY MODES ---
        [ObservableProperty] private string _scanBarcode = string.Empty;
        [ObservableProperty] private string _matrixBatchNo = string.Empty;
        [ObservableProperty] private DateTime _matrixExpiryDate = DateTime.Now.AddYears(1);
        [ObservableProperty] private string _matrixFilterText = string.Empty; // Fast-filter for the grid

        // --- FINANCIAL TOTALS ---
        [ObservableProperty] private decimal _subtotal = 0m;
        [ObservableProperty] private decimal _totalDiscountAmount = 0m;
        [ObservableProperty] private decimal _globalBillDiscount = 0m;
        [ObservableProperty] private decimal _freightAmount = 0m;
        [ObservableProperty] private decimal _netPayable = 0m;

        // --- COLLECTIONS ---
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<GrnLine> GrnLines { get; set; } = new();
        public ObservableCollection<ItemMasterSummaryDto> AvailableItems { get; set; } = new();

        // The hidden list that holds all variants, and the visible list that binds to the UI
        private List<GrnLine> _allMatrixVariants = new();
        public ObservableCollection<GrnLine> ActiveMatrixVariants { get; set; } = new();

        [ObservableProperty] private GrnLine? _selectedLine;
        [ObservableProperty] private ItemMasterSummaryDto? _selectedItem;

        public GrnViewModel(GrnRepository grnRepository, ItemMasterRepository itemMasterRepository)
        {
            _grnRepository = grnRepository;
            _itemMasterRepository = itemMasterRepository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var suppliers = await _grnRepository.GetActiveSuppliersAsync();
            foreach (var sup in suppliers) Suppliers.Add(sup);

            var items = await _itemMasterRepository.GetSummariesAsync();
            foreach (var item in items) AvailableItems.Add(item);
        }

        // --- MATRIX ENGINE & FILTERING ---
        partial void OnSelectedItemChanged(ItemMasterSummaryDto? value)
        {
            if (value == null) return;
            _ = LoadVariantsForGridAsync(value.ParentId);
        }

        partial void OnMatrixFilterTextChanged(string value) => ApplyMatrixFilter();

        private async Task LoadVariantsForGridAsync(int parentId)
        {
            _allMatrixVariants.Clear();
            var variants = await _itemMasterRepository.GetVariantsByParentIdAsync(parentId);

            foreach (var variant in variants)
            {
                var newLine = new GrnLine
                {
                    ItemVariantId = variant.Id,
                    ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                    VariantDescription = variant.VariantDescription,
                    Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                    Barcode = variant.Barcode,
                    Uom = variant.ItemParent?.BaseUom ?? "PCS",
                    TaxCode = variant.ItemParent?.TaxCode ?? "Exempt",
                    UnitCost = variant.CostPrice,
                    RetailPrice = variant.RetailPrice,
                    WholesalePrice = variant.WholesalePrice,
                    MinimumPrice = variant.MinimumPrice,
                    ReceivedQty = 0
                };

                _allMatrixVariants.Add(newLine);
            }
            ApplyMatrixFilter();
        }

        private void ApplyMatrixFilter()
        {
            ActiveMatrixVariants.Clear();
            var query = _allMatrixVariants.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(MatrixFilterText))
            {
                var search = MatrixFilterText.ToLower();
                query = query.Where(v => v.VariantDescription.ToLower().Contains(search) ||
                                         v.Barcode.ToLower().Contains(search));
            }

            foreach (var item in query)
            {
                ActiveMatrixVariants.Add(item);
            }
        }

        // --- BARCODE SEARCH ENGINE ---
        [RelayCommand]
        private async Task AddItemAsync()
        {
            if (string.IsNullOrWhiteSpace(ScanBarcode)) return;

            var variant = await _itemMasterRepository.GetItemByBarcodeAsync(ScanBarcode.Trim());

            if (variant == null)
            {
                MessageBox.Show($"Barcode '{ScanBarcode}' not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                ScanBarcode = string.Empty;
                return;
            }

            var newLine = new GrnLine
            {
                ItemVariantId = variant.Id,
                ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                VariantDescription = variant.VariantDescription,
                Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                Barcode = variant.Barcode,
                Uom = variant.ItemParent?.BaseUom ?? "PCS",
                TaxCode = variant.ItemParent?.TaxCode ?? "Exempt",
                UnitCost = variant.CostPrice,
                RetailPrice = variant.RetailPrice,
                WholesalePrice = variant.WholesalePrice,
                MinimumPrice = variant.MinimumPrice,
                ReceivedQty = 1
            };

            // Drop directly into the main grid if scanning a specific barcode
            GrnLines.Add(newLine);
            ScanBarcode = string.Empty;
            RecalculateTotals();
        }

        // --- THE ENTERPRISE FINANCIAL ENGINE ---
        partial void OnFreightAmountChanged(decimal value) => RecalculateTotals();
        partial void OnGlobalBillDiscountChanged(decimal value) => RecalculateTotals();
        partial void OnIsTaxInclusiveChanged(bool value) => RecalculateTotals();
        partial void OnAmortizeFocChanged(bool value) => RecalculateTotals();

        [RelayCommand] // Allows the UI DataGrid to trigger this when a user finishes editing a cell
        public void RecalculateTotals()
        {
            if (!GrnLines.Any())
            {
                Subtotal = 0; TotalDiscountAmount = 0; NetPayable = 0;
                return;
            }

            foreach (var line in GrnLines)
            {
                // 1. Line Tax Math (Simplified VAT calculation)
                decimal taxRate = line.TaxCode.Contains("18") ? 0.18m : line.TaxCode.Contains("5") ? 0.05m : 0m;

                decimal rawLineTotal = (line.ReceivedQty * line.UnitCost) - line.LineDiscount;

                if (IsTaxInclusive)
                {
                    // If unit cost already has tax, extract the tax amount backwards
                    line.TaxAmount = rawLineTotal - (rawLineTotal / (1 + taxRate));
                    line.LineTotal = rawLineTotal; // No extra tax added to the bill
                }
                else
                {
                    // If exclusive, calculate tax and add it to the final line total
                    line.TaxAmount = rawLineTotal * taxRate;
                    line.LineTotal = rawLineTotal + line.TaxAmount;
                }
            }

            Subtotal = GrnLines.Sum(l => l.LineTotal);
            decimal lineDiscounts = GrnLines.Sum(l => l.LineDiscount);

            TotalDiscountAmount = lineDiscounts + GlobalBillDiscount;
            NetPayable = Subtotal - GlobalBillDiscount + FreightAmount;

            // 2. Landed Cost Allocation
            decimal totalBaseValue = GrnLines.Sum(l => l.LineTotal);

            if (totalBaseValue > 0)
            {
                foreach (var line in GrnLines)
                {
                    decimal weightPercentage = line.LineTotal / totalBaseValue;
                    decimal allocatedFreight = FreightAmount * weightPercentage;
                    decimal allocatedGlobalDisc = GlobalBillDiscount * weightPercentage;

                    decimal landedLineTotal = line.LineTotal + allocatedFreight - allocatedGlobalDisc;

                    // ENTERPRISE FOC MATH
                    // If Amortize is True, we divide the cost over Received + FOC (Making items cheaper)
                    // If False, we only divide over Received (Cost stays normal, FOC are treated as zero-value bonuses)
                    decimal totalPhysicalQty = line.ReceivedQty + (AmortizeFoc ? line.FocQty : 0m);

                    if (totalPhysicalQty > 0)
                    {
                        line.LandedCost = Math.Round(landedLineTotal / totalPhysicalQty, 2);
                    }
                }
            }
        }

        // --- GRID ACTIONS ---
        [RelayCommand]
        private void RemoveLine(GrnLine line)
        {
            if (line != null)
            {
                GrnLines.Remove(line);
                RecalculateTotals();
            }
        }

        [RelayCommand]
        private void AddMatrix()
        {
            var itemsToAdd = _allMatrixVariants.Where(v => v.ReceivedQty > 0 || v.FocQty > 0).ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show("Please enter a Received Qty for at least one matrix variant.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                if (string.IsNullOrWhiteSpace(item.BatchNo) && !string.IsNullOrWhiteSpace(MatrixBatchNo))
                    item.BatchNo = MatrixBatchNo;

                item.ExpiryDate = MatrixExpiryDate;
                GrnLines.Add(item);
            }

            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();
            SelectedItem = null;
            MatrixFilterText = string.Empty;

            RecalculateTotals();
        }

        // --- POSTING EXECUTION ---
        [RelayCommand]
        private async Task PostGrnAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Supplier.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
            {
                MessageBox.Show("Supplier Invoice Number is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!GrnLines.Any())
            {
                MessageBox.Show("Cannot post an empty GRN. Please add items.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Post GRN for Rs. {NetPayable:N2}? \nThis will irreversibly update Inventory and Supplier Ledgers.",
                                         "Confirm Post", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var header = new GrnHeader
                    {
                        SupplierId = SelectedSupplier.Id,
                        SupplierInvoiceNo = this.SupplierInvoiceNo.Trim(),
                        InvoiceDate = this.InvoiceDate,
                        ReceivedDate = this.ReceivedDate,
                        DueDate = this.DueDate,
                        Remarks = this.Remarks.Trim(),
                        Subtotal = this.Subtotal,
                        GlobalBillDiscount = this.GlobalBillDiscount,
                        FreightAmount = this.FreightAmount,
                        TotalDiscountAmount = this.TotalDiscountAmount,
                        NetPayable = this.NetPayable,
                        IsTaxInclusive = this.IsTaxInclusive
                    };

                    await _grnRepository.PostGrnAsync(header, GrnLines.ToList());

                    MessageBox.Show($"GRN Posted Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Clear();
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.InnerException?.Message ?? ex.Message;
                    MessageBox.Show($"CRITICAL ERROR: Transaction Rolled Back. \n\n{errorMsg}", "System Protection", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedSupplier = null;
            SupplierInvoiceNo = string.Empty;
            Remarks = string.Empty;
            ScanBarcode = string.Empty;
            MatrixFilterText = string.Empty;

            GlobalBillDiscount = 0;
            FreightAmount = 0;
            SelectedItem = null;

            GrnLines.Clear();
            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();

            RecalculateTotals();
            DocumentStatus = "DRAFT";
        }
    }
}
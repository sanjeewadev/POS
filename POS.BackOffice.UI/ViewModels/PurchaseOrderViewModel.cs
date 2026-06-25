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
    public partial class PoMatrixEntryDto : ObservableObject
    {
        public int ItemVariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Uom { get; set; } = string.Empty;
        public decimal CurrentSOH { get; set; } = 0m;

        [ObservableProperty] private decimal _orderQty = 0m;
        [ObservableProperty] private decimal _expectedCost = 0m;
        [ObservableProperty] private decimal _lineDiscount = 0m;

        public string TaxCode { get; set; } = "Exempt";
        public string? SupplierItemCode { get; set; }
        public int Moq { get; set; } = 1;
    }

    public partial class PurchaseOrderViewModel : ObservableObject
    {
        private readonly PoRepository _poRepository;
        private readonly ItemMasterRepository _itemMasterRepository;

        // --- ZONE 1: HEADER ---
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private string _supplierCode = string.Empty;

        [ObservableProperty] private string _selectedTerms = "Credit";
        [ObservableProperty] private int? _creditDaysInput = null;

        [ObservableProperty] private DateTime _orderDate = DateTime.Now;
        [ObservableProperty] private DateTime _expectedDate = DateTime.Now.AddDays(7);
        [ObservableProperty] private string _currentUser = "Admin";
        [ObservableProperty] private string _remarks = string.Empty;

        // --- ZONE 2: ENTRY MODES ---
        [ObservableProperty] private string _scanBarcode = string.Empty;
        [ObservableProperty] private string _matrixFilterText = string.Empty;

        // --- FINANCIAL TOTALS ---
        [ObservableProperty] private decimal _globalBillDiscount = 0m;
        [ObservableProperty] private decimal _subtotal = 0m;
        [ObservableProperty] private decimal _totalDiscountAmount = 0m;
        [ObservableProperty] private decimal _totalTaxAmount = 0m;
        [ObservableProperty] private decimal _netPayable = 0m;

        // --- COLLECTIONS ---
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<PoLine> PoLines { get; set; } = new();
        public ObservableCollection<ItemMasterSummaryDto> AvailableItems { get; set; } = new();

        private List<PoMatrixEntryDto> _allMatrixVariants = new();
        public ObservableCollection<PoMatrixEntryDto> ActiveMatrixVariants { get; set; } = new();

        [ObservableProperty] private PoMatrixEntryDto? _selectedMatrixVariant;
        [ObservableProperty] private PoLine? _selectedLine;
        [ObservableProperty] private ItemMasterSummaryDto? _selectedItem;

        public PurchaseOrderViewModel(PoRepository poRepository, ItemMasterRepository itemMasterRepository)
        {
            _poRepository = poRepository;
            _itemMasterRepository = itemMasterRepository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var suppliers = await _poRepository.GetActiveSuppliersAsync();
            foreach (var sup in suppliers) Suppliers.Add(sup);

            var items = await _itemMasterRepository.GetSummariesAsync();
            foreach (var item in items) AvailableItems.Add(item);
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            SupplierCode = value?.SupplierCode ?? string.Empty;

            if (value != null && value.DefaultCreditDays > 0)
            {
                CreditDaysInput = value.DefaultCreditDays;
            }
        }

        partial void OnSelectedItemChanged(ItemMasterSummaryDto? value)
        {
            if (value == null) return;

            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Supplier first before loading matrix items.", "Supplier Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                SelectedItem = null;
                return;
            }

            _ = LoadVariantsForGridAsync(value.ParentId);
        }

        partial void OnMatrixFilterTextChanged(string value) => ApplyMatrixFilter();

        private async Task LoadVariantsForGridAsync(int parentId)
        {
            _allMatrixVariants.Clear();
            var variants = await _itemMasterRepository.GetVariantsByParentIdAsync(parentId);

            foreach (var variant in variants)
            {
                var supplierLink = variant.ItemSuppliers?.FirstOrDefault(s => s.SupplierId == SelectedSupplier!.Id);
                if (supplierLink == null) continue;

                _allMatrixVariants.Add(new PoMatrixEntryDto
                {
                    ItemVariantId = variant.Id,
                    ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                    VariantDescription = variant.VariantDescription,
                    Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                    Barcode = variant.Barcode,
                    Uom = variant.ItemParent?.BaseUom ?? "PCS",
                    TaxCode = variant.ItemParent?.TaxCode ?? "Exempt",
                    ExpectedCost = supplierLink.LastCostPrice,
                    SupplierItemCode = supplierLink.SupplierItemCode,
                    Moq = supplierLink.MinimumOrderQuantity,
                    OrderQty = 0,
                    CurrentSOH = 0m
                });
            }

            if (!_allMatrixVariants.Any())
            {
                MessageBox.Show("None of the variants for this item are approved for the selected supplier.", "Strict Mode", MessageBoxButton.OK, MessageBoxImage.Information);
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

            foreach (var item in query) ActiveMatrixVariants.Add(item);
        }

        [RelayCommand]
        private async Task AddItemAsync()
        {
            if (string.IsNullOrWhiteSpace(ScanBarcode)) return;

            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Supplier first before scanning items.", "Supplier Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                ScanBarcode = string.Empty;
                return;
            }

            var variant = await _itemMasterRepository.GetItemByBarcodeAsync(ScanBarcode.Trim());

            if (variant == null)
            {
                MessageBox.Show($"Barcode '{ScanBarcode}' not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                ScanBarcode = string.Empty;
                return;
            }

            var supplierLink = variant.ItemSuppliers?.FirstOrDefault(s => s.SupplierId == SelectedSupplier.Id);
            if (supplierLink == null)
            {
                MessageBox.Show($"Strict Mode Blocked: This item ({variant.ItemParent?.ItemName} - {variant.VariantDescription}) is not approved for the selected supplier.", "Invalid Vendor", MessageBoxButton.OK, MessageBoxImage.Error);
                ScanBarcode = string.Empty;
                return;
            }

            var newLine = new PoLine
            {
                ItemVariantId = variant.Id,
                ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                VariantDescription = variant.VariantDescription,
                Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                Barcode = variant.Barcode,
                Uom = variant.ItemParent?.BaseUom ?? "PCS",
                TaxCode = variant.ItemParent?.TaxCode ?? "Exempt",
                ExpectedCost = supplierLink.LastCostPrice,
                SupplierItemCode = supplierLink.SupplierItemCode,
                Moq = supplierLink.MinimumOrderQuantity,
                OrderQty = supplierLink.MinimumOrderQuantity > 0 ? supplierLink.MinimumOrderQuantity : 1
            };

            PoLines.Add(newLine);
            ScanBarcode = string.Empty;
            RecalculateTotals();
        }

        partial void OnGlobalBillDiscountChanged(decimal value) => RecalculateTotals();

        public void RecalculateTotals()
        {
            if (!PoLines.Any())
            {
                Subtotal = 0; TotalDiscountAmount = 0; TotalTaxAmount = 0; NetPayable = 0;
                return;
            }

            foreach (var line in PoLines)
            {
                // Core calculation without complex inclusive/exclusive branching
                decimal taxRate = line.TaxCode.Contains("18") ? 0.18m : line.TaxCode.Contains("5") ? 0.05m : 0m;
                decimal rawLineTotal = (line.OrderQty * line.ExpectedCost) - line.LineDiscount;

                line.TaxAmount = rawLineTotal * taxRate;
                line.LineTotal = rawLineTotal + line.TaxAmount;
            }

            Subtotal = PoLines.Sum(l => l.LineTotal);
            TotalDiscountAmount = PoLines.Sum(l => l.LineDiscount) + GlobalBillDiscount;
            TotalTaxAmount = PoLines.Sum(l => l.TaxAmount);
            NetPayable = Subtotal - GlobalBillDiscount;
        }

        [RelayCommand]
        private void AddMatrix()
        {
            var itemsToAdd = _allMatrixVariants.Where(v => v.OrderQty > 0).ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show("Please enter an Order Qty for at least one matrix variant.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                var newLine = new PoLine
                {
                    ItemVariantId = item.ItemVariantId,
                    ItemCode = item.ItemCode,
                    VariantDescription = item.VariantDescription,
                    Description = item.Description,
                    Barcode = item.Barcode,
                    Uom = item.Uom,
                    OrderQty = item.OrderQty,
                    ExpectedCost = item.ExpectedCost,
                    LineDiscount = item.LineDiscount,
                    TaxCode = item.TaxCode,
                    SupplierItemCode = item.SupplierItemCode,
                    Moq = item.Moq
                };

                PoLines.Add(newLine);
            }

            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();
            SelectedItem = null;
            MatrixFilterText = string.Empty;

            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveLine(PoLine line)
        {
            if (line != null)
            {
                PoLines.Remove(line);
                RecalculateTotals();
            }
        }

        [RelayCommand]
        private async Task SaveOrderAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Supplier.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!PoLines.Any())
            {
                MessageBox.Show("Cannot save an empty Purchase Order.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Save and Finalize Purchase Order?", "Confirm Save", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var header = new PoHeader
                    {
                        SupplierId = SelectedSupplier.Id,
                        OrderDate = this.OrderDate,
                        ExpectedDate = this.ExpectedDate,
                        Terms = this.SelectedTerms,
                        CreditDays = this.CreditDaysInput ?? 0,
                        Remarks = this.Remarks.Trim(),
                        Subtotal = this.Subtotal,
                        GlobalBillDiscount = this.GlobalBillDiscount,
                        TotalTaxAmount = this.TotalTaxAmount,
                        TotalDiscountAmount = this.TotalDiscountAmount,
                        NetPayable = this.NetPayable,
                        CreatedBy = this.CurrentUser
                        // IsTaxInclusive defaults silently to database
                    };

                    await _poRepository.SavePurchaseOrderAsync(header, PoLines.ToList(), false);

                    // PO Saved successfully, trigger PDF download prompt
                    var pdfResult = MessageBox.Show($"Purchase Order Saved Successfully!\n\nWould you like to download the PDF now?", "Success", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (pdfResult == MessageBoxResult.Yes)
                    {
                        GeneratePoPdf(header, PoLines.ToList());
                    }

                    Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Database Error: {ex.Message}", "System Protection", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GeneratePoPdf(PoHeader header, List<PoLine> lines)
        {
            // PDF Generation Library logic will go here
            MessageBox.Show($"PDF Module Initializing...\n\n(This will automatically generate a standard A4 Purchase Order format and launch a save file dialog).", "PDF Generation", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedSupplier = null;
            SupplierCode = string.Empty;
            SelectedTerms = "Credit";
            CreditDaysInput = null;
            Remarks = string.Empty;
            ScanBarcode = string.Empty;
            MatrixFilterText = string.Empty;
            GlobalBillDiscount = 0;
            SelectedItem = null;

            PoLines.Clear();
            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();

            RecalculateTotals();
        }
    }
}
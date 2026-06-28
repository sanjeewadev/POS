using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
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
        public string TaxCode { get; set; } = "TAX-FREE";
        public string SupplierItemCode { get; set; } = string.Empty;
        public int Moq { get; set; } = 1;

        [ObservableProperty]
        private decimal _orderQty = 0m;

        [ObservableProperty]
        private decimal _expectedCost = 0m;

        [ObservableProperty]
        private decimal _lineDiscount = 0m;
    }

    public partial class PurchaseOrderViewModel : ObservableObject
    {
        private readonly PoRepository _poRepository;
        private readonly ItemMasterRepository _itemMasterRepository;

        // =========================================================
        // HEADER
        // =========================================================

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private string _supplierCode = string.Empty;

        [ObservableProperty]
        private string _selectedTerms = "Credit";

        [ObservableProperty]
        private int? _creditDaysInput = null;

        [ObservableProperty]
        private DateTime _orderDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _expectedDate = DateTime.Now.AddDays(7);

        [ObservableProperty]
        private string _currentUser = "Admin";

        [ObservableProperty]
        private string _remarks = string.Empty;

        // =========================================================
        // ENTRY
        // =========================================================

        [ObservableProperty]
        private string _scanBarcode = string.Empty;

        [ObservableProperty]
        private string _matrixFilterText = string.Empty;

        // =========================================================
        // TOTALS
        // =========================================================

        [ObservableProperty]
        private decimal _globalBillDiscount = 0m;

        [ObservableProperty]
        private decimal _subtotal = 0m;

        [ObservableProperty]
        private decimal _totalDiscountAmount = 0m;

        [ObservableProperty]
        private decimal _totalTaxAmount = 0m;

        [ObservableProperty]
        private decimal _netPayable = 0m;

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        public ObservableCollection<PoLine> PoLines { get; } = new();

        public ObservableCollection<ItemMasterSummaryDto> AvailableItems { get; } = new();

        private readonly List<PoMatrixEntryDto> _allMatrixVariants = new();

        public ObservableCollection<PoMatrixEntryDto> ActiveMatrixVariants { get; } = new();

        [ObservableProperty]
        private PoMatrixEntryDto? _selectedMatrixVariant;

        [ObservableProperty]
        private PoLine? _selectedLine;

        [ObservableProperty]
        private ItemMasterSummaryDto? _selectedItem;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public PurchaseOrderViewModel(
            PoRepository poRepository,
            ItemMasterRepository itemMasterRepository)
        {
            _poRepository = poRepository;
            _itemMasterRepository = itemMasterRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                Suppliers.Clear();
                AvailableItems.Clear();

                var suppliers = await _poRepository.GetActiveSuppliersAsync();
                foreach (var supplier in suppliers)
                {
                    Suppliers.Add(supplier);
                }

                var items = await _itemMasterRepository.GetSummariesAsync();
                foreach (var item in items)
                {
                    AvailableItems.Add(item);
                }

                StatusMessage = "Purchase Order page loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize Purchase Order page.";

                MessageBox.Show(
                    $"Failed to initialize Purchase Order page:\n\n{ex.Message}",
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
        // HEADER EVENTS
        // =========================================================

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            SupplierCode = value?.SupplierCode ?? string.Empty;

            if (value != null && value.DefaultCreditDays > 0)
            {
                CreditDaysInput = value.DefaultCreditDays;
            }

            ClearLoadedMatrixOnly();

            StatusMessage = value == null
                ? "Select a supplier before adding items."
                : $"Selected supplier: {value.SupplierName}";
        }

        partial void OnSelectedTermsChanged(string value)
        {
            if (!string.Equals(value, "Credit", StringComparison.OrdinalIgnoreCase))
            {
                CreditDaysInput = 0;
            }
            else if (SelectedSupplier != null && SelectedSupplier.DefaultCreditDays > 0)
            {
                CreditDaysInput = SelectedSupplier.DefaultCreditDays;
            }
        }

        partial void OnGlobalBillDiscountChanged(decimal value)
        {
            RecalculateTotals();
        }

        // =========================================================
        // ITEM / MATRIX LOADING
        // =========================================================

        partial void OnSelectedItemChanged(ItemMasterSummaryDto? value)
        {
            if (value == null)
                return;

            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier first before loading items.",
                    "Supplier Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SelectedItem = null;
                return;
            }

            _ = LoadVariantsForGridAsync(value.ParentId);
        }

        private async Task LoadVariantsForGridAsync(int parentId)
        {
            IsBusy = true;

            try
            {
                _allMatrixVariants.Clear();
                ActiveMatrixVariants.Clear();

                var variants = await _itemMasterRepository.GetVariantsByParentIdAsync(parentId);

                foreach (var variant in variants)
                {
                    if (variant.ItemParent == null)
                        continue;

                    if (variant.ItemParent.IsPurchaseLocked)
                        continue;

                    var supplierLink = variant.ItemSuppliers?
                        .FirstOrDefault(s => s.SupplierId == SelectedSupplier!.Id);

                    if (supplierLink == null)
                        continue;

                    _allMatrixVariants.Add(new PoMatrixEntryDto
                    {
                        ItemVariantId = variant.Id,
                        ItemCode = variant.ItemParent.ItemCode,
                        VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                            ? "Standard"
                            : variant.VariantDescription,
                        Description = variant.ItemParent.ItemName,
                        Barcode = variant.Barcode,
                        Uom = string.IsNullOrWhiteSpace(variant.ItemParent.BaseUom)
                            ? "PCS"
                            : variant.ItemParent.BaseUom,
                        TaxCode = string.IsNullOrWhiteSpace(variant.ItemParent.TaxCode)
                            ? "TAX-FREE"
                            : variant.ItemParent.TaxCode,
                        ExpectedCost = supplierLink.LastCostPrice > 0
                            ? supplierLink.LastCostPrice
                            : variant.CostPrice,
                        SupplierItemCode = supplierLink.SupplierItemCode ?? string.Empty,
                        Moq = supplierLink.MinimumOrderQuantity <= 0
                            ? 1
                            : supplierLink.MinimumOrderQuantity,
                        OrderQty = 0,
                        CurrentSOH = 0m
                    });
                }

                if (!_allMatrixVariants.Any())
                {
                    MessageBox.Show(
                        "None of the variants for this item are approved for the selected supplier.",
                        "No Supplier-Approved Variants",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                ApplyMatrixFilter();

                StatusMessage = $"{ActiveMatrixVariants.Count} supplier-approved variant(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load item variants.";

                MessageBox.Show(
                    $"Failed to load item variants:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnMatrixFilterTextChanged(string value)
        {
            ApplyMatrixFilter();
        }

        private void ApplyMatrixFilter()
        {
            ActiveMatrixVariants.Clear();

            IEnumerable<PoMatrixEntryDto> query = _allMatrixVariants;

            if (!string.IsNullOrWhiteSpace(MatrixFilterText))
            {
                string search = MatrixFilterText.Trim().ToLowerInvariant();

                query = query.Where(v =>
                    v.VariantDescription.ToLowerInvariant().Contains(search) ||
                    v.Barcode.ToLowerInvariant().Contains(search) ||
                    v.SupplierItemCode.ToLowerInvariant().Contains(search));
            }

            foreach (var item in query)
            {
                ActiveMatrixVariants.Add(item);
            }
        }

        // =========================================================
        // ADD ITEM BY BARCODE / SKU
        // =========================================================

        [RelayCommand]
        private async Task AddItemAsync()
        {
            string term = (ScanBarcode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
                return;

            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier first before scanning items.",
                    "Supplier Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ScanBarcode = string.Empty;
                return;
            }

            try
            {
                var variant = await _itemMasterRepository.GetItemByBarcodeAsync(term);

                if (variant == null)
                {
                    MessageBox.Show(
                        $"Barcode/SKU '{term}' was not found.",
                        "Item Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ScanBarcode = string.Empty;
                    return;
                }

                if (variant.ItemParent?.IsPurchaseLocked == true)
                {
                    MessageBox.Show(
                        "This item is purchase locked.",
                        "Purchase Locked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ScanBarcode = string.Empty;
                    return;
                }

                var supplierLink = variant.ItemSuppliers?
                    .FirstOrDefault(s => s.SupplierId == SelectedSupplier.Id);

                if (supplierLink == null)
                {
                    MessageBox.Show(
                        $"This item is not approved for supplier '{SelectedSupplier.SupplierName}'.",
                        "Invalid Supplier",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ScanBarcode = string.Empty;
                    return;
                }

                var newLine = BuildPoLineFromVariant(variant, supplierLink);

                MergeOrAddLine(newLine);

                ScanBarcode = string.Empty;
                RecalculateTotals();

                StatusMessage = "Item added to Purchase Order.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to add item.";

                MessageBox.Show(
                    $"Failed to add item:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static PoLine BuildPoLineFromVariant(
            ItemVariant variant,
            ItemSupplier supplierLink)
        {
            int moq = supplierLink.MinimumOrderQuantity <= 0
                ? 1
                : supplierLink.MinimumOrderQuantity;

            return new PoLine
            {
                ItemVariantId = variant.Id,
                ItemCode = variant.ItemParent?.ItemCode ?? string.Empty,
                VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                    ? "Standard"
                    : variant.VariantDescription,
                Description = variant.ItemParent?.ItemName ?? string.Empty,
                Barcode = variant.Barcode,
                Uom = string.IsNullOrWhiteSpace(variant.ItemParent?.BaseUom)
                    ? "PCS"
                    : variant.ItemParent.BaseUom,
                TaxCode = string.IsNullOrWhiteSpace(variant.ItemParent?.TaxCode)
                    ? "TAX-FREE"
                    : variant.ItemParent.TaxCode,
                ExpectedCost = supplierLink.LastCostPrice > 0
                    ? supplierLink.LastCostPrice
                    : variant.CostPrice,
                SupplierItemCode = supplierLink.SupplierItemCode ?? string.Empty,
                Moq = moq,
                OrderQty = moq
            };
        }

        // =========================================================
        // ADD MATRIX ITEMS
        // =========================================================

        [RelayCommand]
        private void AddMatrix()
        {
            var itemsToAdd = _allMatrixVariants
                .Where(v => v.OrderQty > 0)
                .ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show(
                    "Please enter an order quantity for at least one matrix variant.",
                    "No Quantity",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var belowMoq = itemsToAdd
                .FirstOrDefault(v => v.Moq > 0 && v.OrderQty < v.Moq);

            if (belowMoq != null)
            {
                MessageBox.Show(
                    $"Variant '{belowMoq.VariantDescription}' has MOQ {belowMoq.Moq}. Order quantity cannot be lower than MOQ.",
                    "MOQ Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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

                MergeOrAddLine(newLine);
            }

            ClearLoadedMatrixOnly();
            RecalculateTotals();

            StatusMessage = "Matrix items added to Purchase Order.";
        }

        private void MergeOrAddLine(PoLine newLine)
        {
            var existing = PoLines.FirstOrDefault(l =>
                l.ItemVariantId == newLine.ItemVariantId);

            if (existing == null)
            {
                PoLines.Add(newLine);
                return;
            }

            existing.OrderQty += newLine.OrderQty;

            if (newLine.ExpectedCost > 0)
                existing.ExpectedCost = newLine.ExpectedCost;

            if (!string.IsNullOrWhiteSpace(newLine.SupplierItemCode))
                existing.SupplierItemCode = newLine.SupplierItemCode;

            existing.Moq = newLine.Moq;
        }

        [RelayCommand]
        private void RemoveLine(PoLine? line)
        {
            if (line == null)
                return;

            PoLines.Remove(line);
            RecalculateTotals();

            StatusMessage = "Line removed.";
        }

        // =========================================================
        // TOTALS
        // =========================================================

        public void RecalculateTotals()
        {
            if (!PoLines.Any())
            {
                Subtotal = 0m;
                TotalDiscountAmount = 0m;
                TotalTaxAmount = 0m;
                NetPayable = 0m;
                RefreshPoLineGrid();
                return;
            }

            decimal subtotal = 0m;
            decimal lineDiscountTotal = 0m;
            decimal taxTotal = 0m;
            decimal lineNetTotal = 0m;

            foreach (var line in PoLines)
            {
                decimal taxRate = GetTaxRate(line.TaxCode);
                decimal gross = line.OrderQty * line.ExpectedCost;
                decimal afterLineDiscount = gross - line.LineDiscount;

                if (afterLineDiscount < 0)
                    afterLineDiscount = 0;

                line.TaxAmount = Math.Round(afterLineDiscount * taxRate, 2);
                line.LineTotal = Math.Round(afterLineDiscount + line.TaxAmount, 2);

                subtotal += gross;
                lineDiscountTotal += line.LineDiscount;
                taxTotal += line.TaxAmount;
                lineNetTotal += line.LineTotal;
            }

            Subtotal = Math.Round(subtotal, 2);
            TotalDiscountAmount = Math.Round(lineDiscountTotal + GlobalBillDiscount, 2);
            TotalTaxAmount = Math.Round(taxTotal, 2);

            decimal net = lineNetTotal - GlobalBillDiscount;
            NetPayable = Math.Round(net < 0 ? 0 : net, 2);

            RefreshPoLineGrid();
        }

        private void RefreshPoLineGrid()
        {
            CollectionViewSource.GetDefaultView(PoLines)?.Refresh();
        }

        private static decimal GetTaxRate(string taxCode)
        {
            string value = (taxCode ?? string.Empty).Trim().ToUpperInvariant();

            if (value.Contains("18"))
                return 0.18m;

            if (value.Contains("5"))
                return 0.05m;

            return 0m;
        }

        // =========================================================
        // SAVE
        // =========================================================

        [RelayCommand]
        private async Task SaveOrderAsync()
        {
            if (!ValidateBeforeSave())
                return;

            var result = MessageBox.Show(
                "Save and approve this Purchase Order?",
                "Confirm Save",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                RecalculateTotals();

                var header = new PoHeader
                {
                    SupplierId = SelectedSupplier!.Id,
                    OrderDate = OrderDate,
                    ExpectedDate = ExpectedDate,
                    Terms = SelectedTerms,
                    CreditDays = CreditDaysInput ?? 0,
                    Remarks = Remarks.Trim(),
                    Subtotal = Subtotal,
                    GlobalBillDiscount = GlobalBillDiscount,
                    TotalTaxAmount = TotalTaxAmount,
                    TotalDiscountAmount = TotalDiscountAmount,
                    NetPayable = NetPayable,
                    CreatedBy = CurrentUser,
                    ApprovedBy = CurrentUser,
                    IsTaxInclusive = false
                };

                await _poRepository.SavePurchaseOrderAsync(
                    header,
                    PoLines.ToList(),
                    isDraft: false);

                var pdfResult = MessageBox.Show(
                    "Purchase Order saved successfully.\n\nPDF generation can be added after the PO save flow is stable.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Clear();
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Save blocked.";

                MessageBox.Show(
                    ex.Message,
                    "Save Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                StatusMessage = "Save failed.";

                MessageBox.Show(
                    $"Failed to save Purchase Order:\n\n{message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidateBeforeSave()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (ExpectedDate.Date < OrderDate.Date)
            {
                MessageBox.Show(
                    "Expected date cannot be before order date.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (CreditDaysInput.HasValue &&
                (CreditDaysInput.Value < 0 || CreditDaysInput.Value > 365))
            {
                MessageBox.Show(
                    "Credit days must be between 0 and 365.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!PoLines.Any())
            {
                MessageBox.Show(
                    "Cannot save an empty Purchase Order.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (GlobalBillDiscount < 0)
            {
                MessageBox.Show(
                    "Global bill discount cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            foreach (var line in PoLines)
            {
                if (line.OrderQty <= 0)
                {
                    MessageBox.Show(
                        $"Order quantity must be greater than zero for item '{line.Description}'.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (line.Moq > 0 && line.OrderQty < line.Moq)
                {
                    MessageBox.Show(
                        $"Item '{line.Description} / {line.VariantDescription}' has MOQ {line.Moq}.",
                        "MOQ Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (line.ExpectedCost <= 0)
                {
                    MessageBox.Show(
                        $"Expected cost must be greater than zero for item '{line.Description}'.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (line.LineDiscount < 0)
                {
                    MessageBox.Show(
                        $"Line discount cannot be negative for item '{line.Description}'.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                decimal gross = line.OrderQty * line.ExpectedCost;

                if (line.LineDiscount > gross)
                {
                    MessageBox.Show(
                        $"Line discount cannot be greater than line value for item '{line.Description}'.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        // =========================================================
        // CLEAR
        // =========================================================

        [RelayCommand]
        private void Clear()
        {
            SelectedSupplier = null;
            SupplierCode = string.Empty;
            SelectedTerms = "Credit";
            CreditDaysInput = null;

            OrderDate = DateTime.Now;
            ExpectedDate = DateTime.Now.AddDays(7);

            Remarks = string.Empty;
            ScanBarcode = string.Empty;
            MatrixFilterText = string.Empty;
            GlobalBillDiscount = 0m;
            SelectedItem = null;
            SelectedLine = null;

            PoLines.Clear();
            ClearLoadedMatrixOnly();

            RecalculateTotals();

            StatusMessage = "Ready for new Purchase Order.";
        }

        private void ClearLoadedMatrixOnly()
        {
            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();
            MatrixFilterText = string.Empty;
            SelectedItem = null;
            SelectedMatrixVariant = null;
        }
    }
}
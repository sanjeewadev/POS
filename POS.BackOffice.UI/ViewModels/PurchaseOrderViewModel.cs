using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PoMatrixEntryDto : ObservableObject
    {
        private bool _isRecalculating;

        public int ItemVariantId { get; set; }

        public int ItemParentId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PrintName { get; set; } = string.Empty;

        public string Uom { get; set; } = string.Empty;

        public decimal CurrentSOH { get; set; } = 0m;

        public string TaxCode { get; set; } = "VAT";

        public string SupplierItemCode { get; set; } = string.Empty;

        public int Moq { get; set; } = 1;

        public bool AllowDecimalQuantity { get; set; } = false;

        public string FullDisplayName =>
            PoVmDisplayNameHelper.BuildDisplayName(Description, VariantDescription, SkuCode);

        public string ReceiptDisplayName =>
            PoVmDisplayNameHelper.BuildDisplayName(
                string.IsNullOrWhiteSpace(PrintName) ? Description : PrintName,
                VariantDescription,
                FullDisplayName);

        public string DisplayName => FullDisplayName;

        public string VariantDisplayName =>
            PoVmDisplayNameHelper.IsStandardVariantDescription(VariantDescription)
                ? "Standard"
                : PoVmDisplayNameHelper.NormalizeText(VariantDescription);

        public decimal GrossAmount => Math.Round(OrderQty * ExpectedCost, 2);

        public decimal NetBeforeVat
        {
            get
            {
                decimal net = GrossAmount - LineDiscount;
                return net < 0 ? 0m : Math.Round(net, 2);
            }
        }

        [ObservableProperty]
        private decimal _orderQty = 0m;

        [ObservableProperty]
        private decimal _expectedCost = 0m;

        [ObservableProperty]
        private string _lineDiscountMode = "Amount";

        [ObservableProperty]
        private decimal _lineDiscountValue = 0m;

        [ObservableProperty]
        private decimal _lineDiscount = 0m;

        [ObservableProperty]
        private decimal _vatRatePercent = 0m;

        [ObservableProperty]
        private bool _isVatIncluded = false;

        [ObservableProperty]
        private decimal _vatAmount = 0m;

        [ObservableProperty]
        private decimal _lineTotal = 0m;

        public void RecalculateLineAmounts()
        {
            if (_isRecalculating)
                return;

            _isRecalculating = true;

            try
            {
                decimal gross = GrossAmount;

                LineDiscount = CalculateDiscountAmount(
                    gross,
                    LineDiscountMode,
                    LineDiscountValue);

                decimal afterDiscount = gross - LineDiscount;

                if (afterDiscount < 0)
                    afterDiscount = 0m;

                decimal vatRate = VatRatePercent / 100m;

                if (VatRatePercent <= 0)
                {
                    VatAmount = 0m;
                    LineTotal = Math.Round(afterDiscount, 2);
                }
                else if (IsVatIncluded)
                {
                    VatAmount = Math.Round(
                        afterDiscount - (afterDiscount / (1 + vatRate)),
                        2);

                    LineTotal = Math.Round(afterDiscount, 2);
                }
                else
                {
                    VatAmount = Math.Round(afterDiscount * vatRate, 2);
                    LineTotal = Math.Round(afterDiscount + VatAmount, 2);
                }
            }
            finally
            {
                _isRecalculating = false;
            }

            OnPropertyChanged(nameof(GrossAmount));
            OnPropertyChanged(nameof(NetBeforeVat));
        }

        partial void OnOrderQtyChanged(decimal value)
        {
            RecalculateLineAmounts();
        }

        partial void OnExpectedCostChanged(decimal value)
        {
            RecalculateLineAmounts();
        }

        partial void OnLineDiscountModeChanged(string value)
        {
            RecalculateLineAmounts();
        }

        partial void OnLineDiscountValueChanged(decimal value)
        {
            RecalculateLineAmounts();
        }

        partial void OnLineDiscountChanged(decimal value)
        {
            if (_isRecalculating)
                return;

            if (IsAmountDiscount(LineDiscountMode))
                LineDiscountValue = value;

            RecalculateLineAmounts();
        }

        partial void OnVatRatePercentChanged(decimal value)
        {
            RecalculateLineAmounts();
        }

        partial void OnIsVatIncludedChanged(bool value)
        {
            RecalculateLineAmounts();
        }

        private static decimal CalculateDiscountAmount(
            decimal gross,
            string? discountMode,
            decimal discountValue)
        {
            if (gross <= 0 || discountValue <= 0)
                return 0m;

            if (IsPercentDiscount(discountMode))
                return Math.Round(gross * discountValue / 100m, 2);

            return Math.Round(discountValue, 2);
        }

        private static bool IsAmountDiscount(string? value)
        {
            return NormalizeDiscountMode(value) == "Amount";
        }

        private static bool IsPercentDiscount(string? value)
        {
            return NormalizeDiscountMode(value) == "Percent";
        }

        private static string NormalizeDiscountMode(string? value)
        {
            string mode = (value ?? string.Empty).Trim();

            if (mode.Equals("Percent", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("%", StringComparison.OrdinalIgnoreCase))
            {
                return "Percent";
            }

            return "Amount";
        }
    }

    public partial class PurchaseOrderViewModel : ObservableObject
    {
        private readonly PoRepository _poRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;
        private bool _isClearing;

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

        [ObservableProperty]
        private decimal _bulkMatrixQuantity = 0m;

        [ObservableProperty]
        private decimal _bulkMatrixExpectedCost = 0m;

        [ObservableProperty]
        private bool _bulkMatrixVatIncluded = false;

        // =========================================================
        // BULK DISCOUNT / VAT
        // =========================================================

        public ObservableCollection<string> DiscountModes { get; } = new(new[]
        {
            "Amount",
            "Percent"
        });

        [ObservableProperty]
        private string _bulkDiscountMode = "Amount";

        [ObservableProperty]
        private decimal _bulkDiscountValue = 0m;

        // Kept for older XAML/logic compatibility.
        [ObservableProperty]
        private decimal _bulkVatRatePercent = 0m;

        // Kept for older XAML/logic compatibility.
        [ObservableProperty]
        private bool _bulkIsVatIncluded = false;

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

        public bool IsSupplierSelected => SelectedSupplier != null;

        public bool IsEntryEnabled => SelectedSupplier != null && !IsBusy;

        public PurchaseOrderViewModel(
            PoRepository poRepository,
            IMessageBoxService messageBoxService)
        {
            _poRepository = poRepository ?? throw new ArgumentNullException(nameof(poRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));

            PoLines.CollectionChanged += (_, _) =>
            {
                SaveOrderCommand.NotifyCanExecuteChanged();
                ApplyBulkDiscountModeToLinesCommand.NotifyCanExecuteChanged();
                ApplyBulkDiscountValueToLinesCommand.NotifyCanExecuteChanged();
            };

            ActiveMatrixVariants.CollectionChanged += (_, _) =>
            {
                ApplyBulkMatrixQuantityCommand.NotifyCanExecuteChanged();
                ApplyBulkMatrixExpectedCostCommand.NotifyCanExecuteChanged();
            };
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            IsBusy = true;

            try
            {
                Suppliers.Clear();
                AvailableItems.Clear();
                ClearLoadedMatrixOnly();

                var suppliers = await _poRepository.GetActiveSuppliersAsync();

                foreach (var supplier in suppliers)
                    Suppliers.Add(supplier);

                _isInitialized = true;
                StatusMessage = "Purchase Order page loaded. Select a supplier first.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize Purchase Order page.";

                _messageBoxService.ShowError(
                    $"Failed to initialize Purchase Order page:\n\n{ex.Message}",
                    "Database Error");
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
            OnPropertyChanged(nameof(IsSupplierSelected));
            OnPropertyChanged(nameof(IsEntryEnabled));

            SupplierCode = value?.SupplierCode ?? string.Empty;

            if (_isClearing)
                return;

            if (value != null && value.DefaultCreditDays > 0)
                CreditDaysInput = value.DefaultCreditDays;

            if (PoLines.Any())
            {
                PoLines.Clear();
                RecalculateTotals();

                _messageBoxService.ShowInformation(
                    "PO lines were cleared because the supplier was changed.",
                    "Supplier Changed");
            }

            ClearLoadedMatrixOnly();

            if (value == null)
            {
                AvailableItems.Clear();
                StatusMessage = "Select a supplier before adding items.";
                NotifyCommandStates();
                return;
            }

            StatusMessage = $"Selected supplier: {value.SupplierName}. Loading approved items.";
            _ = LoadSupplierApprovedItemsAsync(value.Id);

            NotifyCommandStates();
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

        partial void OnMatrixFilterTextChanged(string value)
        {
            ApplyMatrixFilter();
        }

        partial void OnSelectedItemChanged(ItemMasterSummaryDto? value)
        {
            if (_isClearing)
                return;

            if (value == null)
                return;

            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier first before loading items.",
                    "Supplier Required");

                SelectedItem = null;
                return;
            }

            _ = LoadVariantsForGridAsync(value.ParentId);
        }

        partial void OnBulkMatrixQuantityChanged(decimal value)
        {
            ApplyBulkMatrixQuantityCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkMatrixExpectedCostChanged(decimal value)
        {
            ApplyBulkMatrixExpectedCostCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkMatrixVatIncludedChanged(bool value)
        {
            foreach (var item in ActiveMatrixVariants)
            {
                item.IsVatIncluded = value;
                item.RecalculateLineAmounts();
            }

            StatusMessage = value
                ? "VAT included applied to visible matrix rows."
                : "VAT included removed from visible matrix rows.";
        }

        partial void OnBulkDiscountModeChanged(string value)
        {
            ApplyBulkDiscountModeToLinesCommand.NotifyCanExecuteChanged();
            ApplyBulkDiscountValueToLinesCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkDiscountValueChanged(decimal value)
        {
            ApplyBulkDiscountValueToLinesCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsEntryEnabled));

            NotifyCommandStates();
        }

        // =========================================================
        // SUPPLIER-APPROVED ITEM LOADING
        // =========================================================

        private async Task LoadSupplierApprovedItemsAsync(int supplierId)
        {
            if (supplierId <= 0)
                return;

            IsBusy = true;

            try
            {
                AvailableItems.Clear();

                var items = await _poRepository.GetSupplierApprovedItemSummariesAsync(supplierId);

                foreach (var item in items)
                    AvailableItems.Add(item);

                StatusMessage = $"{AvailableItems.Count} supplier-approved item parent(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load supplier-approved items.";

                _messageBoxService.ShowError(
                    $"Failed to load supplier-approved items:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadVariantsForGridAsync(int parentId)
        {
            if (SelectedSupplier == null)
                return;

            IsBusy = true;

            try
            {
                _allMatrixVariants.Clear();
                ActiveMatrixVariants.Clear();
                SelectedMatrixVariant = null;

                var variants = await _poRepository.GetSupplierApprovedVariantsByParentAsync(
                    parentId,
                    SelectedSupplier.Id);

                foreach (var variant in variants)
                {
                    var item = new PoMatrixEntryDto
                    {
                        ItemVariantId = variant.ItemVariantId,
                        ItemParentId = variant.ItemParentId,
                        ItemCode = variant.ItemCode,
                        SkuCode = variant.SkuCode,
                        Barcode = variant.Barcode,
                        Description = variant.Description,
                        PrintName = variant.PrintName,
                        VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                            ? "Standard"
                            : variant.VariantDescription,
                        Uom = string.IsNullOrWhiteSpace(variant.Uom) ? "PCS" : variant.Uom,
                        TaxCode = string.IsNullOrWhiteSpace(variant.TaxCode) ? "VAT" : variant.TaxCode,
                        SupplierItemCode = variant.SupplierItemCode,
                        Moq = variant.Moq <= 0 ? 1 : variant.Moq,
                        AllowDecimalQuantity = variant.AllowDecimalQuantity,
                        CurrentSOH = variant.CurrentSOH,
                        OrderQty = 0m,
                        ExpectedCost = variant.LastSupplierCost > 0
                            ? variant.LastSupplierCost
                            : variant.CurrentCost,
                        LineDiscountMode = "Amount",
                        LineDiscountValue = 0m,
                        VatRatePercent = variant.VatRatePercent,
                        IsVatIncluded = BulkMatrixVatIncluded || variant.IsVatIncluded
                    };

                    item.RecalculateLineAmounts();
                    _allMatrixVariants.Add(item);
                }

                ApplyMatrixFilter();

                if (!_allMatrixVariants.Any())
                {
                    _messageBoxService.ShowInformation(
                        "None of the variants for this item are approved for the selected supplier.",
                        "No Supplier-Approved Variants");
                }

                StatusMessage = $"{ActiveMatrixVariants.Count} supplier-approved variant(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load item variants.";

                _messageBoxService.ShowError(
                    $"Failed to load item variants:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyMatrixFilter()
        {
            ActiveMatrixVariants.Clear();

            IEnumerable<PoMatrixEntryDto> query = _allMatrixVariants;

            if (!string.IsNullOrWhiteSpace(MatrixFilterText))
            {
                string search = MatrixFilterText.Trim().ToLowerInvariant();

                query = query.Where(v =>
                    SafeLower(v.DisplayName).Contains(search) ||
                    SafeLower(v.ItemCode).Contains(search) ||
                    SafeLower(v.SkuCode).Contains(search) ||
                    SafeLower(v.Barcode).Contains(search) ||
                    SafeLower(v.VariantDescription).Contains(search) ||
                    SafeLower(v.SupplierItemCode).Contains(search));
            }

            foreach (var item in query)
                ActiveMatrixVariants.Add(item);

            SelectedMatrixVariant = ActiveMatrixVariants.FirstOrDefault();

            NotifyCommandStates();
        }

        // =========================================================
        // ADD ITEM BY BARCODE / SKU
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task AddItemAsync()
        {
            string term = (ScanBarcode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
                return;

            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier first before scanning items.",
                    "Supplier Required");

                ScanBarcode = string.Empty;
                return;
            }

            IsBusy = true;

            try
            {
                var variant = await _poRepository.GetSupplierApprovedVariantByBarcodeOrSkuAsync(
                    term,
                    SelectedSupplier.Id);

                if (variant == null)
                {
                    _messageBoxService.ShowWarning(
                        $"Barcode/SKU '{term}' was not found or is not approved for supplier '{SelectedSupplier.SupplierName}'.",
                        "Item Not Found");

                    ScanBarcode = string.Empty;
                    return;
                }

                var newLine = BuildPoLineFromVariant(
                    variant,
                    variant.Moq <= 0 ? 1 : variant.Moq);

                MergeOrAddLine(newLine);

                ScanBarcode = string.Empty;
                RecalculateTotals();

                StatusMessage = "Item added to Purchase Order.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to add item.";

                _messageBoxService.ShowError(
                    $"Failed to add item:\n\n{ex.Message}",
                    "Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static PoLine BuildPoLineFromVariant(
            PoVariantLookupDto variant,
            decimal qty)
        {
            decimal orderQty = qty <= 0
                ? variant.Moq <= 0 ? 1m : variant.Moq
                : qty;

            decimal expectedCost = variant.LastSupplierCost > 0
                ? variant.LastSupplierCost
                : variant.CurrentCost;

            var line = new PoLine
            {
                ItemVariantId = variant.ItemVariantId,
                ItemCode = variant.ItemCode,
                SkuCode = variant.SkuCode,
                VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                    ? "Standard"
                    : variant.VariantDescription,
                Description = variant.Description,
                PrintName = variant.PrintName,
                Barcode = variant.Barcode,
                Uom = string.IsNullOrWhiteSpace(variant.Uom) ? "PCS" : variant.Uom,
                TaxCode = string.IsNullOrWhiteSpace(variant.TaxCode) ? "VAT" : variant.TaxCode,
                SupplierItemCode = variant.SupplierItemCode,
                Moq = variant.Moq <= 0 ? 1 : variant.Moq,
                OrderQty = orderQty,
                ExpectedCost = expectedCost,
                LineDiscountMode = "Amount",
                LineDiscountValue = 0m,
                LineDiscount = 0m,
                VatRatePercent = variant.VatRatePercent,
                IsVatIncluded = variant.IsVatIncluded,
                TaxAmount = 0m,
                LineTotal = 0m,
                LineStatus = "Open"
            };

            RecalculateLine(line);

            return line;
        }

        private static PoLine BuildPoLineFromMatrix(PoMatrixEntryDto item)
        {
            item.RecalculateLineAmounts();

            var line = new PoLine
            {
                ItemVariantId = item.ItemVariantId,
                ItemCode = item.ItemCode,
                SkuCode = item.SkuCode,
                VariantDescription = string.IsNullOrWhiteSpace(item.VariantDescription)
                    ? "Standard"
                    : item.VariantDescription,
                Description = item.Description,
                PrintName = item.PrintName,
                Barcode = item.Barcode,
                Uom = string.IsNullOrWhiteSpace(item.Uom) ? "PCS" : item.Uom,
                TaxCode = string.IsNullOrWhiteSpace(item.TaxCode) ? "VAT" : item.TaxCode,
                SupplierItemCode = item.SupplierItemCode,
                Moq = item.Moq <= 0 ? 1 : item.Moq,
                OrderQty = item.OrderQty,
                ExpectedCost = item.ExpectedCost,
                LineDiscountMode = NormalizeDiscountMode(item.LineDiscountMode),
                LineDiscountValue = item.LineDiscountValue,
                LineDiscount = item.LineDiscount,
                VatRatePercent = item.VatRatePercent,
                IsVatIncluded = item.IsVatIncluded,
                TaxAmount = item.VatAmount,
                LineTotal = item.LineTotal,
                LineStatus = "Open"
            };

            RecalculateLine(line);

            return line;
        }

        // =========================================================
        // ADD MATRIX ITEMS
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void AddMatrix()
        {
            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier first.",
                    "Supplier Required");

                return;
            }

            var itemsToAdd = _allMatrixVariants
                .Where(v => v.OrderQty > 0)
                .ToList();

            if (!itemsToAdd.Any())
            {
                _messageBoxService.ShowWarning(
                    "Please enter an order quantity for at least one matrix variant.",
                    "No Quantity");

                return;
            }

            foreach (var item in itemsToAdd)
            {
                var validationError = ValidateMatrixLine(item);

                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    _messageBoxService.ShowWarning(
                        validationError,
                        "Validation Error");

                    return;
                }
            }

            foreach (var item in itemsToAdd)
            {
                var newLine = BuildPoLineFromMatrix(item);
                MergeOrAddLine(newLine);
            }

            ClearLoadedMatrixOnly();
            RecalculateTotals();

            StatusMessage = "Matrix items added to Purchase Order.";
        }

        private static string ValidateMatrixLine(PoMatrixEntryDto item)
        {
            if (item.OrderQty <= 0)
                return $"{item.DisplayName}: order quantity must be greater than zero.";

            if (!item.AllowDecimalQuantity && HasDecimalPart(item.OrderQty))
                return $"{item.DisplayName}: decimal quantity is not allowed for UOM '{item.Uom}'.";

            if (item.Moq > 0 && item.OrderQty < item.Moq)
                return $"{item.DisplayName}: minimum order quantity is {item.Moq}.";

            if (item.ExpectedCost <= 0)
                return $"{item.DisplayName}: expected cost must be greater than zero.";

            if (!IsValidDiscountMode(item.LineDiscountMode))
                return $"{item.DisplayName}: discount mode must be Amount or Percent.";

            if (item.LineDiscountValue < 0)
                return $"{item.DisplayName}: discount value cannot be negative.";

            if (IsPercentDiscount(item.LineDiscountMode) && item.LineDiscountValue > 100)
                return $"{item.DisplayName}: discount percentage cannot be greater than 100.";

            if (item.VatRatePercent < 0 || item.VatRatePercent > 100)
                return $"{item.DisplayName}: VAT rate must be between 0 and 100.";

            item.RecalculateLineAmounts();

            if (item.LineDiscount > item.GrossAmount)
                return $"{item.DisplayName}: discount cannot be greater than line value.";

            return string.Empty;
        }

        private void MergeOrAddLine(PoLine newLine)
        {
            var existing = PoLines.FirstOrDefault(l =>
                l.ItemVariantId == newLine.ItemVariantId);

            if (existing == null)
            {
                PoLines.Add(newLine);
                SaveOrderCommand.NotifyCanExecuteChanged();
                return;
            }

            existing.OrderQty += newLine.OrderQty;

            if (newLine.ExpectedCost > 0)
                existing.ExpectedCost = newLine.ExpectedCost;

            if (!string.IsNullOrWhiteSpace(newLine.SupplierItemCode))
                existing.SupplierItemCode = newLine.SupplierItemCode;

            existing.LineDiscountMode = newLine.LineDiscountMode;
            existing.LineDiscountValue = newLine.LineDiscountValue;
            existing.VatRatePercent = newLine.VatRatePercent;
            existing.IsVatIncluded = newLine.IsVatIncluded;
            existing.Moq = newLine.Moq;

            RecalculateLine(existing);
            RefreshPoLineGrid();

            SaveOrderCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void RemoveLine(PoLine? line)
        {
            if (line == null)
                return;

            PoLines.Remove(line);
            SelectedLine = null;
            RecalculateTotals();

            StatusMessage = "Line removed.";

            SaveOrderCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // MATRIX BULK APPLY COMMANDS
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ApplyBulkMatrixQuantity()
        {
            if (!ActiveMatrixVariants.Any())
            {
                _messageBoxService.ShowWarning(
                    "No matrix variants are loaded.",
                    "No Items");

                return;
            }

            if (BulkMatrixQuantity < 0)
            {
                _messageBoxService.ShowWarning(
                    "Bulk quantity cannot be negative.",
                    "Validation Error");

                return;
            }

            var decimalBlocked = ActiveMatrixVariants
                .FirstOrDefault(v =>
                    !v.AllowDecimalQuantity &&
                    HasDecimalPart(BulkMatrixQuantity));

            if (decimalBlocked != null)
            {
                _messageBoxService.ShowWarning(
                    $"Decimal quantity is not allowed for '{decimalBlocked.DisplayName}' with UOM '{decimalBlocked.Uom}'.",
                    "Validation Error");

                return;
            }

            foreach (var item in ActiveMatrixVariants)
            {
                item.OrderQty = BulkMatrixQuantity;
                item.RecalculateLineAmounts();
            }

            StatusMessage = $"Bulk quantity {FormatQuantity(BulkMatrixQuantity)} applied to visible matrix rows.";
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ApplyBulkMatrixExpectedCost()
        {
            if (!ActiveMatrixVariants.Any())
            {
                _messageBoxService.ShowWarning(
                    "No matrix variants are loaded.",
                    "No Items");

                return;
            }

            if (BulkMatrixExpectedCost <= 0)
            {
                _messageBoxService.ShowWarning(
                    "Bulk cost must be greater than zero.",
                    "Validation Error");

                return;
            }

            foreach (var item in ActiveMatrixVariants)
            {
                item.ExpectedCost = BulkMatrixExpectedCost;
                item.RecalculateLineAmounts();
            }

            StatusMessage = $"Bulk cost Rs. {BulkMatrixExpectedCost:N2} applied to visible matrix rows.";
        }

        // Kept for older XAML compatibility.
        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ApplyBulkDiscountToMatrix()
        {
            ApplyDiscountToMatrixRows(applyMode: true, applyValue: true);
        }

        // Kept for older XAML compatibility.
        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ApplyBulkVatToMatrix()
        {
            if (!ValidateBulkVat())
                return;

            foreach (var item in ActiveMatrixVariants)
            {
                item.VatRatePercent = BulkVatRatePercent;
                item.IsVatIncluded = BulkIsVatIncluded;
                item.TaxCode = BulkVatRatePercent > 0 ? "VAT" : "TAX-FREE";
                item.RecalculateLineAmounts();
            }

            StatusMessage = "Bulk VAT settings applied to visible matrix rows.";
        }

        private void ApplyDiscountToMatrixRows(bool applyMode, bool applyValue)
        {
            if (!ActiveMatrixVariants.Any())
            {
                _messageBoxService.ShowWarning(
                    "No matrix variants are loaded.",
                    "No Items");

                return;
            }

            if (!ValidateBulkDiscount())
                return;

            string mode = NormalizeDiscountMode(BulkDiscountMode);

            foreach (var item in ActiveMatrixVariants)
            {
                if (applyMode)
                    item.LineDiscountMode = mode;

                if (applyValue)
                    item.LineDiscountValue = BulkDiscountValue;

                item.RecalculateLineAmounts();
            }

            StatusMessage = "Bulk discount applied to visible matrix rows.";
        }

        // =========================================================
        // PO LINE BULK APPLY COMMANDS
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ApplyBulkDiscountModeToLines()
        {
            if (!PoLines.Any())
            {
                _messageBoxService.ShowWarning(
                    "No PO lines are available.",
                    "No Items");

                return;
            }

            if (!IsValidDiscountMode(BulkDiscountMode))
            {
                _messageBoxService.ShowWarning(
                    "Discount mode must be Amount or Percent.",
                    "Validation Error");

                return;
            }

            string mode = NormalizeDiscountMode(BulkDiscountMode);

            foreach (var line in PoLines)
            {
                line.LineDiscountMode = mode;
                RecalculateLine(line);
            }

            RecalculateTotals();
            StatusMessage = $"Discount type '{mode}' applied to PO lines.";
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ApplyBulkDiscountValueToLines()
        {
            if (!PoLines.Any())
            {
                _messageBoxService.ShowWarning(
                    "No PO lines are available.",
                    "No Items");

                return;
            }

            if (!ValidateBulkDiscount())
                return;

            string mode = NormalizeDiscountMode(BulkDiscountMode);

            foreach (var line in PoLines)
            {
                line.LineDiscountMode = mode;
                line.LineDiscountValue = BulkDiscountValue;
                RecalculateLine(line);
            }

            RecalculateTotals();
            StatusMessage = $"Discount value {BulkDiscountValue:N2} applied to PO lines.";
        }

        // Kept for older XAML compatibility.
        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ApplyBulkDiscountToLines()
        {
            ApplyBulkDiscountValueToLines();
        }

        // Kept for older XAML compatibility.
        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ApplyBulkVatToLines()
        {
            if (!PoLines.Any())
            {
                _messageBoxService.ShowWarning(
                    "No PO lines are available.",
                    "No Items");

                return;
            }

            if (!ValidateBulkVat())
                return;

            foreach (var line in PoLines)
            {
                line.VatRatePercent = BulkVatRatePercent;
                line.IsVatIncluded = BulkIsVatIncluded;
                line.TaxCode = BulkVatRatePercent > 0 ? "VAT" : "TAX-FREE";
                RecalculateLine(line);
            }

            RecalculateTotals();
            StatusMessage = "Bulk VAT settings applied to PO lines.";
        }

        private bool ValidateBulkDiscount()
        {
            if (!IsValidDiscountMode(BulkDiscountMode))
            {
                _messageBoxService.ShowWarning(
                    "Discount mode must be Amount or Percent.",
                    "Validation Error");

                return false;
            }

            if (BulkDiscountValue < 0)
            {
                _messageBoxService.ShowWarning(
                    "Discount value cannot be negative.",
                    "Validation Error");

                return false;
            }

            if (IsPercentDiscount(BulkDiscountMode) && BulkDiscountValue > 100)
            {
                _messageBoxService.ShowWarning(
                    "Discount percentage cannot be greater than 100.",
                    "Validation Error");

                return false;
            }

            return true;
        }

        private bool ValidateBulkVat()
        {
            if (BulkVatRatePercent < 0 || BulkVatRatePercent > 100)
            {
                _messageBoxService.ShowWarning(
                    "VAT rate must be between 0 and 100.",
                    "Validation Error");

                return false;
            }

            return true;
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
                SaveOrderCommand.NotifyCanExecuteChanged();
                return;
            }

            decimal subtotal = 0m;
            decimal lineDiscountTotal = 0m;
            decimal vatTotal = 0m;
            decimal lineNetTotal = 0m;

            foreach (var line in PoLines)
            {
                RecalculateLine(line);

                subtotal += line.OrderQty * line.ExpectedCost;
                lineDiscountTotal += line.LineDiscount;
                vatTotal += line.TaxAmount;
                lineNetTotal += line.LineTotal;
            }

            if (GlobalBillDiscount < 0)
                GlobalBillDiscount = 0m;

            if (GlobalBillDiscount > lineNetTotal)
                GlobalBillDiscount = lineNetTotal;

            Subtotal = Math.Round(subtotal, 2);
            TotalDiscountAmount = Math.Round(lineDiscountTotal + GlobalBillDiscount, 2);
            TotalTaxAmount = Math.Round(vatTotal, 2);

            decimal net = lineNetTotal - GlobalBillDiscount;
            NetPayable = Math.Round(net < 0 ? 0m : net, 2);

            RefreshPoLineGrid();
            SaveOrderCommand.NotifyCanExecuteChanged();
        }

        private static void RecalculateLine(PoLine line)
        {
            line.LineDiscountMode = NormalizeDiscountMode(line.LineDiscountMode);

            if (line.LineDiscountValue <= 0 &&
                line.LineDiscount > 0 &&
                line.LineDiscountMode == "Amount")
            {
                line.LineDiscountValue = line.LineDiscount;
            }

            decimal gross = line.OrderQty * line.ExpectedCost;

            line.LineDiscount = CalculateDiscountAmount(
                gross,
                line.LineDiscountMode,
                line.LineDiscountValue);

            decimal afterDiscount = gross - line.LineDiscount;

            if (afterDiscount < 0)
                afterDiscount = 0m;

            decimal vatRate = line.VatRatePercent / 100m;

            if (line.VatRatePercent <= 0)
            {
                line.TaxAmount = 0m;
                line.LineTotal = Math.Round(afterDiscount, 2);
            }
            else if (line.IsVatIncluded)
            {
                line.TaxAmount = Math.Round(
                    afterDiscount - (afterDiscount / (1 + vatRate)),
                    2);

                line.LineTotal = Math.Round(afterDiscount, 2);
            }
            else
            {
                line.TaxAmount = Math.Round(afterDiscount * vatRate, 2);
                line.LineTotal = Math.Round(afterDiscount + line.TaxAmount, 2);
            }
        }

        private void RefreshPoLineGrid()
        {
            CollectionViewSource.GetDefaultView(PoLines)?.Refresh();
        }

        // =========================================================
        // SAVE
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanSaveOrder))]
        private async Task SaveOrderAsync()
        {
            RecalculateTotals();

            if (!ValidateBeforeSave())
                return;

            bool confirm = _messageBoxService.ShowConfirmation(
                "Save and approve this Purchase Order?",
                "Confirm Save");

            if (!confirm)
                return;

            IsBusy = true;

            try
            {
                RecalculateTotals();

                var header = new PoHeader
                {
                    SupplierId = SelectedSupplier!.Id,
                    OrderDate = OrderDate.Date,
                    ExpectedDate = ExpectedDate.Date,
                    Terms = SelectedTerms,
                    CreditDays = CreditDaysInput ?? 0,
                    Remarks = Remarks.Trim(),
                    Subtotal = Subtotal,
                    GlobalBillDiscount = GlobalBillDiscount,
                    TotalTaxAmount = TotalTaxAmount,
                    TotalDiscountAmount = TotalDiscountAmount,
                    NetPayable = NetPayable,
                    CreatedBy = string.IsNullOrWhiteSpace(CurrentUser) ? "Admin" : CurrentUser.Trim(),
                    ApprovedBy = string.IsNullOrWhiteSpace(CurrentUser) ? "Admin" : CurrentUser.Trim(),
                    IsTaxInclusive = false,
                    Status = "Approved"
                };

                var linesToSave = PoLines
                    .Where(l => l.OrderQty > 0)
                    .ToList();

                await _poRepository.SavePurchaseOrderAsync(
                    header,
                    linesToSave);

                _messageBoxService.ShowInformation(
                    "Purchase Order saved successfully.",
                    "Success");

                Clear();
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Save blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Save Blocked");
            }
            catch (Exception ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                StatusMessage = "Save failed.";

                _messageBoxService.ShowError(
                    $"Failed to save Purchase Order:\n\n{message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSaveOrder()
        {
            return !IsBusy &&
                   SelectedSupplier != null &&
                   PoLines.Any(l => l.OrderQty > 0);
        }

        private bool ValidateBeforeSave()
        {
            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier.",
                    "Validation Error");

                return false;
            }

            if (ExpectedDate.Date < OrderDate.Date)
            {
                _messageBoxService.ShowWarning(
                    "Expected date cannot be before order date.",
                    "Validation Error");

                return false;
            }

            if (CreditDaysInput.HasValue &&
                (CreditDaysInput.Value < 0 || CreditDaysInput.Value > 365))
            {
                _messageBoxService.ShowWarning(
                    "Credit days must be between 0 and 365.",
                    "Validation Error");

                return false;
            }

            var activeLines = PoLines
                .Where(l => l.OrderQty > 0)
                .ToList();

            if (!activeLines.Any())
            {
                _messageBoxService.ShowWarning(
                    "Cannot save an empty Purchase Order.",
                    "Validation Error");

                return false;
            }

            if (GlobalBillDiscount < 0)
            {
                _messageBoxService.ShowWarning(
                    "Global bill discount cannot be negative.",
                    "Validation Error");

                return false;
            }

            var duplicateVariant = activeLines
                .GroupBy(l => l.ItemVariantId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateVariant != null)
            {
                _messageBoxService.ShowWarning(
                    "The same item variant cannot appear twice in one Purchase Order.",
                    "Duplicate Item");

                return false;
            }

            foreach (var line in activeLines)
            {
                RecalculateLine(line);

                string name = string.IsNullOrWhiteSpace(line.DisplayName)
                    ? line.Description
                    : line.DisplayName;

                if (line.ItemVariantId <= 0)
                {
                    _messageBoxService.ShowWarning(
                        "Invalid item variant found in Purchase Order.",
                        "Validation Error");

                    return false;
                }

                if (line.OrderQty <= 0)
                {
                    _messageBoxService.ShowWarning(
                        $"Order quantity must be greater than zero for item '{name}'.",
                        "Validation Error");

                    return false;
                }

                if (line.Moq > 0 && line.OrderQty < line.Moq)
                {
                    _messageBoxService.ShowWarning(
                        $"Item '{name}' has MOQ {line.Moq}.",
                        "MOQ Validation");

                    return false;
                }

                if (line.ExpectedCost <= 0)
                {
                    _messageBoxService.ShowWarning(
                        $"Expected cost must be greater than zero for item '{name}'.",
                        "Validation Error");

                    return false;
                }

                if (!IsValidDiscountMode(line.LineDiscountMode))
                {
                    _messageBoxService.ShowWarning(
                        $"Discount mode must be Amount or Percent for item '{name}'.",
                        "Validation Error");

                    return false;
                }

                if (line.LineDiscountValue < 0)
                {
                    _messageBoxService.ShowWarning(
                        $"Discount value cannot be negative for item '{name}'.",
                        "Validation Error");

                    return false;
                }

                if (IsPercentDiscount(line.LineDiscountMode) && line.LineDiscountValue > 100)
                {
                    _messageBoxService.ShowWarning(
                        $"Discount percentage cannot be greater than 100 for item '{name}'.",
                        "Validation Error");

                    return false;
                }

                if (line.LineDiscount < 0)
                {
                    _messageBoxService.ShowWarning(
                        $"Line discount cannot be negative for item '{name}'.",
                        "Validation Error");

                    return false;
                }

                decimal gross = line.OrderQty * line.ExpectedCost;

                if (line.LineDiscount > gross)
                {
                    _messageBoxService.ShowWarning(
                        $"Line discount cannot be greater than line value for item '{name}'.",
                        "Validation Error");

                    return false;
                }

                if (line.VatRatePercent < 0 || line.VatRatePercent > 100)
                {
                    _messageBoxService.ShowWarning(
                        $"VAT rate must be between 0 and 100 for item '{name}'.",
                        "Validation Error");

                    return false;
                }
            }

            return true;
        }

        // =========================================================
        // CLEAR
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        public void Clear()
        {
            _isClearing = true;

            try
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

                BulkMatrixQuantity = 0m;
                BulkMatrixExpectedCost = 0m;
                BulkMatrixVatIncluded = false;

                BulkDiscountMode = "Amount";
                BulkDiscountValue = 0m;
                BulkVatRatePercent = 0m;
                BulkIsVatIncluded = false;

                GlobalBillDiscount = 0m;
                SelectedItem = null;
                SelectedLine = null;
                SelectedMatrixVariant = null;

                PoLines.Clear();
                AvailableItems.Clear();
                ClearLoadedMatrixOnly();

                RecalculateTotals();

                StatusMessage = "Ready for new Purchase Order.";
            }
            finally
            {
                _isClearing = false;

                OnPropertyChanged(nameof(IsSupplierSelected));
                OnPropertyChanged(nameof(IsEntryEnabled));

                NotifyCommandStates();
            }
        }

        private void ClearLoadedMatrixOnly()
        {
            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();
            MatrixFilterText = string.Empty;
            SelectedItem = null;
            SelectedMatrixVariant = null;

            NotifyCommandStates();
        }

        // =========================================================
        // COMMAND STATE
        // =========================================================

        private void NotifyCommandStates()
        {
            InitializeCommand.NotifyCanExecuteChanged();
            AddItemCommand.NotifyCanExecuteChanged();
            AddMatrixCommand.NotifyCanExecuteChanged();
            SaveOrderCommand.NotifyCanExecuteChanged();
            ClearCommand.NotifyCanExecuteChanged();

            ApplyBulkMatrixQuantityCommand.NotifyCanExecuteChanged();
            ApplyBulkMatrixExpectedCostCommand.NotifyCanExecuteChanged();

            ApplyBulkDiscountModeToLinesCommand.NotifyCanExecuteChanged();
            ApplyBulkDiscountValueToLinesCommand.NotifyCanExecuteChanged();

            ApplyBulkDiscountToMatrixCommand.NotifyCanExecuteChanged();
            ApplyBulkVatToMatrixCommand.NotifyCanExecuteChanged();
            ApplyBulkDiscountToLinesCommand.NotifyCanExecuteChanged();
            ApplyBulkVatToLinesCommand.NotifyCanExecuteChanged();
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static decimal CalculateDiscountAmount(
            decimal gross,
            string? discountMode,
            decimal discountValue)
        {
            if (gross <= 0 || discountValue <= 0)
                return 0m;

            if (IsPercentDiscount(discountMode))
                return Math.Round(gross * discountValue / 100m, 2);

            return Math.Round(discountValue, 2);
        }

        private static bool IsValidDiscountMode(string? value)
        {
            string mode = NormalizeDiscountMode(value);

            return mode == "Amount" || mode == "Percent";
        }

        private static bool IsPercentDiscount(string? value)
        {
            return NormalizeDiscountMode(value) == "Percent";
        }

        private static string NormalizeDiscountMode(string? value)
        {
            string mode = (value ?? string.Empty).Trim();

            if (mode.Equals("Percent", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("%", StringComparison.OrdinalIgnoreCase))
            {
                return "Percent";
            }

            return "Amount";
        }

        private static bool HasDecimalPart(decimal value)
        {
            return value != Math.Truncate(value);
        }

        private static string SafeLower(string? value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string FormatQuantity(decimal value)
        {
            return value.ToString("0.###");
        }
    }

    internal static class PoVmDisplayNameHelper
    {
        public static string BuildDisplayName(
            string? baseName,
            string? variantDescription,
            string? fallback)
        {
            string cleanBaseName = NormalizeText(baseName);
            string cleanVariant = NormalizeText(variantDescription);
            string cleanFallback = NormalizeText(fallback);

            if (IsStandardVariantDescription(cleanVariant))
            {
                if (!string.IsNullOrWhiteSpace(cleanBaseName))
                    return cleanBaseName;

                return cleanFallback;
            }

            if (string.IsNullOrWhiteSpace(cleanBaseName))
                return cleanVariant;

            return $"{cleanBaseName} - {cleanVariant}";
        }

        public static bool IsStandardVariantDescription(string? value)
        {
            string cleanValue = NormalizeText(value);

            return string.IsNullOrWhiteSpace(cleanValue) ||
                   cleanValue.Equals("Standard", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
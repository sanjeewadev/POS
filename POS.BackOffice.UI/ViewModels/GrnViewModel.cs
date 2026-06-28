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
    public partial class GrnViewModel : ObservableObject
    {
        private readonly GrnRepository _grnRepository;
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly PoRepository _poRepository;

        // =========================================================
        // HEADER
        // =========================================================

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private bool _isSupplierSelectionEnabled = true;

        [ObservableProperty]
        private string _supplierInvoiceNo = string.Empty;

        [ObservableProperty]
        private DateTime _invoiceDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _receivedDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _dueDate = DateTime.Now.AddDays(30);

        [ObservableProperty]
        private string _documentStatus = "DRAFT";

        [ObservableProperty]
        private string _remarks = string.Empty;

        // =========================================================
        // PO LINKING
        // =========================================================

        public ObservableCollection<PoHeader> OpenPurchaseOrders { get; } = new();

        [ObservableProperty]
        private PoHeader? _selectedPO;

        // =========================================================
        // ENTRY
        // =========================================================

        [ObservableProperty]
        private string _scanBarcode = string.Empty;

        [ObservableProperty]
        private string _matrixBatchNo = string.Empty;

        [ObservableProperty]
        private DateTime? _matrixExpiryDate = null;

        [ObservableProperty]
        private string _matrixFilterText = string.Empty;

        // =========================================================
        // TOTALS
        // =========================================================

        [ObservableProperty]
        private decimal _subtotal = 0m;

        [ObservableProperty]
        private decimal _totalDiscountAmount = 0m;

        [ObservableProperty]
        private decimal _globalBillDiscount = 0m;

        [ObservableProperty]
        private decimal _freightAmount = 0m;

        [ObservableProperty]
        private decimal _netPayable = 0m;

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        public ObservableCollection<GrnLine> GrnLines { get; } = new();

        public ObservableCollection<ItemMasterSummaryDto> AvailableItems { get; } = new();

        private readonly List<GrnLine> _allMatrixVariants = new();

        public ObservableCollection<GrnLine> ActiveMatrixVariants { get; } = new();

        [ObservableProperty]
        private GrnLine? _selectedLine;

        [ObservableProperty]
        private ItemMasterSummaryDto? _selectedItem;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public GrnViewModel(
            GrnRepository grnRepository,
            ItemMasterRepository itemMasterRepository,
            PoRepository poRepository)
        {
            _grnRepository = grnRepository;
            _itemMasterRepository = itemMasterRepository;
            _poRepository = poRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                Suppliers.Clear();
                AvailableItems.Clear();
                OpenPurchaseOrders.Clear();

                var suppliers = await _grnRepository.GetActiveSuppliersAsync();
                foreach (var supplier in suppliers)
                    Suppliers.Add(supplier);

                var items = await _itemMasterRepository.GetSummariesAsync();
                foreach (var item in items)
                    AvailableItems.Add(item);

                await LoadOpenPurchaseOrdersAsync();

                StatusMessage = "GRN page loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize GRN page.";

                MessageBox.Show(
                    $"Failed to initialize GRN page:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadOpenPurchaseOrdersAsync()
        {
            OpenPurchaseOrders.Clear();

            var openPos = await _poRepository.GetOpenPurchaseOrdersAsync();

            foreach (var po in openPos)
                OpenPurchaseOrders.Add(po);
        }

        // =========================================================
        // HEADER EVENTS
        // =========================================================

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (value != null)
            {
                int creditDays = value.DefaultCreditDays > 0
                    ? value.DefaultCreditDays
                    : 30;

                DueDate = InvoiceDate.Date.AddDays(creditDays);
                StatusMessage = $"Selected supplier: {value.SupplierName}";
            }
            else
            {
                StatusMessage = "Select a supplier before adding items.";
            }

            ClearLoadedMatrixOnly();
        }

        partial void OnInvoiceDateChanged(DateTime value)
        {
            if (SelectedSupplier != null)
            {
                int creditDays = SelectedSupplier.DefaultCreditDays > 0
                    ? SelectedSupplier.DefaultCreditDays
                    : 30;

                DueDate = value.Date.AddDays(creditDays);
            }
        }

        partial void OnGlobalBillDiscountChanged(decimal value)
        {
            RecalculateTotals();
        }

        partial void OnFreightAmountChanged(decimal value)
        {
            RecalculateTotals();
        }

        // =========================================================
        // PO TO GRN
        // =========================================================

        [RelayCommand]
        private async Task LoadPoAsync()
        {
            if (SelectedPO == null)
            {
                MessageBox.Show(
                    "Please select a Purchase Order to load.",
                    "Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;

            try
            {
                var fullPo = await _grnRepository.GetApprovedPoDetailsAsync(SelectedPO.Id);

                if (fullPo == null)
                {
                    MessageBox.Show(
                        "Selected Purchase Order is not available for receiving.",
                        "PO Not Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == fullPo.SupplierId);
                IsSupplierSelectionEnabled = false;

                if (SelectedSupplier != null)
                {
                    DueDate = InvoiceDate.Date.AddDays(
                        SelectedSupplier.DefaultCreditDays > 0
                            ? SelectedSupplier.DefaultCreditDays
                            : 30);
                }

                GrnLines.Clear();
                ClearLoadedMatrixOnly();

                foreach (var poLine in fullPo.PoLines)
                {
                    decimal outstandingQty = poLine.OrderQty - poLine.ReceivedQty;

                    if (outstandingQty <= 0)
                        continue;

                    var grnLine = new GrnLine
                    {
                        PoLineId = poLine.Id,
                        ItemVariantId = poLine.ItemVariantId,
                        ItemCode = poLine.ItemVariant?.ItemParent?.ItemCode ?? string.Empty,
                        VariantDescription = string.IsNullOrWhiteSpace(poLine.ItemVariant?.VariantDescription)
                            ? "Standard"
                            : poLine.ItemVariant.VariantDescription,
                        Description = poLine.ItemVariant?.ItemParent?.ItemName ?? string.Empty,
                        Barcode = poLine.ItemVariant?.Barcode ?? string.Empty,
                        Uom = poLine.Uom,
                        OrderedQty = poLine.OrderQty,
                        ReceivedQty = outstandingQty,
                        UnitCost = poLine.ExpectedCost,
                        LineDiscount = 0m,
                        BatchNo = string.Empty,
                        ExpiryDate = null
                    };

                    GrnLines.Add(grnLine);
                }

                RecalculateTotals();

                StatusMessage = $"{GrnLines.Count} PO line(s) loaded for receiving.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load PO.";

                MessageBox.Show(
                    $"Failed to load Purchase Order:\n\n{ex.Message}",
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
        // MATRIX LOADING
        // =========================================================

        partial void OnSelectedItemChanged(ItemMasterSummaryDto? value)
        {
            if (value == null)
                return;

            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier before loading items.",
                    "Supplier Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SelectedItem = null;
                return;
            }

            _ = LoadVariantsForGridAsync(value.ParentId);
        }

        partial void OnMatrixFilterTextChanged(string value)
        {
            ApplyMatrixFilter();
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

                    var newLine = new GrnLine
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
                        UnitCost = supplierLink.LastCostPrice > 0
                            ? supplierLink.LastCostPrice
                            : variant.CostPrice,
                        ReceivedQty = 0m,
                        OrderedQty = 0m,
                        LineDiscount = 0m
                    };

                    _allMatrixVariants.Add(newLine);
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

                StatusMessage = $"{ActiveMatrixVariants.Count} variant(s) loaded.";
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

        private void ApplyMatrixFilter()
        {
            ActiveMatrixVariants.Clear();

            IEnumerable<GrnLine> query = _allMatrixVariants;

            if (!string.IsNullOrWhiteSpace(MatrixFilterText))
            {
                string search = MatrixFilterText.Trim().ToLowerInvariant();

                query = query.Where(v =>
                    v.VariantDescription.ToLowerInvariant().Contains(search) ||
                    v.Description.ToLowerInvariant().Contains(search) ||
                    v.Barcode.ToLowerInvariant().Contains(search));
            }

            foreach (var item in query)
                ActiveMatrixVariants.Add(item);
        }

        // =========================================================
        // BARCODE / SKU ADD
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
                    "Please select a supplier before scanning items.",
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

                var newLine = BuildGrnLineFromVariant(variant, supplierLink);
                newLine.ReceivedQty = 1m;

                if (!string.IsNullOrWhiteSpace(MatrixBatchNo))
                    newLine.BatchNo = MatrixBatchNo.Trim();

                if (MatrixExpiryDate.HasValue)
                    newLine.ExpiryDate = MatrixExpiryDate;

                MergeOrAddLine(newLine);

                ScanBarcode = string.Empty;
                RecalculateTotals();

                StatusMessage = "Item added to GRN.";
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

        private static GrnLine BuildGrnLineFromVariant(
            ItemVariant variant,
            ItemSupplier supplierLink)
        {
            return new GrnLine
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
                UnitCost = supplierLink.LastCostPrice > 0
                    ? supplierLink.LastCostPrice
                    : variant.CostPrice,
                OrderedQty = 0m,
                ReceivedQty = 0m,
                LineDiscount = 0m,
                BatchNo = string.Empty,
                ExpiryDate = null
            };
        }

        // =========================================================
        // MATRIX ADD
        // =========================================================

        [RelayCommand]
        private void AddMatrix()
        {
            var itemsToAdd = _allMatrixVariants
                .Where(v => v.ReceivedQty > 0)
                .ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show(
                    "Please enter a received quantity for at least one matrix variant.",
                    "No Quantity",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                var newLine = new GrnLine
                {
                    ItemVariantId = item.ItemVariantId,
                    ItemCode = item.ItemCode,
                    VariantDescription = item.VariantDescription,
                    Description = item.Description,
                    Barcode = item.Barcode,
                    Uom = item.Uom,
                    OrderedQty = 0m,
                    ReceivedQty = item.ReceivedQty,
                    UnitCost = item.UnitCost,
                    LineDiscount = item.LineDiscount,
                    BatchNo = string.IsNullOrWhiteSpace(item.BatchNo)
                        ? MatrixBatchNo.Trim()
                        : item.BatchNo.Trim(),
                    ExpiryDate = item.ExpiryDate ?? MatrixExpiryDate
                };

                MergeOrAddLine(newLine);
            }

            ClearLoadedMatrixOnly();
            RecalculateTotals();

            StatusMessage = "Matrix items added to GRN.";
        }

        private void MergeOrAddLine(GrnLine newLine)
        {
            string newBatch = NormalizeBatchKey(newLine.BatchNo);
            DateTime? newExpiry = newLine.ExpiryDate?.Date;

            var existing = GrnLines.FirstOrDefault(l =>
                l.ItemVariantId == newLine.ItemVariantId &&
                NormalizeBatchKey(l.BatchNo) == newBatch &&
                l.ExpiryDate?.Date == newExpiry &&
                (l.PoLineId ?? 0) == (newLine.PoLineId ?? 0));

            if (existing == null)
            {
                GrnLines.Add(newLine);
                return;
            }

            existing.ReceivedQty += newLine.ReceivedQty;

            if (newLine.UnitCost > 0)
                existing.UnitCost = newLine.UnitCost;

            existing.LineDiscount += newLine.LineDiscount;

            if (!string.IsNullOrWhiteSpace(newLine.Uom))
                existing.Uom = newLine.Uom;
        }

        [RelayCommand]
        private void RemoveLine(GrnLine? line)
        {
            if (line == null)
                return;

            GrnLines.Remove(line);
            RecalculateTotals();

            StatusMessage = "Line removed.";
        }

        // =========================================================
        // TOTALS
        // =========================================================

        [RelayCommand]
        public void RecalculateTotals()
        {
            if (!GrnLines.Any())
            {
                Subtotal = 0m;
                TotalDiscountAmount = 0m;
                NetPayable = 0m;
                RefreshGrnLineGrid();
                return;
            }

            decimal subtotal = 0m;
            decimal lineDiscountTotal = 0m;

            foreach (var line in GrnLines)
            {
                decimal gross = line.ReceivedQty * line.UnitCost;
                decimal lineTotal = gross - line.LineDiscount;

                if (lineTotal < 0)
                    lineTotal = 0;

                line.LineTotal = Math.Round(lineTotal, 2);

                subtotal += line.LineTotal;
                lineDiscountTotal += line.LineDiscount;
            }

            Subtotal = Math.Round(subtotal, 2);
            TotalDiscountAmount = Math.Round(lineDiscountTotal + GlobalBillDiscount, 2);

            decimal net = subtotal - GlobalBillDiscount + FreightAmount;
            NetPayable = Math.Round(net < 0 ? 0 : net, 2);

            AllocateLandedCost();
            RefreshGrnLineGrid();
        }

        private void AllocateLandedCost()
        {
            decimal totalBaseValue = GrnLines.Sum(l => l.LineTotal);

            if (totalBaseValue <= 0)
            {
                foreach (var line in GrnLines)
                    line.LandedCost = 0m;

                return;
            }

            foreach (var line in GrnLines)
            {
                decimal weight = line.LineTotal / totalBaseValue;
                decimal allocatedFreight = FreightAmount * weight;
                decimal allocatedGlobalDiscount = GlobalBillDiscount * weight;
                decimal landedLineTotal = line.LineTotal + allocatedFreight - allocatedGlobalDiscount;

                line.LandedCost = line.ReceivedQty > 0
                    ? Math.Round(landedLineTotal / line.ReceivedQty, 2)
                    : 0m;
            }
        }

        private void RefreshGrnLineGrid()
        {
            CollectionViewSource.GetDefaultView(GrnLines)?.Refresh();
            CollectionViewSource.GetDefaultView(ActiveMatrixVariants)?.Refresh();
        }

        // =========================================================
        // POST
        // =========================================================

        [RelayCommand]
        private async Task PostGrnAsync()
        {
            if (!ValidateBeforePost())
                return;

            var result = MessageBox.Show(
                $"Post GRN for Rs. {NetPayable:N2}?\n\nThis will update inventory, batches, supplier ledger, and PO received quantities.",
                "Confirm GRN Posting",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                RecalculateTotals();

                var validLines = GrnLines
                    .Where(l => l.ReceivedQty > 0)
                    .Select(CloneLineForPosting)
                    .ToList();

                var header = new GrnHeader
                {
                    PurchaseOrderId = SelectedPO?.Id,
                    SupplierId = SelectedSupplier!.Id,
                    SupplierInvoiceNo = SupplierInvoiceNo.Trim(),
                    InvoiceDate = InvoiceDate,
                    ReceivedDate = ReceivedDate,
                    DueDate = DueDate,
                    CreditDays = Math.Max(0, (DueDate.Date - InvoiceDate.Date).Days),
                    Remarks = Remarks.Trim(),
                    Subtotal = Subtotal,
                    GlobalBillDiscount = GlobalBillDiscount,
                    FreightAmount = FreightAmount,
                    TotalDiscountAmount = TotalDiscountAmount,
                    NetPayable = NetPayable,
                    CreatedBy = "Admin",
                    PostedBy = "Admin"
                };

                await _grnRepository.PostGrnAsync(header, validLines);

                MessageBox.Show(
                    "GRN posted successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Clear();
                await LoadOpenPurchaseOrdersAsync();
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Posting blocked.";

                MessageBox.Show(
                    ex.Message,
                    "Posting Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                StatusMessage = "Posting failed.";

                MessageBox.Show(
                    $"Transaction rolled back.\n\n{message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidateBeforePost()
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

            if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
            {
                MessageBox.Show(
                    "Supplier invoice number is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (DueDate.Date < InvoiceDate.Date)
            {
                MessageBox.Show(
                    "Due date cannot be before invoice date.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!GrnLines.Any(l => l.ReceivedQty > 0))
            {
                MessageBox.Show(
                    "Cannot post an empty GRN.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (GlobalBillDiscount < 0 || FreightAmount < 0)
            {
                MessageBox.Show(
                    "Global discount and freight cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            foreach (var line in GrnLines.Where(l => l.ReceivedQty > 0))
            {
                if (line.UnitCost <= 0)
                {
                    MessageBox.Show(
                        $"Unit cost must be greater than zero for item '{line.Description}'.",
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

                decimal gross = line.ReceivedQty * line.UnitCost;

                if (line.LineDiscount > gross)
                {
                    MessageBox.Show(
                        $"Line discount cannot be greater than line value for item '{line.Description}'.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (SelectedPO != null)
                {
                    decimal outstanding = line.OrderedQty;

                    // OrderedQty stores original PO order qty in the UI.
                    // Repository performs final exact validation against current DB ReceivedQty.
                    if (line.ReceivedQty > outstanding)
                    {
                        MessageBox.Show(
                            $"Received quantity cannot be greater than ordered quantity for item '{line.Description}'.",
                            "PO Quantity Validation",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return false;
                    }
                }
            }

            return true;
        }

        private static GrnLine CloneLineForPosting(GrnLine line)
        {
            return new GrnLine
            {
                PoLineId = line.PoLineId,
                ItemVariantId = line.ItemVariantId,
                BatchNo = line.BatchNo?.Trim() ?? string.Empty,
                ExpiryDate = line.ExpiryDate,
                Uom = line.Uom?.Trim() ?? string.Empty,
                OrderedQty = line.OrderedQty,
                ReceivedQty = line.ReceivedQty,
                UnitCost = line.UnitCost,
                LineDiscount = line.LineDiscount,
                LandedCost = line.LandedCost,
                LineTotal = line.LineTotal
            };
        }

        // =========================================================
        // CLEAR
        // =========================================================

        [RelayCommand]
        private void Clear()
        {
            SelectedPO = null;
            IsSupplierSelectionEnabled = true;

            SelectedSupplier = null;
            SupplierInvoiceNo = string.Empty;

            InvoiceDate = DateTime.Now;
            ReceivedDate = DateTime.Now;
            DueDate = DateTime.Now.AddDays(30);

            Remarks = string.Empty;
            ScanBarcode = string.Empty;
            MatrixBatchNo = string.Empty;
            MatrixExpiryDate = null;
            MatrixFilterText = string.Empty;

            GlobalBillDiscount = 0m;
            FreightAmount = 0m;
            SelectedItem = null;
            SelectedLine = null;

            GrnLines.Clear();
            ClearLoadedMatrixOnly();

            RecalculateTotals();

            DocumentStatus = "DRAFT";
            StatusMessage = "Ready for new GRN.";
        }

        private void ClearLoadedMatrixOnly()
        {
            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();
            MatrixFilterText = string.Empty;
            SelectedItem = null;
        }

        private static string NormalizeBatchKey(string? batchNo)
        {
            return (batchNo ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
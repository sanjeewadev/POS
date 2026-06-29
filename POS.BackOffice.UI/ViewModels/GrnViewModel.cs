using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class GrnViewModel : ObservableObject
    {
        private readonly GrnRepository _grnRepository;
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly DispatcherTimer _recalculateTimer;

        private readonly List<GrnLineEntryDto> _allMatrixVariants = new();

        private bool _isRecalculating = false;
        private bool _isClearing = false;

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

        public ObservableCollection<GrnPoLookupDto> OpenPurchaseOrders { get; } = new();

        [ObservableProperty]
        private GrnPoLookupDto? _selectedPO;

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

        public ObservableCollection<GrnLineEntryDto> GrnLines { get; } = new();

        public ObservableCollection<ItemMasterSummaryDto> AvailableItems { get; } = new();

        public ObservableCollection<GrnLineEntryDto> ActiveMatrixVariants { get; } = new();

        [ObservableProperty]
        private GrnLineEntryDto? _selectedLine;

        [ObservableProperty]
        private ItemMasterSummaryDto? _selectedItem;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public bool IsPoLinked => SelectedPO != null;

        public GrnViewModel(
            GrnRepository grnRepository,
            ItemMasterRepository itemMasterRepository,
            PoRepository poRepository)
        {
            _grnRepository = grnRepository;
            _itemMasterRepository = itemMasterRepository;

            _recalculateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };

            _recalculateTimer.Tick += (_, _) =>
            {
                _recalculateTimer.Stop();
                RecalculateTotals();
            };

            _ = InitializeAsync();
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        private async Task InitializeAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading GRN page...";

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

            var openPos = await _grnRepository.GetOpenPurchaseOrderLookupsAsync();

            foreach (var po in openPos)
                OpenPurchaseOrders.Add(po);
        }

        // =========================================================
        // HEADER EVENTS
        // =========================================================

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (_isClearing)
                return;

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
            QueueRecalculate();
        }

        partial void OnFreightAmountChanged(decimal value)
        {
            QueueRecalculate();
        }

        partial void OnSelectedPOChanged(GrnPoLookupDto? value)
        {
            OnPropertyChanged(nameof(IsPoLinked));
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

            if (GrnLines.Any())
            {
                var confirm = MessageBox.Show(
                    "Loading a Purchase Order will clear the current GRN lines.\n\nContinue?",
                    "Replace Current GRN",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            IsBusy = true;
            StatusMessage = $"Loading PO {SelectedPO.PoNumber}...";

            try
            {
                var poLines = await _grnRepository.GetOutstandingPoLinesAsync(
                    SelectedPO.PoHeaderId);

                if (!poLines.Any())
                {
                    MessageBox.Show(
                        "This Purchase Order has no outstanding lines to receive.",
                        "No Outstanding Quantity",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                UnsubscribeGrnLineEvents();

                GrnLines.Clear();
                ClearLoadedMatrixOnly();

                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == SelectedPO.SupplierId);
                IsSupplierSelectionEnabled = false;

                if (SelectedSupplier != null)
                {
                    DueDate = InvoiceDate.Date.AddDays(
                        SelectedSupplier.DefaultCreditDays > 0
                            ? SelectedSupplier.DefaultCreditDays
                            : 30);
                }

                foreach (var poLine in poLines)
                {
                    var grnLine = new GrnLineEntryDto
                    {
                        PoLineId = poLine.PoLineId,
                        ItemVariantId = poLine.ItemVariantId,
                        ItemCode = poLine.ItemCode,
                        SkuCode = poLine.SkuCode,
                        Barcode = poLine.Barcode,
                        Description = poLine.Description,
                        VariantDescription = poLine.VariantDescription,
                        Uom = string.IsNullOrWhiteSpace(poLine.Uom) ? "PCS" : poLine.Uom,
                        OrderedQty = poLine.OrderedQty,
                        OutstandingPoQty = poLine.OutstandingQty,
                        ReceivedQty = poLine.OutstandingQty,
                        UnitCost = poLine.ExpectedCost,
                        LineDiscount = 0m,
                        BatchNo = string.Empty,
                        ExpiryDate = null,
                        RequiresExpiry = poLine.RequiresExpiry
                    };

                    SubscribeGrnLineEvents(grnLine);
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
            if (_isClearing)
                return;

            if (value == null)
                return;

            if (SelectedPO != null)
            {
                MessageBox.Show(
                    "This GRN is linked to a Purchase Order. Use the loaded PO lines only.",
                    "PO Linked GRN",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                SelectedItem = null;
                return;
            }

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
            StatusMessage = "Loading item variants...";

            try
            {
                _allMatrixVariants.Clear();
                ActiveMatrixVariants.Clear();

                var variants = await _itemMasterRepository.GetVariantsByParentIdAsync(parentId);

                foreach (var variant in variants)
                {
                    if (variant.ItemParent == null)
                        continue;

                    if (variant.IsDeactivated || variant.ItemParent.IsDeactivated)
                        continue;

                    if (variant.ItemParent.IsPurchaseLocked)
                        continue;

                    var supplierLink = variant.ItemSuppliers?
                        .FirstOrDefault(s => s.SupplierId == SelectedSupplier!.Id);

                    if (supplierLink == null)
                        continue;

                    var newLine = new GrnLineEntryDto
                    {
                        ItemVariantId = variant.Id,
                        ItemCode = variant.ItemParent.ItemCode,
                        SkuCode = variant.SkuCode,
                        Barcode = variant.Barcode ?? string.Empty,
                        Description = variant.ItemParent.ItemName,
                        VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                            ? "Standard"
                            : variant.VariantDescription,
                        Uom = string.IsNullOrWhiteSpace(variant.ItemParent.BaseUom)
                            ? "PCS"
                            : variant.ItemParent.BaseUom,
                        UnitCost = supplierLink.LastCostPrice > 0
                            ? supplierLink.LastCostPrice
                            : variant.CostPrice,
                        ReceivedQty = 0m,
                        OrderedQty = 0m,
                        OutstandingPoQty = 0m,
                        LineDiscount = 0m,
                        BatchNo = string.Empty,
                        ExpiryDate = null,
                        RequiresExpiry = variant.ItemParent.HasBatchExpiry
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

            IEnumerable<GrnLineEntryDto> query = _allMatrixVariants;

            if (!string.IsNullOrWhiteSpace(MatrixFilterText))
            {
                string search = MatrixFilterText.Trim().ToLowerInvariant();

                query = query.Where(v =>
                    SafeLower(v.VariantDescription).Contains(search) ||
                    SafeLower(v.Description).Contains(search) ||
                    SafeLower(v.ItemCode).Contains(search) ||
                    SafeLower(v.SkuCode).Contains(search) ||
                    SafeLower(v.Barcode).Contains(search));
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

            if (SelectedPO != null)
            {
                MessageBox.Show(
                    "This GRN is linked to a Purchase Order. Use the loaded PO lines only.",
                    "PO Linked GRN",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ScanBarcode = string.Empty;
                return;
            }

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

                if (variant.ItemParent == null)
                {
                    MessageBox.Show(
                        "Selected item has no parent item record.",
                        "Invalid Item",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ScanBarcode = string.Empty;
                    return;
                }

                if (variant.IsDeactivated || variant.ItemParent.IsDeactivated)
                {
                    MessageBox.Show(
                        "This item is deactivated.",
                        "Inactive Item",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ScanBarcode = string.Empty;
                    return;
                }

                if (variant.ItemParent.IsPurchaseLocked)
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

                var newLine = BuildLineFromVariant(variant, supplierLink);
                newLine.ReceivedQty = 1m;

                if (!string.IsNullOrWhiteSpace(MatrixBatchNo))
                    newLine.BatchNo = MatrixBatchNo.Trim();

                if (MatrixExpiryDate.HasValue)
                    newLine.ExpiryDate = MatrixExpiryDate.Value.Date;

                MergeOrAddLine(newLine);

                ScanBarcode = string.Empty;
                RecalculateTotals();

                StatusMessage = $"Item added: {newLine.DisplayName}.";
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

        private static GrnLineEntryDto BuildLineFromVariant(
            ItemVariant variant,
            ItemSupplier supplierLink)
        {
            return new GrnLineEntryDto
            {
                ItemVariantId = variant.Id,
                ItemCode = variant.ItemParent?.ItemCode ?? string.Empty,
                SkuCode = variant.SkuCode,
                Barcode = variant.Barcode ?? string.Empty,
                Description = variant.ItemParent?.ItemName ?? string.Empty,
                VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                    ? "Standard"
                    : variant.VariantDescription,
                Uom = string.IsNullOrWhiteSpace(variant.ItemParent?.BaseUom)
                    ? "PCS"
                    : variant.ItemParent.BaseUom,
                UnitCost = supplierLink.LastCostPrice > 0
                    ? supplierLink.LastCostPrice
                    : variant.CostPrice,
                OrderedQty = 0m,
                OutstandingPoQty = 0m,
                ReceivedQty = 0m,
                LineDiscount = 0m,
                BatchNo = string.Empty,
                ExpiryDate = null,
                RequiresExpiry = variant.ItemParent?.HasBatchExpiry == true
            };
        }

        // =========================================================
        // MATRIX ADD
        // =========================================================

        [RelayCommand]
        private void AddMatrix()
        {
            if (SelectedPO != null)
            {
                MessageBox.Show(
                    "This GRN is linked to a Purchase Order. Use the loaded PO lines only.",
                    "PO Linked GRN",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

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
                var newLine = item.CloneForGrnEntry();

                newLine.BatchNo = string.IsNullOrWhiteSpace(item.BatchNo)
                    ? MatrixBatchNo.Trim()
                    : item.BatchNo.Trim();

                newLine.ExpiryDate = item.ExpiryDate ?? MatrixExpiryDate;

                MergeOrAddLine(newLine);
            }

            ClearLoadedMatrixOnly();
            RecalculateTotals();

            StatusMessage = "Matrix items added to GRN.";
        }

        private void MergeOrAddLine(GrnLineEntryDto newLine)
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
                SubscribeGrnLineEvents(newLine);
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
        private void RemoveLine(GrnLineEntryDto? line)
        {
            if (line == null)
                return;

            line.PropertyChanged -= GrnLine_PropertyChanged;

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
            if (_isRecalculating)
                return;

            _isRecalculating = true;

            try
            {
                decimal subtotal = 0m;
                decimal lineDiscountTotal = 0m;

                foreach (var line in GrnLines)
                {
                    if (line.ReceivedQty <= 0 || line.UnitCost <= 0)
                    {
                        line.LineTotal = 0m;
                        line.LandedCost = 0m;
                        continue;
                    }

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
            }
            finally
            {
                _isRecalculating = false;
            }
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
                if (line.ReceivedQty <= 0)
                {
                    line.LandedCost = 0m;
                    continue;
                }

                decimal weight = line.LineTotal / totalBaseValue;
                decimal allocatedFreight = FreightAmount * weight;
                decimal allocatedGlobalDiscount = GlobalBillDiscount * weight;
                decimal landedLineTotal = line.LineTotal + allocatedFreight - allocatedGlobalDiscount;

                line.LandedCost = Math.Round(landedLineTotal / line.ReceivedQty, 2);
            }
        }

        private void QueueRecalculate()
        {
            _recalculateTimer.Stop();
            _recalculateTimer.Start();
        }

        // =========================================================
        // POST
        // =========================================================

        [RelayCommand]
        private async Task PostGrnAsync()
        {
            RecalculateTotals();

            if (!ValidateBeforePost())
                return;

            var result = MessageBox.Show(
                $"Post GRN for Rs. {NetPayable:N2}?\n\nThis will update inventory, item batches, PO received quantities, and supplier ledger.",
                "Confirm GRN Posting",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = "Posting GRN...";

            try
            {
                RecalculateTotals();

                var validLines = GrnLines
                    .Where(l => l.ReceivedQty > 0)
                    .Select(ToPostingLine)
                    .ToList();

                var header = new GrnHeader
                {
                    PurchaseOrderId = SelectedPO?.PoHeaderId,
                    SupplierId = SelectedSupplier!.Id,
                    SupplierInvoiceNo = SupplierInvoiceNo.Trim(),
                    InvoiceDate = InvoiceDate.Date,
                    ReceivedDate = ReceivedDate.Date,
                    DueDate = DueDate.Date,
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

                StatusMessage = "GRN posted successfully.";
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

            if (GlobalBillDiscount > Subtotal)
            {
                MessageBox.Show(
                    "Global bill discount cannot be greater than subtotal.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            var errors = new List<string>();

            bool isPoLinked = SelectedPO != null;

            foreach (var line in GrnLines.Where(l => l.ReceivedQty > 0))
            {
                errors.AddRange(line.ValidateForPost(isPoLinked));

                if (line.ExpiryDate.HasValue &&
                    line.ExpiryDate.Value.Date < ReceivedDate.Date)
                {
                    errors.Add($"{line.DisplayName}: expiry date cannot be before received date.");
                }
            }

            if (errors.Any())
            {
                MessageBox.Show(
                    "Cannot post GRN because validation failed:\n\n" +
                    string.Join("\n", errors),
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        private static GrnLine ToPostingLine(GrnLineEntryDto line)
        {
            return new GrnLine
            {
                PoLineId = line.PoLineId,
                ItemBatchId = line.ItemBatchId,
                ItemVariantId = line.ItemVariantId,
                BatchNo = line.BatchNo?.Trim() ?? string.Empty,
                ExpiryDate = line.ExpiryDate?.Date,
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
            _isClearing = true;

            try
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

                UnsubscribeGrnLineEvents();
                GrnLines.Clear();

                ClearLoadedMatrixOnly();

                Subtotal = 0m;
                TotalDiscountAmount = 0m;
                NetPayable = 0m;

                DocumentStatus = "DRAFT";
                StatusMessage = "Ready for new GRN.";

                OnPropertyChanged(nameof(IsPoLinked));
            }
            finally
            {
                _isClearing = false;
            }
        }

        private void ClearLoadedMatrixOnly()
        {
            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();
            MatrixFilterText = string.Empty;

            if (!_isClearing)
            {
                _isClearing = true;
                SelectedItem = null;
                _isClearing = false;
            }
            else
            {
                SelectedItem = null;
            }
        }

        // =========================================================
        // EVENTS
        // =========================================================

        private void SubscribeGrnLineEvents(GrnLineEntryDto line)
        {
            line.PropertyChanged -= GrnLine_PropertyChanged;
            line.PropertyChanged += GrnLine_PropertyChanged;
        }

        private void UnsubscribeGrnLineEvents()
        {
            foreach (var line in GrnLines)
                line.PropertyChanged -= GrnLine_PropertyChanged;
        }

        private void GrnLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isRecalculating)
                return;

            if (e.PropertyName == nameof(GrnLineEntryDto.ReceivedQty) ||
                e.PropertyName == nameof(GrnLineEntryDto.UnitCost) ||
                e.PropertyName == nameof(GrnLineEntryDto.LineDiscount))
            {
                QueueRecalculate();
            }
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static string NormalizeBatchKey(string? batchNo)
        {
            return (batchNo ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string SafeLower(string? value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }

    internal static class GrnLineEntryDtoExtensions
    {
        public static GrnLineEntryDto CloneForGrnEntry(this GrnLineEntryDto source)
        {
            return new GrnLineEntryDto
            {
                GrnLineId = 0,
                PoLineId = source.PoLineId,
                ItemBatchId = source.ItemBatchId,
                ItemVariantId = source.ItemVariantId,
                ItemCode = source.ItemCode,
                SkuCode = source.SkuCode,
                Barcode = source.Barcode,
                Description = source.Description,
                VariantDescription = source.VariantDescription,
                Uom = source.Uom,
                OrderedQty = source.OrderedQty,
                OutstandingPoQty = source.OutstandingPoQty,
                RequiresExpiry = source.RequiresExpiry,
                BatchNo = source.BatchNo,
                ExpiryDate = source.ExpiryDate,
                ReceivedQty = source.ReceivedQty,
                UnitCost = source.UnitCost,
                LineDiscount = source.LineDiscount,
                LandedCost = source.LandedCost,
                LineTotal = source.LineTotal
            };
        }
    }
}
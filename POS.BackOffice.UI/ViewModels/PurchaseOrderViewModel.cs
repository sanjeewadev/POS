using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Exports;
using POS.Core.Utilities;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PurchaseOrderViewModel : ObservableObject
    {
        private readonly PoRepository _poRepository;
        private readonly StoreSettingsRepository _storeSettingsRepository;
        private readonly OperationalExportBuilder _exportBuilder;
        private readonly ExportDialogService _exportDialogService;
        private readonly ExportAuthorizationService _exportAuthorization;
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

        [ObservableProperty]
        private PoLine? _selectedLine;

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
            StoreSettingsRepository storeSettingsRepository,
            OperationalExportBuilder exportBuilder,
            ExportDialogService exportDialogService,
            ExportAuthorizationService exportAuthorization,
            IMessageBoxService messageBoxService)
        {
            _poRepository = poRepository ?? throw new ArgumentNullException(nameof(poRepository));
            _storeSettingsRepository = storeSettingsRepository ?? throw new ArgumentNullException(nameof(storeSettingsRepository));
            _exportBuilder = exportBuilder ?? throw new ArgumentNullException(nameof(exportBuilder));
            _exportDialogService = exportDialogService ?? throw new ArgumentNullException(nameof(exportDialogService));
            _exportAuthorization = exportAuthorization ?? throw new ArgumentNullException(nameof(exportAuthorization));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));

            PoLines.CollectionChanged += (_, e) =>
            {
                SaveOrderCommand.NotifyCanExecuteChanged();
                ApplyBulkDiscountModeToLinesCommand.NotifyCanExecuteChanged();
                ApplyBulkDiscountValueToLinesCommand.NotifyCanExecuteChanged();

                if (e.NewItems != null) foreach (PoLine line in e.NewItems) SubscribeToLineEvents(line);
                if (e.OldItems != null) foreach (PoLine line in e.OldItems) UnsubscribeFromLineEvents(line);

                // A line was added or removed, so recalculate totals.
                RecalculateTotals();
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
            OnPropertyChanged(nameof(CanUseSupplierVatPriceMode));
            OnPropertyChanged(nameof(SupplierPriceModeText));

            if (value?.HasVat != true)
                IsTaxInclusive = false;

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

            AvailableItems.Clear();

            if (value == null)
            {
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
            if (_isNormalizingGlobalBillDiscount)
                return;

            RecalculateTotals();
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
                    SelectedSupplier.Id,
                    OrderDate);

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

        private PoLine BuildPoLineFromVariant(
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
                TaxCategoryId = variant.TaxCategoryId,
                TaxRateId = variant.TaxRateId,
                TaxCategoryCodeSnapshot = variant.TaxCategoryCode,
                TaxCodeSnapshot = variant.TaxCode,
                TaxNameSnapshot = variant.TaxName,
                TaxRatePercentSnapshot = variant.VatRatePercent,
                SupplierItemCode = variant.SupplierItemCode,
                Moq = variant.Moq <= 0 ? 1 : variant.Moq,
                HasBatchTracking = variant.HasBatchTracking,
                HasExpiryTracking = variant.HasExpiryTracking,
                OrderQty = orderQty,
                ExpectedCost = expectedCost,
                LineDiscountMode = "Amount",
                LineDiscountValue = 0m,
                LineDiscount = 0m,
                VatRatePercent = variant.VatRatePercent,
                IsVatIncluded = IsTaxInclusive,
                TaxAmount = 0m,
                LineTotal = 0m,
                LineStatus = "Open"
            };

            line.RecalculateLineAmounts();

            return line;
        }

        // =========================================================
        // REUSABLE VARIANT ENTRY DIALOG
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanOpenPurchasingVariantEntryDialog))]
        private void OpenPurchasingVariantEntryDialog()
        {
            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Select a supplier before opening variant entry.",
                    "Supplier Required");
                return;
            }

            if (AvailableItems.Count == 0)
            {
                _messageBoxService.ShowInformation(
                    "No supplier-approved purchasable Stock Items are available for this supplier.",
                    "No Items");
                return;
            }

            try
            {
                int supplierId = SelectedSupplier.Id;

                IReadOnlyList<PurchasingItemOption> itemOptions = AvailableItems
                    .Select(item => new PurchasingItemOption
                    {
                        ParentId = item.ParentId,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        TrackingText = item.TrackingText
                    })
                    .ToList();

                async Task<IReadOnlyList<PurchasingVariantSource>> LoadVariantsAsync(int parentId)
                {
                    IReadOnlyList<PoVariantLookupDto> variants =
                        await _poRepository.GetSupplierApprovedVariantsByParentAsync(
                            parentId,
                            supplierId,
                            OrderDate.Date);

                    return variants
                        .Select(variant => new PurchasingVariantSource
                        {
                            ItemVariantId = variant.ItemVariantId,
                            ItemCode = variant.ItemCode,
                            SkuCode = variant.SkuCode,
                            Barcode = variant.Barcode,
                            Description = variant.Description,
                            PrintName = variant.PrintName,
                            VariantDescription = variant.VariantDescription,
                            Uom = variant.Uom,
                            SuggestedUnitCost = variant.LastSupplierCost > 0m
                                ? variant.LastSupplierCost
                                : variant.CurrentCost,
                            HasBatchTracking = variant.HasBatchTracking,
                            RequiresExpiry = false,
                            IsScaleItem = false,
                            AllowDecimalQuantity = variant.AllowDecimalQuantity,
                            MinimumQuantity = variant.Moq <= 0 ? 1 : variant.Moq,
                            SupplierItemCode = variant.SupplierItemCode,
                            TaxCategoryId = variant.TaxCategoryId,
                            TaxRateId = variant.TaxRateId,
                            TaxCategoryCode = variant.TaxCategoryCode,
                            TaxCategoryName = variant.TaxCategoryName,
                            TaxCode = variant.TaxCode,
                            TaxName = variant.TaxName,
                            VatRatePercent = variant.VatRatePercent
                        })
                        .ToList();
                }

                var dialogViewModel = new PurchasingVariantEntryDialogViewModel(
                    itemOptions,
                    LoadVariantsAsync,
                    OrderDate.Date,
                    expiryEntryEnabled: false,
                    quantityLabel: "Order Qty",
                    unitCostLabel: "Expected Cost",
                    minimumQuantityEnabled: true,
                    dialogTitle: "Add Purchase Order Variants",
                    primaryActionText: "ADD ENTERED ROWS TO PO");

                var dialog = new PurchasingVariantEntryDialog(dialogViewModel)
                {
                    Owner = GetDialogOwner()
                };

                if (dialog.ShowDialog() != true)
                    return;

                foreach (PurchasingVariantEntryRow acceptedRow in dialog.AcceptedRows)
                    MergeOrAddLine(BuildPoLineFromPurchasingEntry(acceptedRow));

                RecalculateTotals();
                StatusMessage = $"{dialog.AcceptedRows.Count} variant row(s) added to the Purchase Order.";
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Open PO purchasing variant-entry dialog",
                    ex);

                _messageBoxService.ShowError(
                    "The variant-entry window could not be opened or completed. " +
                    "The current Purchase Order remains available and no partial dialog changes were applied. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "PO Variant Entry Error");
            }
        }

        private bool CanOpenPurchasingVariantEntryDialog()
        {
            return !IsBusy &&
                   SelectedSupplier != null &&
                   AvailableItems.Count > 0;
        }

        private PoLine BuildPoLineFromPurchasingEntry(
            PurchasingVariantEntryRow row)
        {
            PurchasingVariantSource source = row.Source;

            var line = new PoLine
            {
                ItemVariantId = source.ItemVariantId,
                ItemCode = source.ItemCode,
                SkuCode = source.SkuCode,
                VariantDescription = string.IsNullOrWhiteSpace(source.VariantDescription)
                    ? "Standard"
                    : source.VariantDescription,
                Description = source.Description,
                PrintName = source.PrintName,
                Barcode = source.Barcode,
                Uom = string.IsNullOrWhiteSpace(source.Uom) ? "PCS" : source.Uom,
                TaxCode = string.IsNullOrWhiteSpace(source.TaxCode) ? "VAT" : source.TaxCode,
                TaxCategoryId = source.TaxCategoryId,
                TaxRateId = source.TaxRateId,
                TaxCategoryCodeSnapshot = source.TaxCategoryCode,
                TaxCodeSnapshot = source.TaxCode,
                TaxNameSnapshot = source.TaxName,
                TaxRatePercentSnapshot = source.VatRatePercent,
                SupplierItemCode = source.SupplierItemCode,
                Moq = source.MinimumQuantity <= 0 ? 1 : source.MinimumQuantity,
                HasBatchTracking = source.HasBatchTracking,
                HasExpiryTracking = false,
                OrderQty = row.Quantity,
                ExpectedCost = row.UnitCost,
                LineDiscountMode = "Amount",
                LineDiscountValue = 0m,
                LineDiscount = 0m,
                VatRatePercent = source.VatRatePercent,
                IsVatIncluded = IsTaxInclusive,
                TaxAmount = 0m,
                LineTotal = 0m,
                LineStatus = "Open"
            };

            line.RecalculateLineAmounts();
            return line;
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
            existing.IsVatIncluded = IsTaxInclusive;
            existing.TaxCategoryId = newLine.TaxCategoryId;
            existing.TaxRateId = newLine.TaxRateId;
            existing.TaxCategoryCodeSnapshot = newLine.TaxCategoryCodeSnapshot;
            existing.TaxCodeSnapshot = newLine.TaxCodeSnapshot;
            existing.TaxNameSnapshot = newLine.TaxNameSnapshot;
            existing.TaxRatePercentSnapshot = newLine.TaxRatePercentSnapshot;
            existing.Moq = newLine.Moq;
            // The setters on the 'existing' PoLine will trigger its own recalculation.
            // The PropertyChanged event will then bubble up to the ViewModel to update document totals.

            SaveOrderCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void RemoveLine(PoLine? line)
        {
            if (line == null)
                return;

            UnsubscribeFromLineEvents(line);
            PoLines.Remove(line);
            SelectedLine = null;
            RecalculateTotals();

            StatusMessage = "Line removed.";

            SaveOrderCommand.NotifyCanExecuteChanged();
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
                // The setter on LineDiscountMode will trigger RecalculateLineAmounts()
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
                // The setter on LineDiscountValue will trigger RecalculateLineAmounts()
            }

            RecalculateTotals();
            StatusMessage = $"Discount value {BulkDiscountValue:N2} applied to PO lines.";
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

        // =========================================================
        // TOTALS
        // =========================================================

        public void RecalculateTotals()
        {
            RecalculateAuthoritativeTotals();
        }

        private static void RecalculateLine(PoLine line)
        {
            // This method is now just a proxy to the line's own calculation method.
            // It's kept for any parts of the code that were calling the static helper.
            line.RecalculateLineAmounts();
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
                    IsTaxInclusive =
                        SelectedSupplier?.HasVat == true &&
                        IsTaxInclusive,
                    TaxableAmountTotal = TaxableAmountTotal,
                    StandardRatedAmount = StandardRatedAmount,
                    ZeroRatedAmount = ZeroRatedAmount,
                    ExemptAmount = ExemptAmount,
                    OutOfScopeAmount = OutOfScopeAmount,
                    Status = "Approved"
                };

                var linesToSave = PoLines
                    .Where(l => l.OrderQty > 0)
                    .ToList();

                await _poRepository.SavePurchaseOrderAsync(
                    header,
                    linesToSave);

                header.Supplier = SelectedSupplier!;
                header.PoLines = linesToSave;

                bool exportPdf = _messageBoxService.ShowConfirmation(
                    $"Purchase Order {header.PoNumber} was saved and approved successfully.\n\nExport it as PDF now?",
                    "Purchase Order Saved");

                string saveStatus = $"Purchase Order {header.PoNumber} saved and approved.";

                if (exportPdf)
                    saveStatus = await ExportSavedPurchaseOrderPdfAsync(header);

                Clear();
                StatusMessage = saveStatus;
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

        private async Task<string> ExportSavedPurchaseOrderPdfAsync(
            PoHeader savedPurchaseOrder)
        {
            try
            {
                _exportAuthorization.EnsureOperationalDocumentAllowed();

                StoreSettings settings = await _storeSettingsRepository.GetActiveAsync()
                    ?? StoreSettingsRepository.CreateDefaultSettings();

                var document = _exportBuilder.BuildPurchaseOrder(
                    savedPurchaseOrder,
                    settings,
                    _exportAuthorization.CurrentUsername);

                string? path = _exportDialogService.SaveTablePdf(
                    "Save Purchase Order PDF",
                    ExportFileNameHelper.Create(
                        "Purchase_Order",
                        savedPurchaseOrder.PoNumber,
                        "pdf"),
                    document);

                return path == null
                    ? $"Purchase Order {savedPurchaseOrder.PoNumber} saved; PDF export was cancelled."
                    : $"Purchase Order {savedPurchaseOrder.PoNumber} saved and exported to PDF.";
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    $"Export saved Purchase Order {savedPurchaseOrder.PoNumber} to PDF",
                    ex);

                _messageBoxService.ShowError(
                    $"Purchase Order {savedPurchaseOrder.PoNumber} was saved successfully, " +
                    "but the PDF could not be exported.\n\n" +
                    ex.Message,
                    "PDF Export Error");

                return $"Purchase Order {savedPurchaseOrder.PoNumber} saved; PDF export failed.";
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
                line.RecalculateLineAmounts();

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

                BulkDiscountMode = "Amount";
                BulkDiscountValue = 0m;

                GlobalBillDiscount = 0m;
                IsTaxInclusive = false;
                TaxableAmountTotal = 0m;
                StandardRatedAmount = 0m;
                ZeroRatedAmount = 0m;
                ExemptAmount = 0m;
                OutOfScopeAmount = 0m;
                SelectedLine = null;

                UnsubscribeAllLineEvents();
                PoLines.Clear();
                AvailableItems.Clear();

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

        // =========================================================
        // COMMAND STATE
        // =========================================================

        private void NotifyCommandStates()
        {
            InitializeCommand.NotifyCanExecuteChanged();
            AddItemCommand.NotifyCanExecuteChanged();
            OpenPurchasingVariantEntryDialogCommand.NotifyCanExecuteChanged();
            SaveOrderCommand.NotifyCanExecuteChanged();
            ClearCommand.NotifyCanExecuteChanged();
            ApplyBulkDiscountModeToLinesCommand.NotifyCanExecuteChanged();
            ApplyBulkDiscountValueToLinesCommand.NotifyCanExecuteChanged();
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        // =========================================================
        // PO LINE EVENT HANDLING
        // =========================================================

        private void SubscribeToLineEvents(PoLine line)
        {
            line.PropertyChanged += PoLine_PropertyChanged;
        }

        private void UnsubscribeFromLineEvents(PoLine line)
        {
            line.PropertyChanged -= PoLine_PropertyChanged;
        }

        private void UnsubscribeAllLineEvents()
        {
            foreach (var line in PoLines)
            {
                UnsubscribeFromLineEvents(line);
            }
        }

        private void PoLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When a line's calculated total changes, we need to update the document totals.
            if (e.PropertyName == nameof(PoLine.LineTotal))
                RecalculateTotals();
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

        private static Window? GetDialogOwner()
        {
            return Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive)
                ?? Application.Current?.MainWindow;
        }

    }
}

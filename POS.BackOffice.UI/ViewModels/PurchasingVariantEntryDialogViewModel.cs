using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Utilities;
namespace POS.BackOffice.UI.ViewModels
{
    public sealed class PurchasingItemOption
    {
        public int ParentId { get; init; }
        public string ItemCode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string TrackingText { get; init; } = string.Empty;
        public string DisplayText => string.IsNullOrWhiteSpace(ItemCode)
            ? ItemName
            : $"{ItemCode} - {ItemName}";
    }
    public sealed class PurchasingVariantSource
    {
        public int ItemVariantId { get; init; }
        public string ItemCode { get; init; } = string.Empty;
        public string SkuCode { get; init; } = string.Empty;
        public string Barcode { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string PrintName { get; init; } = string.Empty;
        public string VariantDescription { get; init; } = string.Empty;
        public string Uom { get; init; } = string.Empty;
        public decimal SuggestedUnitCost { get; init; }
        public bool HasBatchTracking { get; init; }
        public bool RequiresExpiry { get; init; }
        public bool IsScaleItem { get; init; }
        public bool AllowDecimalQuantity { get; init; }
        public int MinimumQuantity { get; init; } = 1;
        public string SupplierItemCode { get; init; } = string.Empty;
        public int TaxCategoryId { get; init; }
        public int? TaxRateId { get; init; }
        public string TaxCode { get; init; } = string.Empty;
        public string TaxName { get; init; } = string.Empty;
        public string LineDiscountMode { get; init; } = "Amount";
        public decimal LineDiscountValue { get; init; }
        public decimal LineDiscount { get; init; }
        public decimal VatRatePercent { get; init; }
        public decimal VatAmount { get; init; }
        public string TaxCategoryCode { get; init; } = string.Empty;
        public string TaxCategoryName { get; init; } = string.Empty;
        public decimal CurrentRetailPrice { get; init; }
        public decimal CurrentWholesalePrice { get; init; }
        public decimal CurrentMinimumPrice { get; init; }
        public decimal CurrentMaximumPrice { get; init; }
    }
    public partial class PurchasingVariantEntryRow : ObservableObject
    {
        public PurchasingVariantEntryRow(PurchasingVariantSource source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            UnitCost = source.SuggestedUnitCost;
        }
        public PurchasingVariantSource Source { get; }
        public string SkuCode => Source.SkuCode;
        public string Uom => string.IsNullOrWhiteSpace(Source.Uom)
            ? "PCS"
            : Source.Uom;
        public bool RequiresExpiry => Source.RequiresExpiry;
        public bool AllowDecimalQuantity => Source.AllowDecimalQuantity;
        public int MinimumQuantity => Source.MinimumQuantity <= 0 ? 1 : Source.MinimumQuantity;
        public string MinimumQuantityDisplay =>
            QuantityDisplayFormatter.Format(MinimumQuantity);
        public string TrackingText
        {
            get
            {
                if (!Source.HasBatchTracking)
                    return "Average Cost";
                return RequiresExpiry ? "Batch + Expiry" : "Batch";
            }
        }
        public string TaxCategoryDisplayText
        {
            get
            {
                string name = string.IsNullOrWhiteSpace(Source.TaxCategoryName)
                    ? Source.TaxCategoryCode
                    : Source.TaxCategoryName;
                if (string.IsNullOrWhiteSpace(name))
                    name = "Tax pending";
                return Source.VatRatePercent > 0m
                    ? $"{name} ({Source.VatRatePercent:N2}%)"
                    : name;
            }
        }
        public string DisplayName
        {
            get
            {
                string variant = string.IsNullOrWhiteSpace(Source.VariantDescription) ||
                                 string.Equals(Source.VariantDescription, "Standard", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : $" - {Source.VariantDescription}";
                return $"{Source.Description}{variant}".Trim();
            }
        }
        [ObservableProperty]
        private decimal _quantity;
        [ObservableProperty]
        private decimal _unitCost;
        [ObservableProperty]
        private DateTime? _expiryDate;
        public bool Matches(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;
            string normalized = search.Trim();
            return Contains(DisplayName, normalized) ||
                   Contains(Source.ItemCode, normalized) ||
                   Contains(Source.SkuCode, normalized) ||
                   Contains(Source.Barcode, normalized) ||
                   Contains(Source.VariantDescription, normalized) ||
                   Contains(Uom, normalized);
        }
        private static bool Contains(string? value, string search)
        {
            return (value ?? string.Empty).Contains(
                search,
                StringComparison.OrdinalIgnoreCase);
        }
    }
    public partial class PurchasingVariantEntryDialogViewModel : ObservableObject
    {
        private readonly Func<int, Task<IReadOnlyList<PurchasingVariantSource>>> _variantLoader;
        private readonly List<PurchasingVariantEntryRow> _allRows = new();
        private int _loadVersion;
        private bool _suppressSelectedItemLoad;
        public PurchasingVariantEntryDialogViewModel(
            IReadOnlyList<PurchasingItemOption> availableItems,
            Func<int, Task<IReadOnlyList<PurchasingVariantSource>>> variantLoader,
            DateTime documentDate,
            bool expiryEntryEnabled,
            string quantityLabel,
            string unitCostLabel,
            bool minimumQuantityEnabled = false,
            string dialogTitle = "Add Stock Item Variants",
            string primaryActionText = "ADD ENTERED ROWS TO GRN")
        {
            if (availableItems == null)
                throw new ArgumentNullException(nameof(availableItems));
            _variantLoader = variantLoader ?? throw new ArgumentNullException(nameof(variantLoader));
            DocumentDate = documentDate.Date;
            ExpiryEntryEnabled = expiryEntryEnabled;
            MinimumQuantityEnabled = minimumQuantityEnabled;
            QuantityLabel = string.IsNullOrWhiteSpace(quantityLabel)
                ? "Quantity"
                : quantityLabel.Trim();
            UnitCostLabel = string.IsNullOrWhiteSpace(unitCostLabel)
                ? "Unit Cost"
                : unitCostLabel.Trim();
            DialogTitle = string.IsNullOrWhiteSpace(dialogTitle)
                ? "Add Stock Item Variants"
                : dialogTitle.Trim();
            PrimaryActionText = string.IsNullOrWhiteSpace(primaryActionText)
                ? "ADD ENTERED ROWS"
                : primaryActionText.Trim();
            InstructionText = ExpiryEntryEnabled
                ? "Select a Stock Item, enter quantity, unit cost and expiry where required. Only rows with quantity greater than zero are added."
                : MinimumQuantityEnabled
                    ? "Select a Stock Item, enter order quantity and expected cost. Minimum order quantity is enforced before rows are added."
                    : "Select a Stock Item, enter quantity and unit cost. Only rows with quantity greater than zero are added.";
            foreach (PurchasingItemOption item in availableItems.OrderBy(item => item.ItemName))
                AvailableItems.Add(item);
        }
        public ObservableCollection<PurchasingItemOption> AvailableItems { get; } = new();
        public ObservableCollection<PurchasingVariantEntryRow> VisibleRows { get; } = new();
        public DateTime DocumentDate { get; }
        public bool ExpiryEntryEnabled { get; }
        public bool MinimumQuantityEnabled { get; }
        public string QuantityLabel { get; }
        public string UnitCostLabel { get; }
        public string DialogTitle { get; }
        public string InstructionText { get; }
        public string PrimaryActionText { get; }
        [ObservableProperty]
        private PurchasingItemOption? _selectedItem;
        [ObservableProperty]
        private string _filterText = string.Empty;
        [ObservableProperty]
        private decimal _bulkQuantity;
        [ObservableProperty]
        private decimal _bulkUnitCost;
        [ObservableProperty]
        private DateTime? _bulkExpiryDate;
        [ObservableProperty]
        private bool _isBusy;
        [ObservableProperty]
        private string _statusMessage = "Select a Stock Item to load its supplier-approved variants.";
        public async Task InitializeAsync()
        {
            if (SelectedItem != null || AvailableItems.Count == 0)
                return;

            _suppressSelectedItemLoad = true;

            try
            {
                SelectedItem = AvailableItems[0];
            }
            finally
            {
                _suppressSelectedItemLoad = false;
            }

            await LoadSelectedItemAsync(SelectedItem);
        }
        partial void OnSelectedItemChanged(PurchasingItemOption? value)
        {
            if (!_suppressSelectedItemLoad)
                _ = LoadSelectedItemAsync(value);
        }
        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }
        [RelayCommand]
        private void ApplyBulkQuantity()
        {
            if (VisibleRows.Count == 0)
            {
                StatusMessage = "No visible variants are available for bulk quantity entry.";
                return;
            }
            if (BulkQuantity < 0m)
            {
                StatusMessage = "Bulk quantity cannot be negative.";
                return;
            }
            PurchasingVariantEntryRow? decimalBlocked = VisibleRows.FirstOrDefault(row =>
                !row.AllowDecimalQuantity && HasDecimalPart(BulkQuantity));
            if (decimalBlocked != null)
            {
                StatusMessage =
                    $"Decimal quantity is not allowed for {decimalBlocked.DisplayName} ({decimalBlocked.Uom}).";
                return;
            }
            if (MinimumQuantityEnabled && BulkQuantity > 0m)
            {
                PurchasingVariantEntryRow? belowMinimum = VisibleRows.FirstOrDefault(row =>
                    BulkQuantity < row.MinimumQuantity);

                if (belowMinimum != null)
                {
                    StatusMessage =
                        $"{belowMinimum.DisplayName} requires minimum quantity {belowMinimum.MinimumQuantityDisplay}.";
                    return;
                }
            }
            foreach (PurchasingVariantEntryRow row in VisibleRows)
                row.Quantity = BulkQuantity;
            StatusMessage =
                $"Quantity {QuantityDisplayFormatter.Format(BulkQuantity)} applied to {VisibleRows.Count} visible variant(s).";
        }
        [RelayCommand]
        private void ApplyBulkUnitCost()
        {
            if (VisibleRows.Count == 0)
            {
                StatusMessage = "No visible variants are available for bulk cost entry.";
                return;
            }
            if (BulkUnitCost <= 0m)
            {
                StatusMessage = "Bulk unit cost must be greater than zero.";
                return;
            }
            foreach (PurchasingVariantEntryRow row in VisibleRows)
                row.UnitCost = BulkUnitCost;
            StatusMessage =
                $"Unit cost Rs. {BulkUnitCost:N2} applied to {VisibleRows.Count} visible variant(s).";
        }
        [RelayCommand]
        private void ApplyBulkExpiry()
        {
            if (!ExpiryEntryEnabled)
            {
                StatusMessage = "Expiry entry is not available for this document.";
                return;
            }
            if (!BulkExpiryDate.HasValue)
            {
                StatusMessage = "Select a bulk expiry date first.";
                return;
            }
            if (BulkExpiryDate.Value.Date < DocumentDate)
            {
                StatusMessage = "Bulk expiry date cannot be before the document received date.";
                return;
            }
            List<PurchasingVariantEntryRow> expiryRows = VisibleRows
                .Where(row => row.RequiresExpiry)
                .ToList();
            foreach (PurchasingVariantEntryRow row in expiryRows)
                row.ExpiryDate = BulkExpiryDate.Value.Date;
            StatusMessage = expiryRows.Count == 0
                ? "No visible variants require expiry."
                : $"Expiry {BulkExpiryDate.Value:yyyy-MM-dd} applied to {expiryRows.Count} visible variant(s).";
        }
        public bool TryCollectAcceptedRows(
            out IReadOnlyList<PurchasingVariantEntryRow> acceptedRows,
            out string validationMessage)
        {
            List<PurchasingVariantEntryRow> selectedRows = _allRows
                .Where(row => row.Quantity > 0m)
                .ToList();
            if (selectedRows.Count == 0)
            {
                acceptedRows = Array.Empty<PurchasingVariantEntryRow>();
                validationMessage = $"Enter {QuantityLabel.ToLowerInvariant()} for at least one variant.";
                return false;
            }
            var errors = new List<string>();
            foreach (PurchasingVariantEntryRow row in selectedRows)
            {
                if (!row.AllowDecimalQuantity && HasDecimalPart(row.Quantity))
                {
                    errors.Add(
                        $"{row.DisplayName}: decimal quantity is not allowed for UOM '{row.Uom}'.");
                }
                if (MinimumQuantityEnabled && row.Quantity < row.MinimumQuantity)
                {
                    errors.Add(
                        $"{row.DisplayName}: minimum quantity is {row.MinimumQuantityDisplay}.");
                }
                if (row.UnitCost <= 0m)
                    errors.Add($"{row.DisplayName}: unit cost must be greater than zero.");
                if (ExpiryEntryEnabled && row.RequiresExpiry && !row.ExpiryDate.HasValue)
                    errors.Add($"{row.DisplayName}: expiry date is required.");
                if (ExpiryEntryEnabled && row.ExpiryDate.HasValue && row.ExpiryDate.Value.Date < DocumentDate)
                {
                    errors.Add(
                        $"{row.DisplayName}: expiry date cannot be before {DocumentDate:yyyy-MM-dd}.");
                }
            }
            if (errors.Count > 0)
            {
                acceptedRows = Array.Empty<PurchasingVariantEntryRow>();
                validationMessage = string.Join(Environment.NewLine, errors);
                return false;
            }
            acceptedRows = selectedRows.ToList();
            validationMessage = string.Empty;
            return true;
        }
        private async Task LoadSelectedItemAsync(PurchasingItemOption? item)
        {
            int version = ++_loadVersion;
            _allRows.Clear();
            VisibleRows.Clear();
            if (item == null)
            {
                StatusMessage = "Select a Stock Item to load its supplier-approved variants.";
                return;
            }
            IsBusy = true;
            StatusMessage = $"Loading variants for {item.ItemName}...";
            try
            {
                IReadOnlyList<PurchasingVariantSource> variants =
                    await _variantLoader(item.ParentId);
                if (version != _loadVersion)
                    return;
                foreach (PurchasingVariantSource variant in variants)
                    _allRows.Add(new PurchasingVariantEntryRow(variant));
                ApplyFilter();
                StatusMessage = _allRows.Count == 0
                    ? "No supplier-approved variants were found for this item."
                    : $"{_allRows.Count} supplier-approved variant(s) loaded.";
            }
            catch (Exception ex)
            {
                if (version != _loadVersion)
                    return;
                _allRows.Clear();
                VisibleRows.Clear();
                StatusMessage = $"Variants could not be loaded: {ex.Message}";
            }
            finally
            {
                if (version == _loadVersion)
                    IsBusy = false;
            }
        }
        private void ApplyFilter()
        {
            VisibleRows.Clear();
            foreach (PurchasingVariantEntryRow row in _allRows.Where(row => row.Matches(FilterText)))
                VisibleRows.Add(row);
        }
        private static bool HasDecimalPart(decimal value)
        {
            return value != decimal.Truncate(value);
        }
    }
}

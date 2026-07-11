using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Models.DTOs;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class GrnViewModel
    {
        private int _taxPreviewVersion;
        private bool _isApplyingAuthoritativePreview;

        [ObservableProperty]
        private bool _supplierPricesIncludeVat;

        [ObservableProperty]
        private decimal _taxableAmountTotal;

        [ObservableProperty]
        private decimal _standardRatedAmount;

        [ObservableProperty]
        private decimal _zeroRatedAmount;

        [ObservableProperty]
        private decimal _exemptAmount;

        [ObservableProperty]
        private decimal _outOfScopeAmount;

        [ObservableProperty]
        private string _taxPreviewStatus = "Add GRN rows to calculate authoritative VAT.";

        public string SupplierVatStatusText
        {
            get
            {
                if (SelectedSupplier == null)
                    return "Supplier VAT status: select a supplier.";

                string status = SelectedSupplier.VatDisplayText;
                return $"Supplier VAT status: {status}. Document price mode is selected separately below.";
            }
        }

        public string SupplierPriceModeText => SupplierPricesIncludeVat
            ? "Supplier prices include VAT"
            : "Supplier prices exclude VAT";

        public int RetailPriceChangeCount => GrnLines.Count(line => line.HasRetailPriceChange);

        public int WholesalePriceChangeCount => GrnLines.Count(line => line.HasWholesalePriceChange);

        public int AnyPriceChangeCount => GrnLines.Count(line =>
            line.HasRetailPriceChange || line.HasWholesalePriceChange);

        public string PriceUpdateSummaryText => AnyPriceChangeCount == 0
            ? "No selling-price changes are selected."
            : $"{RetailPriceChangeCount} retail and {WholesalePriceChangeCount} wholesale price change(s) will be applied when the GRN is posted.";

        partial void OnSupplierPricesIncludeVatChanged(bool value)
        {
            OnPropertyChanged(nameof(SupplierPriceModeText));

            if (_isClearing)
                return;

            foreach (var line in GrnLines)
                line.IsVatIncluded = value;

            foreach (var line in _allMatrixVariants)
                line.IsVatIncluded = value;

            BulkMatrixVatIncluded = value;

            QueueRecalculate();
        }

        [RelayCommand(CanExecute = nameof(CanOpenBulkSellingPriceDialog))]
        private async Task OpenBulkSellingPriceDialogAsync()
        {
            if (!await RecalculateTotalsAuthoritativelyAsync(showErrors: true))
                return;

            var rows = GrnLines
                .Where(line => line.ReceivedQty > 0m)
                .OrderBy(line => line.ItemCode)
                .ThenBy(line => line.SkuCode)
                .ToList();

            if (rows.Count == 0)
            {
                _messageBoxService.ShowWarning(
                    "Add received items to the GRN before opening the selling-price tool.",
                    "No GRN Rows");
                return;
            }

            var dialog = new GrnBulkSellingPriceDialog(rows)
            {
                Owner = GetDialogOwner()
            };

            if (dialog.ShowDialog() != true)
                return;

            NotifyPriceUpdateSummary();
            await RecalculateTotalsAuthoritativelyAsync(showErrors: true);

            StatusMessage = PriceUpdateSummaryText;
        }

        private bool CanOpenBulkSellingPriceDialog()
        {
            return !IsBusy && IsHeaderConfirmed && GrnLines.Any(line => line.ReceivedQty > 0m);
        }

        [RelayCommand(CanExecute = nameof(CanResetProposedSellingPrices))]
        private void ResetProposedSellingPrices()
        {
            foreach (var line in GrnLines)
            {
                line.UpdateSellingPrices = false;
                line.NewRetailPrice = line.CurrentRetailPrice;
                line.NewWholesalePrice = line.CurrentWholesalePrice;
                line.NewMinimumPrice = line.CurrentMinimumPrice;
                line.NewMaximumPrice = line.CurrentMaximumPrice;
                line.RetailMarkupPercent = 0m;
                line.WholesaleMarkupPercent = 0m;
            }

            NotifyPriceUpdateSummary();
            StatusMessage = "Proposed GRN selling-price changes were cleared.";
        }

        private bool CanResetProposedSellingPrices()
        {
            return !IsBusy && GrnLines.Any(line => line.UpdateSellingPrices);
        }

        [RelayCommand]
        private async Task RefreshTaxPreviewAsync()
        {
            await RecalculateTotalsAuthoritativelyAsync(showErrors: true);
        }

        private async Task<bool> RecalculateTotalsAuthoritativelyAsync(bool showErrors = false)
        {
            int previewVersion = ++_taxPreviewVersion;

            try
            {
                var preview = await _grnRepository.CalculateGrnPreviewAsync(
                    InvoiceDate.Date,
                    SupplierPricesIncludeVat,
                    GlobalBillDiscount,
                    FreightAmount,
                    GrnLines.ToList());

                if (previewVersion != _taxPreviewVersion)
                    return false;

                _isApplyingAuthoritativePreview = true;

                try
                {
                    foreach (var line in GrnLines)
                    {
                        line.IsVatIncluded = SupplierPricesIncludeVat;
                        line.GlobalDiscountAllocation = 0m;
                        line.TaxableAmount = 0m;
                        line.VatAmount = 0m;
                        line.LineTotal = 0m;
                        line.LandedCost = 0m;
                    }

                    foreach (var result in preview.Lines)
                    {
                        if (result.SourceIndex < 0 || result.SourceIndex >= GrnLines.Count)
                            continue;

                        var line = GrnLines[result.SourceIndex];
                        line.TaxCategoryCode = result.TaxCategoryCode;
                        line.TaxCategoryName = result.TaxCategoryName;
                        line.VatRatePercent = result.VatRatePercent;
                        line.IsVatIncluded = SupplierPricesIncludeVat;
                        line.LineDiscount = result.LineDiscountAmount;
                        line.GlobalDiscountAllocation = result.GlobalDiscountAllocation;
                        line.TaxableAmount = result.TaxableAmount;
                        line.VatAmount = result.VatAmount;
                        line.LineTotal = result.TaxInclusiveAmount;
                        line.LandedCost = result.LandedCost;
                    }

                    Subtotal = preview.Subtotal;
                    TotalDiscountAmount = preview.TotalDiscount;
                    TotalVatAmount = preview.TotalVat;
                    NetPayable = preview.NetPayable;
                    TaxableAmountTotal = preview.TaxableAmountTotal;
                    StandardRatedAmount = preview.StandardRatedAmount;
                    ZeroRatedAmount = preview.ZeroRatedAmount;
                    ExemptAmount = preview.ExemptAmount;
                    OutOfScopeAmount = preview.OutOfScopeAmount;
                    TaxPreviewStatus = GrnLines.Any(line => line.ReceivedQty > 0m)
                        ? $"Authoritative tax preview calculated for {InvoiceDate:yyyy-MM-dd}."
                        : "Add GRN rows to calculate authoritative VAT.";
                }
                finally
                {
                    _isApplyingAuthoritativePreview = false;
                }

                NotifyPriceUpdateSummary();
                return true;
            }
            catch (Exception ex)
            {
                if (previewVersion != _taxPreviewVersion)
                    return false;

                TaxPreviewStatus = $"Tax preview unavailable: {ex.Message}";

                if (showErrors)
                {
                    _messageBoxService.ShowWarning(
                        ex.Message,
                        "Tax Preview Blocked");
                }

                return false;
            }
        }

        private void NotifyPriceUpdateSummary()
        {
            OnPropertyChanged(nameof(RetailPriceChangeCount));
            OnPropertyChanged(nameof(WholesalePriceChangeCount));
            OnPropertyChanged(nameof(AnyPriceChangeCount));
            OnPropertyChanged(nameof(PriceUpdateSummaryText));

            OpenBulkSellingPriceDialogCommand.NotifyCanExecuteChanged();
            ResetProposedSellingPricesCommand.NotifyCanExecuteChanged();
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

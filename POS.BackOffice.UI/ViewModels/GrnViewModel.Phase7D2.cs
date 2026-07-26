using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Models.DTOs;
using POS.Core.Configuration;
using POS.Core.Services;

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

        [ObservableProperty]
        private bool _isTaxPreviewRetryVisible;

        public string SupplierVatStatusText
        {
            get
            {
                if (SelectedSupplier == null)
                    return "Supplier VAT status: select a supplier.";

                string status = SelectedSupplier.VatDisplayText;

                if (!SelectedSupplier.HasVat)
                {
                    return $"Supplier VAT status: {status}. Supplier input VAT is zero and the VAT price-mode option is disabled.";
                }

                return $"Supplier VAT status: {status}. Match the price mode to the supplier document.";
            }
        }

        public bool CanUseSupplierVatPriceMode =>
            IsHeaderInputEnabled && SelectedSupplier?.HasVat == true;

        public string SupplierPriceModeText
        {
            get
            {
                if (SelectedSupplier == null)
                    return "Select a supplier to set the VAT price mode.";

                if (!SelectedSupplier.HasVat)
                    return "No supplier input VAT applies.";

                return SupplierPricesIncludeVat
                    ? "Supplier prices include VAT"
                    : "Supplier prices exclude VAT";
            }
        }

        public int RetailPriceChangeCount => GrnLines.Count(line => line.HasRetailPriceChange);

        public int WholesalePriceChangeCount => GrnLines.Count(line => line.HasWholesalePriceChange);

        public int MinimumPriceChangeCount => GrnLines.Count(line => line.HasMinimumPriceChange);

        public int MaximumPriceChangeCount => GrnLines.Count(line => line.HasMaximumPriceChange);

        public int AnyPriceChangeCount => GrnLines.Count(line => line.HasAnySellingPriceChange);

        public int MasterPriceActionCount => GrnLines.Count(line =>
            GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction) ==
            GrnSellingPriceActionCodes.UpdateMasterPrice);

        public int BatchOverrideActionCount => GrnLines.Count(line =>
            GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction) ==
            GrnSellingPriceActionCodes.SetBatchPriceOverride);

        public int KeepCurrentPriceActionCount => GrnLines.Count(line =>
            GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction) ==
            GrnSellingPriceActionCodes.UseCurrentMasterPrice);

        public string PriceUpdateSummaryText => MasterPriceActionCount == 0 && BatchOverrideActionCount == 0
            ? "All GRN rows will keep their current pricing authority."
            : $"{MasterPriceActionCount} master update(s), " +
              $"{BatchOverrideActionCount} batch override(s), and " +
              $"{KeepCurrentPriceActionCount} row(s) keeping current pricing will be applied when the GRN is posted.";

        partial void OnSupplierPricesIncludeVatChanged(bool value)
        {
            if (value && SelectedSupplier?.HasVat != true)
            {
                SupplierPricesIncludeVat = false;
                return;
            }

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
            try
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

                foreach (GrnLineEntryDto line in rows)
                {
                    GrnBatchPriceContextDto context = await _grnRepository.GetBatchPriceContextAsync(
                        line.ItemVariantId,
                        line.BatchNo);

                    line.CurrentRetailPrice = context.RetailPrice;
                    line.CurrentWholesalePrice = context.WholesalePrice;
                    line.CurrentMinimumPrice = context.MinimumPrice;
                    line.CurrentMaximumPrice = context.MaximumPrice;
                    line.CurrentPriceSource = context.PriceSource;

                    if (GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction) ==
                        GrnSellingPriceActionCodes.UseCurrentMasterPrice)
                    {
                        line.NewRetailPrice = context.RetailPrice;
                        line.NewWholesalePrice = context.WholesalePrice;
                        line.NewMinimumPrice = context.MinimumPrice;
                        line.NewMaximumPrice = context.MaximumPrice;
                    }
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
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Open GRN bulk selling-price dialog",
                    ex);

                _messageBoxService.ShowError(
                    "The GRN selling-price tool could not be opened or completed. " +
                    "The current GRN remains available and BackOffice will remain open. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "GRN Selling-Price Error");
            }
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
                line.SellingPriceAction = GrnSellingPriceActionCodes.UseCurrentMasterPrice;
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
            return !IsBusy && GrnLines.Any(line => line.HasAnySellingPriceChange);
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
                bool supplierIsVatRegistered =
                    SelectedSupplier?.HasVat == true;
                bool documentIsTaxInclusive =
                    supplierIsVatRegistered && SupplierPricesIncludeVat;

                var preview = await _grnRepository.CalculateGrnPreviewAsync(
                    InvoiceDate.Date,
                    SelectedSupplier?.Id ?? 0,
                    documentIsTaxInclusive,
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
                        line.IsVatIncluded = documentIsTaxInclusive;
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
                        line.IsVatIncluded = documentIsTaxInclusive;
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
                    IsTaxPreviewRetryVisible = false;
                    TaxPreviewStatus = GrnLines.Any(line => line.ReceivedQty > 0m)
                        ? supplierIsVatRegistered
                            ? $"Authoritative tax preview calculated for {InvoiceDate:yyyy-MM-dd}."
                            : "No supplier input VAT: selected supplier is not VAT registered."
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
                IsTaxPreviewRetryVisible = true;

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
            OnPropertyChanged(nameof(MinimumPriceChangeCount));
            OnPropertyChanged(nameof(MaximumPriceChangeCount));
            OnPropertyChanged(nameof(AnyPriceChangeCount));
            OnPropertyChanged(nameof(MasterPriceActionCount));
            OnPropertyChanged(nameof(BatchOverrideActionCount));
            OnPropertyChanged(nameof(KeepCurrentPriceActionCount));
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

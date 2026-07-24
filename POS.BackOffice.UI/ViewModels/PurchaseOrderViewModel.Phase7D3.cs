using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Services.Tax;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PurchaseOrderViewModel
    {
        private bool _isNormalizingGlobalBillDiscount;

        private readonly PurchasingTaxService _purchasingTaxService = new();

        [ObservableProperty]
        private bool _isTaxInclusive;

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

        public bool CanUseSupplierVatPriceMode =>
            SelectedSupplier?.HasVat == true;

        public string SupplierPriceModeText
        {
            get
            {
                if (SelectedSupplier == null)
                    return "Select a supplier to set the VAT price mode.";

                if (!SelectedSupplier.HasVat)
                    return "No supplier input VAT applies.";

                return IsTaxInclusive
                    ? "Supplier prices include VAT"
                    : "Supplier prices exclude VAT";
            }
        }

        partial void OnIsTaxInclusiveChanged(bool value)
        {
            if (value && SelectedSupplier?.HasVat != true)
            {
                IsTaxInclusive = false;
                return;
            }

            OnPropertyChanged(nameof(SupplierPriceModeText));

            foreach (var line in PoLines)
                line.IsVatIncluded = value;

            RecalculateTotals();

            StatusMessage = value
                ? "Supplier prices are treated as VAT inclusive for this Purchase Order."
                : "Supplier prices are treated as VAT exclusive for this Purchase Order.";
        }

        partial void OnOrderDateChanged(DateTime value)
        {
            if (_isClearing)
                return;

            _ = RefreshTaxProfilesAsync();
        }

        private async System.Threading.Tasks.Task RefreshTaxProfilesAsync()
        {
            var variantIds = PoLines
                .Select(line => line.ItemVariantId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (variantIds.Count == 0)
                return;

            try
            {
                if (SelectedSupplier == null)
                    return;

                var profiles = await _poRepository.GetTaxProfilesAsync(
                    variantIds,
                    OrderDate,
                    SelectedSupplier.Id);

                foreach (var line in PoLines)
                {
                    if (profiles.TryGetValue(line.ItemVariantId, out var profile))
                        ApplyProfileToLine(line, profile);
                }


                RecalculateTotals();
                StatusMessage = $"Tax profiles refreshed for PO date {OrderDate:yyyy-MM-dd}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Tax profile refresh failed.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Tax Profile Error");
            }
        }

        private void ApplyProfileToLine(
            PoLine line,
            PurchasingTaxProfile profile)
        {
            line.TaxCategoryId = profile.TaxCategoryId;
            line.TaxRateId = profile.TaxRateId;
            line.TaxCategoryCodeSnapshot = profile.TaxCategoryCode;
            line.TaxCodeSnapshot = profile.TaxCode;
            line.TaxNameSnapshot = profile.TaxName;
            line.TaxRatePercentSnapshot = profile.RatePercent;
            line.TaxCode = profile.TaxCode;
            line.VatRatePercent = profile.RatePercent;
            line.IsVatIncluded = IsTaxInclusive;
        }

        private void RecalculateAuthoritativeTotals()
        {
            if (!PoLines.Any())
            {
                Subtotal = 0m;
                TotalDiscountAmount = 0m;
                TotalTaxAmount = 0m;
                NetPayable = 0m;
                TaxableAmountTotal = 0m;
                StandardRatedAmount = 0m;
                ZeroRatedAmount = 0m;
                ExemptAmount = 0m;
                OutOfScopeAmount = 0m;
                RefreshPoLineGrid();
                SaveOrderCommand.NotifyCanExecuteChanged();
                return;
            }

            try
            {
                var inputs = PoLines
                    .Select(line =>
                    {
                        line.IsVatIncluded = IsTaxInclusive;

                        return new PurchasingTaxLineInput
                        {
                            ItemVariantId = line.ItemVariantId,
                            Quantity = line.OrderQty,
                            UnitPrice = line.ExpectedCost,
                            DiscountMode = line.LineDiscountMode,
                            DiscountValue = line.LineDiscountValue,
                            TaxProfile = BuildPreviewProfile(line)
                        };
                    })
                    .ToList();

                var beforeGlobal = _purchasingTaxService.CalculateDocument(
                    inputs,
                    0m,
                    IsTaxInclusive);

                decimal safeGlobalDiscount = Math.Round(
                    Math.Max(0m, Math.Min(GlobalBillDiscount, beforeGlobal.NetPayable)),
                    2);

                if (safeGlobalDiscount != GlobalBillDiscount)
                {
                    _isNormalizingGlobalBillDiscount = true;

                    try
                    {
                        GlobalBillDiscount = safeGlobalDiscount;
                    }
                    finally
                    {
                        _isNormalizingGlobalBillDiscount = false;
                    }
                }

                var calculation = _purchasingTaxService.CalculateDocument(
                    inputs,
                    safeGlobalDiscount,
                    IsTaxInclusive);

                for (int index = 0; index < PoLines.Count; index++)
                {
                    var line = PoLines[index];
                    var result = calculation.Lines[index];
                    var profile = result.TaxProfile;

                    line.LineDiscount = result.LineDiscountAmount;
                    line.GlobalDiscountAllocation = result.GlobalDiscountAllocation;
                    line.TaxableAmountPreview = result.TaxableAmount;
                    line.TaxAmount = result.VatAmount;
                    line.LineTotal = result.TaxInclusiveAmount;
                    line.TaxCode = profile.TaxCode;
                    line.VatRatePercent = profile.RatePercent;
                    line.IsVatIncluded = IsTaxInclusive;

                    line.TaxCategoryId = profile.TaxCategoryId;
                    line.TaxRateId = profile.TaxRateId;
                    line.TaxCategoryCodeSnapshot = profile.TaxCategoryCode;
                    line.TaxCodeSnapshot = profile.TaxCode;
                    line.TaxNameSnapshot = profile.TaxName;
                    line.TaxRatePercentSnapshot = profile.RatePercent;
                    line.IsTaxInclusiveSnapshot = IsTaxInclusive;
                    line.TaxableAmountSnapshot = result.TaxableAmount;
                    line.VatAmountSnapshot = result.VatAmount;
                    line.TaxInclusiveAmountSnapshot = result.TaxInclusiveAmount;
                    line.TaxSnapshotStatus = TaxSnapshotStatuses.Complete;
                }

                Subtotal = calculation.Subtotal;
                TotalDiscountAmount = calculation.TotalDiscount;
                TotalTaxAmount = calculation.TotalVat;
                NetPayable = calculation.NetPayable;
                TaxableAmountTotal = calculation.TaxableAmountTotal;
                StandardRatedAmount = calculation.StandardRatedAmount;
                ZeroRatedAmount = calculation.ZeroRatedAmount;
                ExemptAmount = calculation.ExemptAmount;
                OutOfScopeAmount = calculation.OutOfScopeAmount;

                RefreshPoLineGrid();
                SaveOrderCommand.NotifyCanExecuteChanged();
            }
            catch (InvalidOperationException ex)
            {
                foreach (var line in PoLines)
                    RecalculateLine(line);

                Subtotal = Math.Round(
                    PoLines.Sum(line => line.OrderQty * line.ExpectedCost),
                    2);

                TotalDiscountAmount = Math.Round(
                    PoLines.Sum(line => line.LineDiscount) +
                    Math.Max(0m, GlobalBillDiscount),
                    2);

                TotalTaxAmount = Math.Round(
                    PoLines.Sum(line => line.TaxAmount),
                    2);

                NetPayable = Math.Round(
                    Math.Max(
                        0m,
                        PoLines.Sum(line => line.LineTotal) -
                        Math.Max(0m, GlobalBillDiscount)),
                    2);

                TaxableAmountTotal = 0m;
                StandardRatedAmount = 0m;
                ZeroRatedAmount = 0m;
                ExemptAmount = 0m;
                OutOfScopeAmount = 0m;

                StatusMessage = ex.Message;

                RefreshPoLineGrid();
                SaveOrderCommand.NotifyCanExecuteChanged();
            }
        }

        private static PurchasingTaxProfile BuildPreviewProfile(PoLine line)
        {
            if (!line.TaxCategoryId.HasValue ||
                string.IsNullOrWhiteSpace(line.TaxCategoryCodeSnapshot))
            {
                throw new InvalidOperationException(
                    $"Item '{line.DisplayName}' has no authoritative Tax Category.");
            }

            return new PurchasingTaxProfile
            {
                ItemVariantId = line.ItemVariantId,
                TaxCategoryId = line.TaxCategoryId.Value,
                TaxCategoryCode = line.TaxCategoryCodeSnapshot,
                TaxCategoryName = line.TaxNameSnapshot ?? line.TaxCategoryCodeSnapshot,
                TaxTreatmentType = string.Empty,
                TaxRateId = line.TaxRateId,
                TaxCode = string.IsNullOrWhiteSpace(line.TaxCodeSnapshot)
                    ? line.TaxCode
                    : line.TaxCodeSnapshot,
                TaxName = string.IsNullOrWhiteSpace(line.TaxNameSnapshot)
                    ? line.TaxCategoryCodeSnapshot
                    : line.TaxNameSnapshot,
                RatePercent = line.TaxRatePercentSnapshot ?? line.VatRatePercent
            };
        }
    }
}

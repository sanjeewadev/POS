using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Models.DTOs;
using POS.Core.Services.Pricing;

namespace POS.BackOffice.UI.ViewModels
{
    public sealed record MasterPriceQuickChangeResult(
        int ItemVariantId,
        decimal MinimumPrice,
        decimal RetailPrice,
        decimal WholesalePrice,
        decimal MaximumPrice);

    public sealed class PriceRoundingOption
    {
        public PriceRoundingOption(string label, decimal increment)
        {
            Label = label;
            Increment = increment;
        }

        public string Label { get; }
        public decimal Increment { get; }
        public override string ToString() => Label;
    }

    public sealed class MasterPriceQuickChangeDialogViewModel : ObservableObject
    {
        private string _selectedCostBasis;
        private string _selectedRetailMethod = SellingPriceCalculationMethods.Manual;
        private string _selectedWholesaleMethod = SellingPriceCalculationMethods.Manual;
        private PriceRoundingOption _selectedRounding;
        private string _retailCalculationInput = "25";
        private string _wholesaleCalculationInput = "15";
        private string _minimumText;
        private string _retailText;
        private string _wholesaleText;
        private string _maximumText;
        private string _validationMessage = string.Empty;

        public MasterPriceQuickChangeDialogViewModel(PriceManagementSummaryDto source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            _selectedCostBasis = source.MovingAverageCost > 0m
                ? SellingPriceCostBasisCodes.AverageCost
                : SellingPriceCostBasisCodes.LastCost;
            RoundingOptions = new[]
            {
                new PriceRoundingOption("Exact cents", 0.01m),
                new PriceRoundingOption("Nearest Rs. 1", 1m),
                new PriceRoundingOption("Nearest Rs. 5", 5m),
                new PriceRoundingOption("Nearest Rs. 10", 10m),
                new PriceRoundingOption("Nearest Rs. 50", 50m),
                new PriceRoundingOption("Nearest Rs. 100", 100m)
            };
            _selectedRounding = RoundingOptions[0];
            _minimumText = Money(source.MinimumPrice);
            _retailText = Money(source.RetailPrice);
            _wholesaleText = Money(source.WholesalePrice);
            _maximumText = Money(source.MaximumPrice);
        }

        public PriceManagementSummaryDto Source { get; }
        public string DisplayDescription => Source.DisplayDescription;
        public string SkuCode => Source.SkuCode;
        public string ItemTypeText => Source.ItemTypeText;
        public string TrackingText => Source.TrackingText;
        public decimal AverageCost => Source.MovingAverageCost;
        public decimal LastCost => Source.LastLandedCost;
        public decimal CurrentMinimum => Source.OriginalMinimumPrice;
        public decimal CurrentRetail => Source.OriginalRetailPrice;
        public decimal CurrentWholesale => Source.OriginalWholesalePrice;
        public decimal CurrentMaximum => Source.OriginalMaximumPrice;

        public IReadOnlyList<string> CostBasisOptions { get; } = new[]
        {
            SellingPriceCostBasisCodes.AverageCost,
            SellingPriceCostBasisCodes.LastCost
        };

        public IReadOnlyList<string> RetailMethodOptions { get; } = new[]
        {
            SellingPriceCalculationMethods.Manual,
            SellingPriceCalculationMethods.MarkupOnCost,
            SellingPriceCalculationMethods.TargetMargin
        };

        public IReadOnlyList<string> WholesaleMethodOptions { get; } = new[]
        {
            SellingPriceCalculationMethods.Manual,
            SellingPriceCalculationMethods.MarkupOnCost,
            SellingPriceCalculationMethods.TargetMargin,
            SellingPriceCalculationMethods.DiscountFromRetail
        };

        public IReadOnlyList<PriceRoundingOption> RoundingOptions { get; }

        public string SelectedCostBasis
        {
            get => _selectedCostBasis;
            set
            {
                if (SetProperty(ref _selectedCostBasis, value))
                {
                    OnPropertyChanged(nameof(SelectedCost));
                    NotifyPreview();
                }
            }
        }

        public string SelectedRetailMethod
        {
            get => _selectedRetailMethod;
            set
            {
                if (SetProperty(ref _selectedRetailMethod, value))
                    OnPropertyChanged(nameof(RetailInputLabel));
            }
        }

        public string SelectedWholesaleMethod
        {
            get => _selectedWholesaleMethod;
            set
            {
                if (SetProperty(ref _selectedWholesaleMethod, value))
                    OnPropertyChanged(nameof(WholesaleInputLabel));
            }
        }

        public PriceRoundingOption SelectedRounding
        {
            get => _selectedRounding;
            set => SetProperty(ref _selectedRounding, value);
        }

        public string RetailCalculationInput
        {
            get => _retailCalculationInput;
            set => SetProperty(ref _retailCalculationInput, value);
        }

        public string WholesaleCalculationInput
        {
            get => _wholesaleCalculationInput;
            set => SetProperty(ref _wholesaleCalculationInput, value);
        }

        public string MinimumText
        {
            get => _minimumText;
            set
            {
                if (SetProperty(ref _minimumText, value))
                    NotifyPreview();
            }
        }

        public string RetailText
        {
            get => _retailText;
            set
            {
                if (SetProperty(ref _retailText, value))
                    NotifyPreview();
            }
        }

        public string WholesaleText
        {
            get => _wholesaleText;
            set
            {
                if (SetProperty(ref _wholesaleText, value))
                    NotifyPreview();
            }
        }

        public string MaximumText
        {
            get => _maximumText;
            set
            {
                if (SetProperty(ref _maximumText, value))
                    NotifyPreview();
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set => SetProperty(ref _validationMessage, value);
        }

        public decimal SelectedCost =>
            SelectedCostBasis == SellingPriceCostBasisCodes.LastCost
                ? LastCost
                : AverageCost;

        public string RetailInputLabel => SelectedRetailMethod switch
        {
            SellingPriceCalculationMethods.MarkupOnCost => "Retail markup %",
            SellingPriceCalculationMethods.TargetMargin => "Retail target margin %",
            _ => "Retail calculation"
        };

        public string WholesaleInputLabel => SelectedWholesaleMethod switch
        {
            SellingPriceCalculationMethods.MarkupOnCost => "Wholesale markup %",
            SellingPriceCalculationMethods.TargetMargin => "Wholesale target margin %",
            SellingPriceCalculationMethods.DiscountFromRetail => "Discount from Retail %",
            _ => "Wholesale calculation"
        };

        public decimal PreviewRetail => TryMoney(RetailText, out decimal value) ? value : 0m;
        public decimal PreviewWholesale => TryMoney(WholesaleText, out decimal value) ? value : 0m;
        public decimal RetailProfit => SellingPriceSuggestionCalculator.ProfitPerUnit(PreviewRetail, SelectedCost);
        public decimal RetailMargin => SellingPriceSuggestionCalculator.MarginPercent(PreviewRetail, SelectedCost);
        public decimal WholesaleProfit => SellingPriceSuggestionCalculator.ProfitPerUnit(PreviewWholesale, SelectedCost);
        public decimal WholesaleMargin => SellingPriceSuggestionCalculator.MarginPercent(PreviewWholesale, SelectedCost);
        public bool RetailBelowCost => SelectedCost > 0m && PreviewRetail > 0m && PreviewRetail < SelectedCost;
        public bool WholesaleBelowCost => SelectedCost > 0m && PreviewWholesale > 0m && PreviewWholesale < SelectedCost;
        public string CostWarning => RetailBelowCost || WholesaleBelowCost
            ? "Warning: one or more proposed prices are below the selected cost basis."
            : string.Empty;

        public bool TryCalculateSuggestions()
        {
            ValidationMessage = string.Empty;
            decimal cost = SelectedCost;

            try
            {
                decimal retail = PreviewRetail;
                if (SelectedRetailMethod != SellingPriceCalculationMethods.Manual)
                {
                    if (!TryMoney(RetailCalculationInput, out decimal retailInput))
                        throw new InvalidOperationException("Enter a valid Retail percentage.");

                    retail = SelectedRetailMethod switch
                    {
                        SellingPriceCalculationMethods.MarkupOnCost =>
                            SellingPriceSuggestionCalculator.FromMarkup(cost, retailInput, SelectedRounding.Increment),
                        SellingPriceCalculationMethods.TargetMargin =>
                            SellingPriceSuggestionCalculator.FromTargetMargin(cost, retailInput, SelectedRounding.Increment),
                        _ => retail
                    };
                    RetailText = Money(retail);
                }

                if (SelectedWholesaleMethod != SellingPriceCalculationMethods.Manual)
                {
                    if (!TryMoney(WholesaleCalculationInput, out decimal wholesaleInput))
                        throw new InvalidOperationException("Enter a valid Wholesale percentage.");

                    decimal wholesale = SelectedWholesaleMethod switch
                    {
                        SellingPriceCalculationMethods.MarkupOnCost =>
                            SellingPriceSuggestionCalculator.FromMarkup(cost, wholesaleInput, SelectedRounding.Increment),
                        SellingPriceCalculationMethods.TargetMargin =>
                            SellingPriceSuggestionCalculator.FromTargetMargin(cost, wholesaleInput, SelectedRounding.Increment),
                        SellingPriceCalculationMethods.DiscountFromRetail =>
                            SellingPriceSuggestionCalculator.FromRetailDiscount(retail, wholesaleInput, SelectedRounding.Increment),
                        _ => PreviewWholesale
                    };
                    WholesaleText = Money(wholesale);
                }

                NotifyPreview();
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                ValidationMessage = ex.Message;
                return false;
            }
        }

        public bool TryBuildResult(out MasterPriceQuickChangeResult? result)
        {
            result = null;
            ValidationMessage = string.Empty;

            if (!TryMoney(MinimumText, out decimal minimum) || minimum < 0m)
                return Fail("Minimum price must be a valid non-negative amount.");
            if (!TryMoney(RetailText, out decimal retail) || retail <= 0m)
                return Fail("Retail price must be greater than zero.");
            if (!TryMoney(WholesaleText, out decimal wholesale) || wholesale < 0m)
                return Fail("Wholesale price must be a valid non-negative amount.");
            if (!TryMoney(MaximumText, out decimal maximum) || maximum < 0m)
                return Fail("Maximum price must be a valid non-negative amount.");
            if (minimum > 0m && maximum > 0m && minimum > maximum)
                return Fail("Minimum price cannot be greater than Maximum price.");
            if (minimum > 0m && retail < minimum)
                return Fail("Retail price cannot be below Minimum price.");
            if (minimum > 0m && wholesale > 0m && wholesale < minimum)
                return Fail("Wholesale price cannot be below Minimum price.");
            if (maximum > 0m && retail > maximum)
                return Fail("Retail price cannot be above Maximum price.");
            if (maximum > 0m && wholesale > maximum)
                return Fail("Wholesale price cannot be above Maximum price.");
            if (wholesale > retail)
                return Fail("Wholesale price cannot be greater than Retail price.");

            minimum = RoundMoney(minimum);
            retail = RoundMoney(retail);
            wholesale = RoundMoney(wholesale);
            maximum = RoundMoney(maximum);

            if (minimum == CurrentMinimum &&
                retail == CurrentRetail &&
                wholesale == CurrentWholesale &&
                maximum == CurrentMaximum)
            {
                return Fail("No master price has changed.");
            }

            result = new MasterPriceQuickChangeResult(
                Source.ItemVariantId,
                minimum,
                retail,
                wholesale,
                maximum);
            return true;
        }

        private bool Fail(string message)
        {
            ValidationMessage = message;
            return false;
        }

        private void NotifyPreview()
        {
            OnPropertyChanged(nameof(SelectedCost));
            OnPropertyChanged(nameof(PreviewRetail));
            OnPropertyChanged(nameof(PreviewWholesale));
            OnPropertyChanged(nameof(RetailProfit));
            OnPropertyChanged(nameof(RetailMargin));
            OnPropertyChanged(nameof(WholesaleProfit));
            OnPropertyChanged(nameof(WholesaleMargin));
            OnPropertyChanged(nameof(RetailBelowCost));
            OnPropertyChanged(nameof(WholesaleBelowCost));
            OnPropertyChanged(nameof(CostWarning));
        }

        private static bool TryMoney(string? text, out decimal value)
        {
            string normalized = (text ?? string.Empty).Trim();
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
                   decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static string Money(decimal value) =>
            RoundMoney(value).ToString("0.00", CultureInfo.CurrentCulture);

        private static decimal RoundMoney(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

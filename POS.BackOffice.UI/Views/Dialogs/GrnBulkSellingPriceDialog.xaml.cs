using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using POS.Core.Models.DTOs;
using POS.Core.Services.Pricing;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class GrnBulkSellingPriceDialog : Window
    {
        private readonly GrnSellingPriceCalculator _calculator = new();
        private bool _isReady;

        public ObservableCollection<GrnBulkSellingPricePreviewRow> Rows { get; } = new();

        public GrnBulkSellingPriceDialog(System.Collections.Generic.IReadOnlyList<GrnLineEntryDto> lines)
        {
            InitializeComponent();
            DataContext = this;

            cmbRetailMethod.ItemsSource = new[]
            {
                GrnSellingPriceMethods.KeepCurrent,
                GrnSellingPriceMethods.SetExactPrice,
                GrnSellingPriceMethods.MarkupFromLandedCost,
                GrnSellingPriceMethods.ChangeCurrentByPercent
            };

            cmbWholesaleMethod.ItemsSource = new[]
            {
                GrnSellingPriceMethods.KeepCurrent,
                GrnSellingPriceMethods.SetExactPrice,
                GrnSellingPriceMethods.MarkupFromLandedCost,
                GrnSellingPriceMethods.ChangeCurrentByPercent
            };

            cmbRounding.ItemsSource = new[]
            {
                GrnSellingPriceRoundingModes.None,
                GrnSellingPriceRoundingModes.NearestOne,
                GrnSellingPriceRoundingModes.NearestFive,
                GrnSellingPriceRoundingModes.NearestTen
            };

            cmbRetailMethod.SelectedItem = GrnSellingPriceMethods.KeepCurrent;
            cmbWholesaleMethod.SelectedItem = GrnSellingPriceMethods.KeepCurrent;
            cmbRounding.SelectedItem = GrnSellingPriceRoundingModes.None;
            txtRetailValue.Text = "0";
            txtWholesaleValue.Text = "0";

            foreach (var line in lines)
            {
                var row = new GrnBulkSellingPricePreviewRow(line)
                {
                    Apply = true
                };

                row.PropertyChanged += PreviewRow_PropertyChanged;
                Rows.Add(row);
            }

            _isReady = true;
            RecalculatePreview();
        }

        private void PricingSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isReady)
                RecalculatePreview();
        }

        private void PricingTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isReady)
                RecalculatePreview();
        }


        private void PreviewRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_isReady || e.PropertyName != nameof(GrnBulkSellingPricePreviewRow.Apply))
                return;

            Dispatcher.BeginInvoke(new Action(UpdateSummary));
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in Rows)
                row.Apply = true;

            UpdateSummary();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in Rows)
                row.Apply = false;

            UpdateSummary();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            RecalculatePreview();

            var selected = Rows.Where(row => row.Apply).ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Select at least one GRN row.",
                    "Bulk Selling Price Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var invalid = selected.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.ValidationMessage));

            if (invalid != null)
            {
                MessageBox.Show(
                    $"Cannot apply prices because '{invalid.DisplayName}' is invalid:\n\n{invalid.ValidationMessage}",
                    "Bulk Selling Price Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var wholesaleAboveRetail = selected
                .Where(row =>
                    row.NewRetailPrice > 0m &&
                    row.NewWholesalePrice > row.NewRetailPrice)
                .ToList();

            if (wholesaleAboveRetail.Count > 0)
            {
                var answer = MessageBox.Show(
                    $"{wholesaleAboveRetail.Count} selected row(s) have a wholesale price above the retail price.\n\nApply these proposed prices anyway?",
                    "Wholesale Price Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (answer != MessageBoxResult.Yes)
                    return;
            }

            string retailMethod = cmbRetailMethod.SelectedItem?.ToString() ?? GrnSellingPriceMethods.KeepCurrent;
            string wholesaleMethod = cmbWholesaleMethod.SelectedItem?.ToString() ?? GrnSellingPriceMethods.KeepCurrent;
            decimal retailValue = ParseDecimal(txtRetailValue.Text);
            decimal wholesaleValue = ParseDecimal(txtWholesaleValue.Text);

            foreach (var row in Rows)
            {
                if (!row.Apply)
                    continue;

                var line = row.SourceLine;
                line.NewRetailPrice = row.NewRetailPrice;
                line.NewWholesalePrice = row.NewWholesalePrice;
                line.NewMinimumPrice = line.CurrentMinimumPrice;
                line.NewMaximumPrice = line.CurrentMaximumPrice;
                line.RetailMarkupPercent = retailMethod == GrnSellingPriceMethods.MarkupFromLandedCost
                    ? retailValue
                    : 0m;
                line.WholesaleMarkupPercent = wholesaleMethod == GrnSellingPriceMethods.MarkupFromLandedCost
                    ? wholesaleValue
                    : 0m;

                bool retailChanged = RoundMoney(line.CurrentRetailPrice) != RoundMoney(line.NewRetailPrice);
                bool wholesaleChanged = RoundMoney(line.CurrentWholesalePrice) != RoundMoney(line.NewWholesalePrice);
                line.UpdateSellingPrices = retailChanged || wholesaleChanged;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RecalculatePreview()
        {
            string retailMethod = cmbRetailMethod.SelectedItem?.ToString() ?? GrnSellingPriceMethods.KeepCurrent;
            string wholesaleMethod = cmbWholesaleMethod.SelectedItem?.ToString() ?? GrnSellingPriceMethods.KeepCurrent;
            string rounding = cmbRounding.SelectedItem?.ToString() ?? GrnSellingPriceRoundingModes.None;

            bool retailValueValid = TryParseDecimal(txtRetailValue.Text, out decimal retailValue);
            bool wholesaleValueValid = TryParseDecimal(txtWholesaleValue.Text, out decimal wholesaleValue);

            foreach (var row in Rows)
            {
                row.ValidationMessage = string.Empty;

                try
                {
                    if (!retailValueValid && retailMethod != GrnSellingPriceMethods.KeepCurrent)
                        throw new InvalidOperationException("Retail value is not a valid number.");

                    if (!wholesaleValueValid && wholesaleMethod != GrnSellingPriceMethods.KeepCurrent)
                        throw new InvalidOperationException("Wholesale value is not a valid number.");

                    row.NewRetailPrice = _calculator.Calculate(
                        row.CurrentRetailPrice,
                        row.LandedCost,
                        row.VatRatePercent,
                        retailMethod,
                        retailValue,
                        rounding);

                    row.NewWholesalePrice = _calculator.Calculate(
                        row.CurrentWholesalePrice,
                        row.LandedCost,
                        row.VatRatePercent,
                        wholesaleMethod,
                        wholesaleValue,
                        rounding);
                }
                catch (Exception ex)
                {
                    row.NewRetailPrice = row.CurrentRetailPrice;
                    row.NewWholesalePrice = row.CurrentWholesalePrice;
                    row.ValidationMessage = ex.Message;
                }
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int selected = Rows.Count(row => row.Apply);
            int retailChanges = Rows.Count(row =>
                row.Apply && RoundMoney(row.CurrentRetailPrice) != RoundMoney(row.NewRetailPrice));
            int wholesaleChanges = Rows.Count(row =>
                row.Apply && RoundMoney(row.CurrentWholesalePrice) != RoundMoney(row.NewWholesalePrice));

            txtPreviewSummary.Text =
                $"Selected: {selected} | Retail changes: {retailChanges} | Wholesale changes: {wholesaleChanges}";
        }

        private static bool TryParseDecimal(string? value, out decimal result)
        {
            string text = (value ?? string.Empty).Trim();

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out result))
                return true;

            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        }

        private static decimal ParseDecimal(string? value)
        {
            return TryParseDecimal(value, out decimal result) ? result : 0m;
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class GrnBulkSellingPricePreviewRow : INotifyPropertyChanged
    {
        private bool _apply;
        private decimal _newRetailPrice;
        private decimal _newWholesalePrice;
        private string _validationMessage = string.Empty;

        public GrnBulkSellingPricePreviewRow(GrnLineEntryDto sourceLine)
        {
            SourceLine = sourceLine ?? throw new ArgumentNullException(nameof(sourceLine));
            NewRetailPrice = sourceLine.NewRetailPrice > 0m
                ? sourceLine.NewRetailPrice
                : sourceLine.CurrentRetailPrice;
            NewWholesalePrice = sourceLine.NewWholesalePrice > 0m
                ? sourceLine.NewWholesalePrice
                : sourceLine.CurrentWholesalePrice;
        }

        public GrnLineEntryDto SourceLine { get; }

        public bool Apply
        {
            get => _apply;
            set => SetField(ref _apply, value);
        }

        public string DisplayName => SourceLine.DisplayName;

        public string SkuCode => SourceLine.SkuCode;

        public decimal LandedCost => SourceLine.LandedCost;

        public decimal VatRatePercent => SourceLine.VatRatePercent;

        public decimal CurrentRetailPrice => SourceLine.CurrentRetailPrice;

        public decimal CurrentWholesalePrice => SourceLine.CurrentWholesalePrice;

        public decimal NewRetailPrice
        {
            get => _newRetailPrice;
            set => SetField(ref _newRetailPrice, value);
        }

        public decimal NewWholesalePrice
        {
            get => _newWholesalePrice;
            set => SetField(ref _newWholesalePrice, value);
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetField(ref _validationMessage, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

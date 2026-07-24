using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Models.DTOs;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class GrnBulkSellingPriceDialog : Window
    {
        private bool _isCompleting;

        public GrnBulkSellingPriceDialog(
            IReadOnlyList<GrnLineEntryDto> lines)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            foreach (GrnLineEntryDto line in lines)
            {
                var row = new GrnBulkSellingPricePreviewRow(line)
                {
                    Apply = true
                };

                row.PropertyChanged += Row_PropertyChanged;
                Rows.Add(row);
            }

            InitializeComponent();
            DataContext = this;
            UpdateSummary();
        }

        public ObservableCollection<GrnBulkSellingPricePreviewRow> Rows { get; } = new();

        private void ApplyBulkValues_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();

            if (!TryParseOptionalMoney(txtBulkRetail.Text, "Retail Price", out decimal? retail) ||
                !TryParseOptionalMoney(txtBulkWholesale.Text, "Wholesale Price", out decimal? wholesale) ||
                !TryParseOptionalMoney(txtBulkMinimum.Text, "Minimum Price", out decimal? minimum) ||
                !TryParseOptionalMoney(txtBulkMaximum.Text, "Maximum Price", out decimal? maximum))
            {
                return;
            }

            if (!retail.HasValue &&
                !wholesale.HasValue &&
                !minimum.HasValue &&
                !maximum.HasValue)
            {
                MessageBox.Show(
                    this,
                    "Enter at least one bulk selling-price value.",
                    "No Bulk Value",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            List<GrnBulkSellingPricePreviewRow> selected = Rows
                .Where(row => row.Apply)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Select at least one item row before applying bulk values.",
                    "No Rows Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            foreach (GrnBulkSellingPricePreviewRow row in selected)
            {
                if (retail.HasValue)
                    row.NewRetailPrice = RoundMoney(retail.Value);

                if (wholesale.HasValue)
                    row.NewWholesalePrice = RoundMoney(wholesale.Value);

                if (minimum.HasValue)
                    row.NewMinimumPrice = RoundMoney(minimum.Value);

                if (maximum.HasValue)
                    row.NewMaximumPrice = RoundMoney(maximum.Value);
            }

            UpdateSummary();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (GrnBulkSellingPricePreviewRow row in Rows)
                row.Apply = true;

            UpdateSummary();
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (GrnBulkSellingPricePreviewRow row in Rows)
                row.Apply = false;

            UpdateSummary();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleting)
                return;

            _isCompleting = true;

            try
            {
                Keyboard.ClearFocus();

                bool cellCommitted = PriceGrid.CommitEdit(
                    DataGridEditingUnit.Cell,
                    true);
                bool rowCommitted = PriceGrid.CommitEdit(
                    DataGridEditingUnit.Row,
                    true);

                if (!cellCommitted || !rowCommitted)
                {
                    MessageBox.Show(
                        this,
                        "The active selling-price value could not be committed. Correct it and try again.",
                        "Selling Price Not Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                List<GrnBulkSellingPricePreviewRow> selected = Rows
                    .Where(row => row.Apply)
                    .ToList();

                if (selected.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        "Select at least one item row.",
                        "No Rows Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                GrnBulkSellingPricePreviewRow? invalid = selected.FirstOrDefault(row =>
                    !string.IsNullOrWhiteSpace(row.ValidationMessage));

                if (invalid != null)
                {
                    MessageBox.Show(
                        this,
                        $"Cannot apply prices for '{invalid.DisplayName}'.\n\n{invalid.ValidationMessage}",
                        "Invalid Selling Price",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                List<GrnBulkSellingPricePreviewRow> changed = selected
                    .Where(row => row.HasAnyChange)
                    .ToList();

                if (changed.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        "No selling prices were changed in the selected rows.",
                        "No Price Changes",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                int wholesaleAboveRetailCount = changed.Count(row =>
                    row.NewRetailPrice > 0m &&
                    row.NewWholesalePrice > row.NewRetailPrice);

                if (wholesaleAboveRetailCount > 0)
                {
                    MessageBoxResult answer = MessageBox.Show(
                        this,
                        $"{wholesaleAboveRetailCount} selected row(s) have a wholesale price above retail.\n\nContinue with these proposed prices?",
                        "Wholesale Price Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (answer != MessageBoxResult.Yes)
                        return;
                }

                foreach (GrnBulkSellingPricePreviewRow row in changed)
                {
                    GrnLineEntryDto line = row.SourceLine;
                    line.NewRetailPrice = RoundMoney(row.NewRetailPrice);
                    line.NewWholesalePrice = RoundMoney(row.NewWholesalePrice);
                    line.NewMinimumPrice = RoundMoney(row.NewMinimumPrice);
                    line.NewMaximumPrice = RoundMoney(row.NewMaximumPrice);
                    line.RetailMarkupPercent = 0m;
                    line.WholesaleMarkupPercent = 0m;
                    line.UpdateSellingPrices = row.HasAnyChange;
                }

                DialogResult = true;
            }
            finally
            {
                _isCompleting = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleting)
                return;

            DialogResult = false;
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateSummary));
        }

        private void UpdateSummary()
        {
            if (txtSummary == null)
                return;

            int selectedCount = Rows.Count(row => row.Apply);
            int changedCount = Rows.Count(row => row.Apply && row.HasAnyChange);
            txtSummary.Text = $"{selectedCount} selected | {changedCount} with proposed changes";
        }

        private bool TryParseOptionalMoney(
            string? text,
            string label,
            out decimal? value)
        {
            string normalized = (text ?? string.Empty).Trim();

            if (normalized.Length == 0)
            {
                value = null;
                return true;
            }

            if (!decimal.TryParse(
                    normalized,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal parsed) &&
                !decimal.TryParse(
                    normalized,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out parsed))
            {
                MessageBox.Show(
                    this,
                    $"{label} must be a valid number or left blank.",
                    "Invalid Bulk Value",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                value = null;
                return false;
            }

            if (parsed < 0m)
            {
                MessageBox.Show(
                    this,
                    $"{label} cannot be negative.",
                    "Invalid Bulk Value",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                value = null;
                return false;
            }

            value = RoundMoney(parsed);
            return true;
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }

    public partial class GrnBulkSellingPricePreviewRow : ObservableObject
    {
        public GrnBulkSellingPricePreviewRow(GrnLineEntryDto sourceLine)
        {
            SourceLine = sourceLine ?? throw new ArgumentNullException(nameof(sourceLine));
            _newRetailPrice = sourceLine.UpdateSellingPrices
                ? sourceLine.NewRetailPrice
                : sourceLine.CurrentRetailPrice;
            _newWholesalePrice = sourceLine.UpdateSellingPrices
                ? sourceLine.NewWholesalePrice
                : sourceLine.CurrentWholesalePrice;
            _newMinimumPrice = sourceLine.UpdateSellingPrices
                ? sourceLine.NewMinimumPrice
                : sourceLine.CurrentMinimumPrice;
            _newMaximumPrice = sourceLine.UpdateSellingPrices
                ? sourceLine.NewMaximumPrice
                : sourceLine.CurrentMaximumPrice;
        }

        public GrnLineEntryDto SourceLine { get; }

        public string DisplayName => SourceLine.DisplayName;

        public string SkuCode => SourceLine.SkuCode;

        public decimal LandedCost => SourceLine.LandedCost;

        public decimal CurrentRetailPrice => SourceLine.CurrentRetailPrice;

        public decimal CurrentWholesalePrice => SourceLine.CurrentWholesalePrice;

        public decimal CurrentMinimumPrice => SourceLine.CurrentMinimumPrice;

        public decimal CurrentMaximumPrice => SourceLine.CurrentMaximumPrice;

        [ObservableProperty]
        private bool _apply;

        [ObservableProperty]
        private decimal _newRetailPrice;

        [ObservableProperty]
        private decimal _newWholesalePrice;

        [ObservableProperty]
        private decimal _newMinimumPrice;

        [ObservableProperty]
        private decimal _newMaximumPrice;

        public bool HasRetailChange =>
            RoundMoney(CurrentRetailPrice) != RoundMoney(NewRetailPrice);

        public bool HasWholesaleChange =>
            RoundMoney(CurrentWholesalePrice) != RoundMoney(NewWholesalePrice);

        public bool HasMinimumChange =>
            RoundMoney(CurrentMinimumPrice) != RoundMoney(NewMinimumPrice);

        public bool HasMaximumChange =>
            RoundMoney(CurrentMaximumPrice) != RoundMoney(NewMaximumPrice);

        public bool HasAnyChange =>
            HasRetailChange ||
            HasWholesaleChange ||
            HasMinimumChange ||
            HasMaximumChange;

        public string ValidationMessage
        {
            get
            {
                if (NewRetailPrice < 0m ||
                    NewWholesalePrice < 0m ||
                    NewMinimumPrice < 0m ||
                    NewMaximumPrice < 0m)
                {
                    return "Selling prices cannot be negative.";
                }

                if (HasRetailChange && NewRetailPrice <= 0m)
                    return "A changed Retail Price must be greater than zero.";

                if (HasWholesaleChange && NewWholesalePrice <= 0m)
                    return "A changed Wholesale Price must be greater than zero.";

                if (NewMaximumPrice > 0m && NewMinimumPrice > NewMaximumPrice)
                    return "Minimum Price cannot be greater than Maximum Price.";

                return string.Empty;
            }
        }

        public string WarningText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ValidationMessage))
                    return ValidationMessage;

                if (NewRetailPrice > 0m && NewWholesalePrice > NewRetailPrice)
                    return "Wholesale is above retail";

                return HasAnyChange ? "Proposed" : "Keep current";
            }
        }

        partial void OnApplyChanged(bool value)
        {
            RaiseDerivedProperties();
        }

        partial void OnNewRetailPriceChanged(decimal value)
        {
            RaiseDerivedProperties();
        }

        partial void OnNewWholesalePriceChanged(decimal value)
        {
            RaiseDerivedProperties();
        }

        partial void OnNewMinimumPriceChanged(decimal value)
        {
            RaiseDerivedProperties();
        }

        partial void OnNewMaximumPriceChanged(decimal value)
        {
            RaiseDerivedProperties();
        }

        private void RaiseDerivedProperties()
        {
            OnPropertyChanged(nameof(HasRetailChange));
            OnPropertyChanged(nameof(HasWholesaleChange));
            OnPropertyChanged(nameof(HasMinimumChange));
            OnPropertyChanged(nameof(HasMaximumChange));
            OnPropertyChanged(nameof(HasAnyChange));
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(WarningText));
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}

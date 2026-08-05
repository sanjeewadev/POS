using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Configuration;
using POS.Core.Models.DTOs;
using POS.Core.Services.Pricing;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class GrnBulkSellingPriceDialog : Window
    {
        private bool _isCompleting;

        public GrnBulkSellingPriceDialog(IReadOnlyList<GrnLineEntryDto> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            foreach (GrnLineEntryDto line in lines)
            {
                var row = new GrnBulkSellingPricePreviewRow(line);
                row.PropertyChanged += Row_PropertyChanged;
                Rows.Add(row);
            }

            InitializeComponent();
            DataContext = this;
            UpdateSummary();
        }

        public ObservableCollection<GrnBulkSellingPricePreviewRow> Rows { get; } = new();

        public IReadOnlyList<string> PriceActions { get; } = new[]
        {
            GrnSellingPriceActionCodes.UpdateMasterPrice,
            GrnSellingPriceActionCodes.SetBatchPriceOverride
        };

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
                PriceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                PriceGrid.CommitEdit(DataGridEditingUnit.Row, true);

                List<GrnBulkSellingPricePreviewRow> selected = Rows.Where(row => row.Apply).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show(this, "Select at least one GRN row.", "No Rows Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                GrnBulkSellingPricePreviewRow? invalid = selected.FirstOrDefault(row =>
                    !string.IsNullOrWhiteSpace(row.ValidationMessage));

                if (invalid != null)
                {
                    MessageBox.Show(
                        this,
                        $"Cannot apply the price action for '{invalid.DisplayName}'.\n\n{invalid.ValidationMessage}",
                        "Invalid Price Action",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                int masterUpdates = selected.Count(row => row.IsMasterUpdate);
                int batchOverrides = selected.Count(row => row.IsBatchOverride);
                int keepCurrent = selected.Count(row => row.IsUseCurrentMaster);

                MessageBoxResult answer = MessageBox.Show(
                    this,
                    $"Apply these GRN price actions?\n\n" +
                    $"Master updates: {masterUpdates}\n" +
                    $"Batch overrides: {batchOverrides}\n" +
                    $"Keep current pricing: {keepCurrent}",
                    "Confirm GRN Price Actions",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                    return;

                foreach (GrnBulkSellingPricePreviewRow row in selected)
                    row.ApplyToSource();

                DialogResult = true;
            }
            finally
            {
                _isCompleting = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCompleting)
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

            int selected = Rows.Count(row => row.Apply);
            int master = Rows.Count(row => row.Apply && row.IsMasterUpdate);
            int batch = Rows.Count(row => row.Apply && row.IsBatchOverride);
            int keep = Rows.Count(row => row.Apply && row.IsUseCurrentMaster);
            txtSummary.Text = $"{selected} selected | {master} master | {batch} batch override | {keep} keep current";
        }

        private void ApplyRetail_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            string action = GrnSellingPriceActionCodes.Normalize(cboBulkAction.SelectedValue?.ToString());

            if (!TryParseMoney(txtBulkRetail.Text, "Retail Price", out decimal retail)) return;

            var selected = GetSelectedRows();
            if (selected == null) return;

            if (!ValidateActionForSelectedRows(action, selected)) return;

            foreach (var row in selected)
            {
                row.SellingPriceAction = action;
                row.NewRetailPrice = retail;
            }
            UpdateSummary();
        }

        private void ApplyWholesale_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            string action = GrnSellingPriceActionCodes.Normalize(cboBulkAction.SelectedValue?.ToString());

            if (!TryParseMoney(txtBulkWholesale.Text, "Wholesale Price", out decimal wholesale)) return;

            var selected = GetSelectedRows();
            if (selected == null) return;

            if (!ValidateActionForSelectedRows(action, selected)) return;

            foreach (var row in selected)
            {
                row.SellingPriceAction = action;
                row.NewWholesalePrice = wholesale;
            }
            UpdateSummary();
        }

        private void ApplyMinimum_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            string action = GrnSellingPriceActionCodes.Normalize(cboBulkAction.SelectedValue?.ToString());

            if (action != GrnSellingPriceActionCodes.UpdateMasterPrice)
            {
                MessageBox.Show(this, "Minimum price can only be applied with the 'Update Master Price' action.", "Action Mismatch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseMoney(txtBulkMinimum.Text, "Minimum Price", out decimal minimum)) return;

            var selected = GetSelectedRows();
            if (selected == null) return;

            if (!ValidateActionForSelectedRows(action, selected)) return;

            foreach (var row in selected)
            {
                row.SellingPriceAction = action;
                row.NewMinimumPrice = minimum;
            }
            UpdateSummary();
        }

        private void ApplyMaximum_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            string action = GrnSellingPriceActionCodes.Normalize(cboBulkAction.SelectedValue?.ToString());

            if (action != GrnSellingPriceActionCodes.UpdateMasterPrice)
            {
                MessageBox.Show(this, "Maximum price can only be applied with the 'Update Master Price' action.", "Action Mismatch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseMoney(txtBulkMaximum.Text, "Maximum Price", out decimal maximum)) return;

            var selected = GetSelectedRows();
            if (selected == null) return;

            if (!ValidateActionForSelectedRows(action, selected)) return;

            foreach (var row in selected)
            {
                row.SellingPriceAction = action;
                row.NewMaximumPrice = maximum;
            }
            UpdateSummary();
        }

        private List<GrnBulkSellingPricePreviewRow>? GetSelectedRows()
        {
            List<GrnBulkSellingPricePreviewRow> selected = Rows.Where(row => row.Apply).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select at least one GRN row.", "No Rows Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            return selected;
        }

        private bool ValidateActionForSelectedRows(string action, List<GrnBulkSellingPricePreviewRow> selected)
        {
            if (action == GrnSellingPriceActionCodes.SetBatchPriceOverride &&
                selected.Any(row => !row.CanUseBatchOverride))
            {
                MessageBox.Show(
                    this,
                    "Batch-only pricing can be applied only to true batch-tracked stock rows.",
                    "Incompatible Rows",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private bool TryParseMoney(string? text, string label, out decimal value)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                MessageBox.Show(this, $"{label} must be entered.", "Value Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                value = 0m;
                return false;
            }

            if ((!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal parsed) &&
                 !decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)) ||
                parsed < 0m)
            {
                MessageBox.Show(this, $"{label} must be a valid non-negative number.", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                value = 0m;
                return false;
            }

            value = RoundMoney(parsed);
            return true;
        }

        private static decimal RoundMoney(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Defer the SelectAll call to allow the TextBox to finish its focus processing.
                // This is a common pattern to handle timing issues in WPF DataGrids.
                Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()), DispatcherPriority.Input);
            }
        }
    }

    public partial class GrnBulkSellingPricePreviewRow : ObservableObject
    {
        public GrnBulkSellingPricePreviewRow(GrnLineEntryDto sourceLine)
        {
            SourceLine = sourceLine ?? throw new ArgumentNullException(nameof(sourceLine));

            string initialAction = GrnSellingPriceActionCodes.Normalize(sourceLine.SellingPriceAction, sourceLine.UpdateSellingPrices);

            // If the initial action is the (now hidden) 'UseCurrentMasterPrice',
            // we must set it to a valid UI option. 'UpdateMasterPrice' is the new default.
            if (initialAction == GrnSellingPriceActionCodes.UseCurrentMasterPrice)
            {
                _sellingPriceAction = GrnSellingPriceActionCodes.UpdateMasterPrice;
            }
            else
            {
                _sellingPriceAction = initialAction;
            }

            _newRetailPrice = sourceLine.NewRetailPrice;
            _newWholesalePrice = sourceLine.NewWholesalePrice;
            _newMinimumPrice = sourceLine.NewMinimumPrice;
            _newMaximumPrice = sourceLine.NewMaximumPrice;
            _apply = true;

            var actions = new List<string> { GrnSellingPriceActionCodes.UpdateMasterPrice };
            if (CanUseBatchOverride)
            {
                actions.Add(GrnSellingPriceActionCodes.SetBatchPriceOverride);
            }
            AvailablePriceActions = actions;

            NormalizeForAction();
        }

        public GrnLineEntryDto SourceLine { get; }
        public string DisplayName => SourceLine.DisplayName;
        public string SkuCode => SourceLine.SkuCode;
        public string BatchNo => string.IsNullOrWhiteSpace(SourceLine.BatchNo) ? "Generated on post" : SourceLine.BatchNo;
        public decimal LandedCost => SourceLine.LandedCost;
        public string CurrentPriceSource => SourceLine.CurrentPriceSource;
        public decimal CurrentRetailPrice => SourceLine.CurrentRetailPrice;
        public decimal CurrentWholesalePrice => SourceLine.CurrentWholesalePrice;
        public decimal CurrentMinimumPrice => SourceLine.CurrentMinimumPrice;
        public decimal CurrentMaximumPrice => SourceLine.CurrentMaximumPrice;
        public bool CanUseBatchOverride => SourceLine.HasBatchTracking;

        [ObservableProperty] private bool _apply;
        [ObservableProperty] private string _sellingPriceAction;
        [ObservableProperty] private decimal _newRetailPrice;
        [ObservableProperty] private decimal _newWholesalePrice;
        [ObservableProperty] private decimal _newMinimumPrice;
        [ObservableProperty] private decimal _newMaximumPrice;

        public IReadOnlyList<string> AvailablePriceActions { get; }

        public bool IsUseCurrentMaster => SellingPriceAction == GrnSellingPriceActionCodes.UseCurrentMasterPrice;
        public bool IsMasterUpdate => SellingPriceAction == GrnSellingPriceActionCodes.UpdateMasterPrice;
        public bool IsBatchOverride => SellingPriceAction == GrnSellingPriceActionCodes.SetBatchPriceOverride;
        public string PriceActionText => GrnSellingPriceActionCodes.ToDisplayText(SellingPriceAction);
        public bool IsMasterPriceFieldsEditable => IsMasterUpdate;


        public string ValidationMessage
        {
            get
            {
                bool priceChanged = NewRetailPrice != CurrentRetailPrice ||
                                    NewWholesalePrice != CurrentWholesalePrice ||
                                    NewMinimumPrice != CurrentMinimumPrice ||
                                    NewMaximumPrice != CurrentMaximumPrice;

                if (IsUseCurrentMaster)
                {
                    return priceChanged
                        ? "Price was changed. You must select 'Update Master' or 'Set Batch Override'."
                        : string.Empty;
                }

                if (IsBatchOverride && !CanUseBatchOverride)
                    return "Batch-only pricing requires a true batch-tracked stock row.";

                if (NewRetailPrice <= 0m)
                    return "Retail price must be greater than zero.";

                // Allow wholesale to be zero for retail-only stores, but not negative.
                if (NewWholesalePrice < 0m)
                    return "Wholesale price cannot be negative.";

                decimal minimum = IsMasterUpdate ? NewMinimumPrice : CurrentMinimumPrice;
                decimal maximum = IsMasterUpdate ? NewMaximumPrice : CurrentMaximumPrice;

                if (minimum < 0m || maximum < 0m)
                    return "Minimum and Maximum prices cannot be negative.";

                if (maximum > 0m && minimum > maximum)
                    return "Minimum Price cannot be greater than Maximum Price.";

                try
                {
                    EffectiveSellingPriceResolver.ValidateOverride(
                        NewRetailPrice,
                        NewWholesalePrice,
                        minimum,
                        maximum);
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message;
                }

                return string.Empty;
            }
        }

        public string WarningText => string.IsNullOrWhiteSpace(ValidationMessage)
            ? PriceActionText
            : ValidationMessage;

        public void ApplyToSource()
        {
            SourceLine.SellingPriceAction = SellingPriceAction;
            SourceLine.UpdateSellingPrices = IsMasterUpdate;
            SourceLine.NewRetailPrice = IsUseCurrentMaster ? SourceLine.CurrentRetailPrice : RoundMoney(NewRetailPrice);
            SourceLine.NewWholesalePrice = IsUseCurrentMaster ? SourceLine.CurrentWholesalePrice : RoundMoney(NewWholesalePrice);
            SourceLine.NewMinimumPrice = IsMasterUpdate ? RoundMoney(NewMinimumPrice) : SourceLine.CurrentMinimumPrice;
            SourceLine.NewMaximumPrice = IsMasterUpdate ? RoundMoney(NewMaximumPrice) : SourceLine.CurrentMaximumPrice;
            SourceLine.RetailMarkupPercent = 0m;
            SourceLine.WholesaleMarkupPercent = 0m;
        }

        partial void OnSellingPriceActionChanged(string value)
        {
            string normalized = GrnSellingPriceActionCodes.Normalize(value);
            if (!string.Equals(value, normalized, StringComparison.Ordinal))
            {
                SellingPriceAction = normalized;
                return;
            }

            if (IsBatchOverride && !CanUseBatchOverride)
                SellingPriceAction = GrnSellingPriceActionCodes.UpdateMasterPrice;

            NormalizeForAction();
            RaiseDerivedProperties();
        }

        partial void OnApplyChanged(bool value) => RaiseDerivedProperties();
        partial void OnNewRetailPriceChanged(decimal value) => RaiseDerivedProperties();
        partial void OnNewWholesalePriceChanged(decimal value) => RaiseDerivedProperties();
        partial void OnNewMinimumPriceChanged(decimal value) => RaiseDerivedProperties();
        partial void OnNewMaximumPriceChanged(decimal value) => RaiseDerivedProperties();

        private void NormalizeForAction()
        {
            if (IsUseCurrentMaster)
            {
                NewRetailPrice = CurrentRetailPrice;
                NewWholesalePrice = CurrentWholesalePrice;
                NewMinimumPrice = CurrentMinimumPrice;
                NewMaximumPrice = CurrentMaximumPrice;
            }
            else if (IsBatchOverride)
            {
                NewMinimumPrice = CurrentMinimumPrice;
                NewMaximumPrice = CurrentMaximumPrice;
            }
        }

        private void RaiseDerivedProperties()
        {
            OnPropertyChanged(nameof(IsUseCurrentMaster));
            OnPropertyChanged(nameof(IsMasterUpdate));
            OnPropertyChanged(nameof(IsBatchOverride));
            OnPropertyChanged(nameof(PriceActionText));
            OnPropertyChanged(nameof(IsMasterPriceFieldsEditable));
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(WarningText));
        }

        private static decimal RoundMoney(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

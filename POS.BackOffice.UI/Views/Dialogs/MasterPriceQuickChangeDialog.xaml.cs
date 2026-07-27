using System;
using System.Windows;
using POS.BackOffice.UI.ViewModels;
using POS.Core.Models.DTOs;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class MasterPriceQuickChangeDialog : Window
    {
        private bool _isCompleting;

        public MasterPriceQuickChangeDialog(PriceManagementSummaryDto source)
        {
            InitializeComponent();
            ViewModel = new MasterPriceQuickChangeDialogViewModel(source);
            DataContext = ViewModel;
        }

        public MasterPriceQuickChangeDialogViewModel ViewModel { get; }
        public MasterPriceQuickChangeResult? Result { get; private set; }

        private void Calculate_Click(object sender, RoutedEventArgs e) =>
            ViewModel.TryCalculateSuggestions();

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleting || !ViewModel.TryBuildResult(out MasterPriceQuickChangeResult? result) || result == null)
                return;

            _isCompleting = true;
            try
            {
                string warning = string.IsNullOrWhiteSpace(ViewModel.CostWarning)
                    ? string.Empty
                    : $"\n\n{ViewModel.CostWarning}";

                MessageBoxResult confirmation = MessageBox.Show(
                    this,
                    $"Save these master prices for {ViewModel.DisplayDescription}?\n\n" +
                    $"Minimum: {ViewModel.CurrentMinimum:N2} → {result.MinimumPrice:N2}\n" +
                    $"Retail: {ViewModel.CurrentRetail:N2} → {result.RetailPrice:N2}\n" +
                    $"Wholesale: {ViewModel.CurrentWholesale:N2} → {result.WholesalePrice:N2}\n" +
                    $"Maximum: {ViewModel.CurrentMaximum:N2} → {result.MaximumPrice:N2}\n\n" +
                    $"Selected cost basis: {ViewModel.SelectedCost:N2}\n" +
                    $"Retail profit / margin: {ViewModel.RetailProfit:N2} / {ViewModel.RetailMargin:N2}%\n" +
                    $"Wholesale profit / margin: {ViewModel.WholesaleProfit:N2} / {ViewModel.WholesaleMargin:N2}%" +
                    warning,
                    "Confirm Master Price Change",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes)
                    return;

                Result = result;
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
    }
}

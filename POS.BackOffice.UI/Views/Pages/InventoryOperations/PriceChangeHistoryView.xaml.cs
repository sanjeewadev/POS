using System;
using System.Windows;
using System.Windows.Controls;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class PriceChangeHistoryView : UserControl
    {
        public PriceChangeHistoryView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PriceChangeHistoryViewModel viewModel)
                return;

            if (!viewModel.InitializeCommand.CanExecute(null))
                return;

            try
            {
                await viewModel.InitializeCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize price change history page:\n\n{ex.Message}",
                    "Price Change History",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
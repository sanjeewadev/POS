using System;
using System.Windows;
using System.Windows.Controls;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Purchasing
{
    public partial class PurchaseOrderDashboardView : UserControl
    {
        public PurchaseOrderDashboardView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PurchaseOrderDashboardViewModel viewModel)
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
                    $"Failed to initialize Purchase Order Dashboard:\n\n{ex.Message}",
                    "Purchase Order Dashboard Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
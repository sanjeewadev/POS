using System;
using System.Windows;
using System.Windows.Controls;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class GrnDashboardView : UserControl
    {
        public GrnDashboardView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not GrnDashboardViewModel viewModel)
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
                    $"Failed to initialize GRN Dashboard:\n\n{ex.Message}",
                    "GRN Dashboard Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
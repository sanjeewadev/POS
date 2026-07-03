using System;
using System.Windows;
using System.Windows.Controls;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryPages
{
    public partial class ItemPropertyView : UserControl
    {
        public ItemPropertyView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ItemPropertyViewModel viewModel)
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
                    $"Failed to initialize Item Property page:\n\n{ex.Message}",
                    "Item Property Page Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
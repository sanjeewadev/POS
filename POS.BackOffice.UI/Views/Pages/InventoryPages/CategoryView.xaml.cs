using System;
using System.Windows;
using System.Windows.Controls;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryPages
{
    /// <summary>
    /// Interaction logic for CategoryView.xaml
    /// </summary>
    public partial class CategoryView : UserControl
    {
        public CategoryView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CategoryViewModel viewModel)
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
                    $"Failed to initialize Category page:\n\n{ex.Message}",
                    "Category Page Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
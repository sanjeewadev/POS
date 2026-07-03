using System;
using System.Windows;
using System.Windows.Controls;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryPages
{
    /// <summary>
    /// Interaction logic for SubCategoryView.xaml
    /// </summary>
    public partial class SubCategoryView : UserControl
    {
        public SubCategoryView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SubCategoryViewModel viewModel)
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
                    $"Failed to initialize Sub-Category page:\n\n{ex.Message}",
                    "Sub-Category Page Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
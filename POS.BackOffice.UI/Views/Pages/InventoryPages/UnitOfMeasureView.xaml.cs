using System;
using System.Windows;
using System.Windows.Controls;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryPages
{
    /// <summary>
    /// Interaction logic for UnitOfMeasureView.xaml
    /// </summary>
    public partial class UnitOfMeasureView : UserControl
    {
        public UnitOfMeasureView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not UnitOfMeasureViewModel viewModel)
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
                    $"Failed to initialize Unit of Measure page:\n\n{ex.Message}",
                    "Unit of Measure Page Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
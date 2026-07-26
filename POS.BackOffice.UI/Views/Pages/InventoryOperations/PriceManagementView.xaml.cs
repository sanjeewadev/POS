using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class PriceManagementView : UserControl
    {
        public PriceManagementView()
        {
            InitializeComponent();
        }

        private async void SaveMasterPrices_Click(object sender, RoutedEventArgs e)
        {
            if (!TryCommitPriceEditors(
                    "Master Price Validation",
                    MasterMinimumPriceTextBox,
                    MasterRetailPriceTextBox,
                    MasterWholesalePriceTextBox,
                    MasterMaximumPriceTextBox))
            {
                return;
            }

            if (DataContext is PriceManagementViewModel viewModel &&
                viewModel.SaveMasterPricesCommand.CanExecute(null))
            {
                await viewModel.SaveMasterPricesCommand.ExecuteAsync(null);
            }
        }

        private void ResetMasterPrices_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PriceManagementViewModel viewModel ||
                !viewModel.ResetMasterPricesCommand.CanExecute(null))
            {
                return;
            }

            viewModel.ResetMasterPricesCommand.Execute(null);
            RefreshPriceEditors(
                MasterMinimumPriceTextBox,
                MasterRetailPriceTextBox,
                MasterWholesalePriceTextBox,
                MasterMaximumPriceTextBox);
        }

        private async void SaveBatchOverride_Click(object sender, RoutedEventArgs e)
        {
            if (!TryCommitPriceEditors(
                    "Batch Price Validation",
                    BatchOverrideRetailPriceTextBox,
                    BatchOverrideWholesalePriceTextBox))
            {
                return;
            }

            if (DataContext is PriceManagementViewModel viewModel &&
                viewModel.SaveBatchOverrideCommand.CanExecute(null))
            {
                await viewModel.SaveBatchOverrideCommand.ExecuteAsync(null);
            }
        }

        private void ResetBatchOverride_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PriceManagementViewModel viewModel ||
                !viewModel.ResetBatchOverrideCommand.CanExecute(null))
            {
                return;
            }

            viewModel.ResetBatchOverrideCommand.Execute(null);
            RefreshPriceEditors(
                BatchOverrideRetailPriceTextBox,
                BatchOverrideWholesalePriceTextBox);
        }

        private static bool TryCommitPriceEditors(string title, params TextBox[] editors)
        {
            foreach (TextBox editor in editors)
            {
                BindingExpression? binding = editor.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
            }

            TextBox? invalidEditor = editors.FirstOrDefault(Validation.GetHasError);
            if (invalidEditor == null)
                return true;

            invalidEditor.Focus();
            invalidEditor.SelectAll();
            MessageBox.Show(
                "Enter a valid numeric value in every price field before saving.",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        private static void RefreshPriceEditors(params TextBox[] editors)
        {
            foreach (TextBox editor in editors)
            {
                BindingExpression? binding = editor.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateTarget();
            }
        }
    }
}

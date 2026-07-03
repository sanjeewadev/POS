using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class StockAdjustmentView : UserControl
    {
        public StockAdjustmentView()
        {
            InitializeComponent();
        }

        private void DgAdjustmentLines_CellEditEnding(
            object sender,
            DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is not StockAdjustmentViewModel viewModel)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    viewModel.RecalculateImpact();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to recalculate stock adjustment totals:\n\n{ex.Message}",
                        "Stock Adjustment Calculation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }), DispatcherPriority.Background);
        }
    }
}
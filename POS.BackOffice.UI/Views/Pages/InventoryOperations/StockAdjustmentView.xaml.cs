using System;
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

        private void DgAdjustmentLines_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is StockAdjustmentViewModel viewModel)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.RecalculateImpact();
                }), DispatcherPriority.Background);
            }
        }
    }
}
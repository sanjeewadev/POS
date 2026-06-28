using System;
using System.Windows.Controls;
using System.Windows.Threading;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class GrnView : UserControl
    {
        public GrnView()
        {
            InitializeComponent();
        }

        private void DgGrnLines_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is GrnViewModel viewModel)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.RecalculateTotals();
                }), DispatcherPriority.Background);
            }
        }
    }
}
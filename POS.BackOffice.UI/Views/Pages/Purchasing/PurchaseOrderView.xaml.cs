using System;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Purchasing
{
    public partial class PurchaseOrderView : UserControl
    {
        public PurchaseOrderView()
        {
            InitializeComponent();

            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<PurchaseOrderViewModel>();
            }
        }

        // Triggered the moment a user finishes editing a cell in the main DataGrid
        private void DgPoLines_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (this.DataContext is PurchaseOrderViewModel viewModel)
            {
                // Delay by a fraction of a millisecond so the new text has time to bind to the PO Line model
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.RecalculateTotals();
                }), DispatcherPriority.Background);
            }
        }
    }
}